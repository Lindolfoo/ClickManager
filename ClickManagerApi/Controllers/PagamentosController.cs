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
public class PagamentosController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public PagamentosController(ApplicationDbContext db) => _db = db;

    private int FotografoId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET /api/pagamentos
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var fid = FotografoId;
        var lista = await _db.Pagamentos
            .Include(p => p.Ensaio).ThenInclude(e => e.Cliente)
            .Where(p => p.Ensaio.FotografoId == fid)
            .OrderByDescending(p => p.CriadoEm)
            .Select(p => new {
                p.Id, p.Valor, p.Tipo, p.Metodo, p.Status, p.DataPagamento,
                Ensaio  = p.Ensaio.Titulo,
                Cliente = p.Ensaio.Cliente.Nome
            })
            .ToListAsync();

        return Ok(lista);
    }

    // POST /api/pagamentos
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Pagamento pagamento)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        pagamento.CriadoEm = DateTime.UtcNow;
        _db.Pagamentos.Add(pagamento);
        await _db.SaveChangesAsync();
        return Ok(pagamento);
    }

    // PATCH /api/pagamentos/5/confirmar
    [HttpPatch("{id}/confirmar")]
    public async Task<IActionResult> Confirmar(int id)
    {
        var p = await _db.Pagamentos.FindAsync(id);
        if (p is null) return NotFound();
        p.Status        = "Pago";
        p.DataPagamento = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(p);
    }
}
