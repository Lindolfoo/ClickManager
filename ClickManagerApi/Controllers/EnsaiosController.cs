using System.Security.Claims;
using ClickManagerApi.Data;
using ClickManagerApi.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClickManagerApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EnsaiosController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public EnsaiosController(ApplicationDbContext db) => _db = db;

    private int FotografoId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET /api/ensaios?de=2026-01-01&ate=2026-12-31
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] DateTime? de  = null,
        [FromQuery] DateTime? ate = null)
    {
        var fid = FotografoId;
        var q = _db.Ensaios
            .Include(e => e.Cliente)
            .Include(e => e.Contrato)
            .Where(e => e.FotografoId == fid);

        if (de  is not null) q = q.Where(e => e.DataHora >= de);
        if (ate is not null) q = q.Where(e => e.DataHora <= ate);

        var lista = await q
            .OrderByDescending(e => e.DataHora)
            .Select(e => new {
                e.Id, e.Titulo, e.DataHora, e.Local, e.Valor, e.Status, e.TotalImagens,
                Cliente    = e.Cliente.Nome,
                Contrato   = e.Contrato != null ? e.Contrato.Numero : null,
                StatusPgto = _db.Pagamentos
                                .Where(p => p.EnsaioId == e.Id)
                                .All(p => p.Status == "Pago") ? "Pago" : "Pendente"
            })
            .ToListAsync();

        return Ok(lista);
    }

    // GET /api/ensaios/5
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var e = await _db.Ensaios
            .Include(e => e.Cliente)
            .Include(e => e.Contrato)
            .Include(e => e.Pagamentos)
            .Include(e => e.ImagensGaleria)
            .FirstOrDefaultAsync(e => e.Id == id && e.FotografoId == FotografoId);

        return e is null ? NotFound() : Ok(e);
    }

    // POST /api/ensaios
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Ensaio ensaio)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        ensaio.FotografoId   = FotografoId;
        ensaio.CriadoEm     = DateTime.UtcNow;
        ensaio.AtualizadoEm = DateTime.UtcNow;

        _db.Ensaios.Add(ensaio);
        await _db.SaveChangesAsync();

        var contrato = new Contrato
        {
            EnsaioId = ensaio.Id,
            Numero   = "CTR" + ensaio.Id.ToString("D5"),
            Status   = "Pendente",
            CriadoEm = DateTime.UtcNow
        };
        _db.Contratos.Add(contrato);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = ensaio.Id }, ensaio);
    }

    // PUT /api/ensaios/5
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Ensaio dados)
    {
        var e = await _db.Ensaios
            .FirstOrDefaultAsync(e => e.Id == id && e.FotografoId == FotografoId);
        if (e is null) return NotFound();

        e.Titulo       = dados.Titulo;
        e.DataHora     = dados.DataHora;
        e.Local        = dados.Local;
        e.Valor        = dados.Valor;
        e.Status       = dados.Status;
        e.Observacoes  = dados.Observacoes;
        e.AtualizadoEm = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(e);
    }

    // DELETE /api/ensaios/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var e = await _db.Ensaios
            .FirstOrDefaultAsync(e => e.Id == id && e.FotografoId == FotografoId);
        if (e is null) return NotFound();
        _db.Ensaios.Remove(e);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // GET /api/ensaios/dashboard
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var fid = FotografoId;

        var proximosEnsaios = await _db.Ensaios
            .Where(e => e.FotografoId == fid && e.DataHora >= DateTime.Today)
            .CountAsync();

        var pgtosPendentes = await _db.Pagamentos
            .Where(p => p.Ensaio.FotografoId == fid && p.Status == "Pendente")
            .CountAsync();

        var clientesAtivos = await _db.Clientes
            .Where(c => c.FotografoId == fid && c.Status == "Ativo")
            .CountAsync();

        var totalRecebido = await _db.Pagamentos
            .Where(p => p.Ensaio.FotografoId == fid && p.Status == "Pago")
            .SumAsync(p => p.Valor);

        var agendaSemana = await _db.Ensaios
            .Include(e => e.Cliente)
            .Include(e => e.Contrato)
            .Where(e => e.FotografoId == fid &&
                        e.DataHora >= DateTime.Today &&
                        e.DataHora <= DateTime.Today.AddDays(7))
            .OrderBy(e => e.DataHora)
            .Select(e => new { e.Titulo, e.DataHora, e.Local, Cliente = e.Cliente.Nome, Contrato = e.Contrato!.Numero })
            .ToListAsync();

        return Ok(new
        {
            ProximosEnsaios     = proximosEnsaios,
            PagamentosPendentes = pgtosPendentes,
            ClientesAtivos      = clientesAtivos,
            TotalRecebido       = totalRecebido,
            AgendaSemana        = agendaSemana
        });
    }
}
