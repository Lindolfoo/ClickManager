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
public class ContratosController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public ContratosController(ApplicationDbContext db) => _db = db;

    private int FotografoId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET /api/contratos
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var fid = FotografoId;
        var lista = await _db.Contratos
            .Include(c => c.Ensaio).ThenInclude(e => e.Cliente)
            .Where(c => c.Ensaio.FotografoId == fid)
            .OrderByDescending(c => c.CriadoEm)
            .Select(c => new {
                c.Id, c.Numero, c.Status, c.DataAssinatura, c.CriadoEm,
                Ensaio  = c.Ensaio.Titulo,
                Cliente = c.Ensaio.Cliente.Nome,
                Valor   = c.Ensaio.Valor
            })
            .ToListAsync();

        return Ok(lista);
    }

    // POST /api/contratos  — cria ou atualiza contrato de um ensaio (upsert)
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CriarContratoRequest req)
    {
        var fid    = FotografoId;
        var ensaio = await _db.Ensaios
            .FirstOrDefaultAsync(e => e.Id == req.EnsaioId && e.FotografoId == fid);
        if (ensaio is null) return NotFound(new { message = "Ensaio não encontrado." });

        var existente = await _db.Contratos
            .FirstOrDefaultAsync(c => c.EnsaioId == req.EnsaioId);

        if (existente is not null)
        {
            existente.Conteudo = req.Conteudo;
            await _db.SaveChangesAsync();
            return Ok(existente);
        }

        var totalContratos = await _db.Contratos
            .Where(c => c.Ensaio!.FotografoId == fid)
            .CountAsync();
        var numero = "CTR" + (totalContratos + 1).ToString("D5");

        var contrato = new Contrato
        {
            EnsaioId  = req.EnsaioId,
            Numero    = numero,
            Conteudo  = req.Conteudo,
            Status    = "Pendente",
            CriadoEm = DateTime.UtcNow
        };
        _db.Contratos.Add(contrato);
        await _db.SaveChangesAsync();
        return Ok(contrato);
    }

    // PATCH /api/contratos/5/assinar
    [HttpPatch("{id}/assinar")]
    public async Task<IActionResult> Assinar(int id)
    {
        var c = await _db.Contratos.FindAsync(id);
        if (c is null) return NotFound();
        c.Status         = "Assinado";
        c.DataAssinatura = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(c);
    }
}

public record CriarContratoRequest(int EnsaioId, string? Conteudo = null);
