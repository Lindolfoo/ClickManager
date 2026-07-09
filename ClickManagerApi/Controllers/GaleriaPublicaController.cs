using ClickManagerApi.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClickManagerApi.Controllers;

// Rota separada da GaleriaController (que exige [Authorize]): aqui é o link
// que o fotógrafo manda pro cliente final, sem login nenhum.
[ApiController]
[Route("api/galeria-publica")]
[AllowAnonymous]
public class GaleriaPublicaController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public GaleriaPublicaController(ApplicationDbContext db) => _db = db;

    // GET /api/galeria-publica/{token}
    [HttpGet("{token}")]
    public async Task<IActionResult> Get(string token)
    {
        var ensaio = await _db.Ensaios
            .Include(e => e.Fotografo)
            .Include(e => e.ImagensGaleria)
            .FirstOrDefaultAsync(e => e.LinkGaleriaToken == token && e.LinkGaleriaAtivo);

        if (ensaio is null) return NotFound(new { message = "Link inválido ou desativado." });

        // DTO enxuto: nada de Valor, Observacoes, ClienteId ou dados internos —
        // só o que o cliente final precisa pra ver a própria galeria.
        return Ok(new
        {
            titulo    = ensaio.Titulo,
            dataHora  = ensaio.DataHora,
            local     = ensaio.Local,
            fotografo = new
            {
                nome      = ensaio.Fotografo?.Nome,
                instagram = ensaio.Fotografo?.Instagram
            },
            imagens = ensaio.ImagensGaleria
                .OrderBy(i => i.Ordem)
                .Select(i => new { i.Url, i.Ordem })
        });
    }
}
