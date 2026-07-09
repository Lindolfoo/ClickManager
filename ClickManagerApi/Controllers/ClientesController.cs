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
public class ClientesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public ClientesController(ApplicationDbContext db) => _db = db;

    private int FotografoId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET /api/clientes
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var fid = FotografoId;
        var lista = await _db.Clientes
            .Where(c => c.FotografoId == fid)
            .OrderByDescending(c => c.CriadoEm)
            .Select(c => new {
                c.Id, c.Nome, c.Email, c.Telefone,
                c.TipoEnsaio, c.Status, c.CriadoEm,
                UltimoEnsaio = _db.Ensaios
                    .Where(e => e.ClienteId == c.Id)
                    .OrderByDescending(e => e.DataHora)
                    .Select(e => (DateTime?)e.DataHora)
                    .FirstOrDefault()
            })
            .ToListAsync();

        return Ok(lista);
    }

    // GET /api/clientes/5
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var c = await _db.Clientes
            .Include(c => c.Ensaios)
            .FirstOrDefaultAsync(c => c.Id == id && c.FotografoId == FotografoId);

        return c is null ? NotFound() : Ok(c);
    }

    // POST /api/clientes
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Cliente cliente)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        cliente.FotografoId = FotografoId;
        cliente.CriadoEm    = DateTime.UtcNow;
        _db.Clientes.Add(cliente);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = cliente.Id }, cliente);
    }

    // PUT /api/clientes/5
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Cliente dados)
    {
        var c = await _db.Clientes
            .FirstOrDefaultAsync(c => c.Id == id && c.FotografoId == FotografoId);
        if (c is null) return NotFound();

        c.Nome        = dados.Nome;
        c.Email       = dados.Email;
        c.Telefone    = dados.Telefone;
        c.TipoEnsaio  = dados.TipoEnsaio;
        c.Status      = dados.Status;
        c.Observacoes = dados.Observacoes;

        await _db.SaveChangesAsync();
        return Ok(c);
    }

    // DELETE /api/clientes/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var c = await _db.Clientes
            .FirstOrDefaultAsync(c => c.Id == id && c.FotografoId == FotografoId);
        if (c is null) return NotFound();
        _db.Clientes.Remove(c);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
