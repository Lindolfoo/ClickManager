using System.Security.Claims;
using System.Security.Cryptography;
using ClickManagerApi.Data;
using ClickManagerApi.Models.Entities;
using ClickManagerApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace ClickManagerApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GaleriaController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IFileUploadService   _upload;
    private readonly IConfiguration       _config;

    public GaleriaController(ApplicationDbContext db, IFileUploadService upload, IConfiguration config)
    {
        _db     = db;
        _upload = upload;
        _config = config;
    }

    private int FotografoId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET /api/galeria/{ensaioId}
    [HttpGet("{ensaioId:int}")]
    public async Task<IActionResult> GetAll(int ensaioId)
    {
        var fid = FotografoId;
        var ensaio = await _db.Ensaios
            .FirstOrDefaultAsync(e => e.Id == ensaioId && e.FotografoId == fid);
        if (ensaio is null) return NotFound();

        var imgs = await _db.ImagensGaleria
            .Where(i => i.EnsaioId == ensaioId)
            .OrderBy(i => i.Ordem)
            .ToListAsync();

        return Ok(imgs);
    }

    // POST /api/galeria/{ensaioId}  — upload de múltiplos arquivos
    [HttpPost("{ensaioId:int}")]
    public async Task<IActionResult> Upload(int ensaioId, [FromForm] IFormFile[] arquivos)
    {
        var fid = FotografoId;
        var ensaio = await _db.Ensaios
            .FirstOrDefaultAsync(e => e.Id == ensaioId && e.FotografoId == fid);
        if (ensaio is null) return NotFound(new { message = "Ensaio não encontrado." });

        if (arquivos is null || arquivos.Length == 0)
            return BadRequest(new { message = "Nenhum arquivo enviado." });

        var ordemMax = await _db.ImagensGaleria
            .Where(i => i.EnsaioId == ensaioId)
            .MaxAsync(i => (int?)i.Ordem) ?? 0;

        var resultado = new List<ImagemGaleria>();
        foreach (var arquivo in arquivos)
        {
            if (arquivo.Length == 0) continue;

            string url;
            try { url = await _upload.UploadAsync(arquivo, "galeria"); }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }

            var imagem = new ImagemGaleria
            {
                EnsaioId     = ensaioId,
                NomeArquivo  = arquivo.FileName,
                Url          = url,
                Ordem        = ++ordemMax,
                TamanhoBytes = arquivo.Length,
                CriadoEm    = DateTime.UtcNow
            };
            _db.ImagensGaleria.Add(imagem);
            resultado.Add(imagem);
        }

        ensaio.TotalImagens += resultado.Count;
        await _db.SaveChangesAsync();
        return Ok(resultado);
    }

    // DELETE /api/galeria/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var fid = FotografoId;
        var img = await _db.ImagensGaleria
            .Include(i => i.Ensaio)
            .FirstOrDefaultAsync(i => i.Id == id && i.Ensaio!.FotografoId == fid);
        if (img is null) return NotFound();

        _upload.Delete(img.Url);
        _db.ImagensGaleria.Remove(img);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // GET /api/galeria/{ensaioId}/link — status atual do link público (sem gerar nada)
    [HttpGet("{ensaioId:int}/link")]
    public async Task<IActionResult> GetLink(int ensaioId)
    {
        var fid = FotografoId;
        var ensaio = await _db.Ensaios
            .FirstOrDefaultAsync(e => e.Id == ensaioId && e.FotografoId == fid);
        if (ensaio is null) return NotFound();

        return Ok(new
        {
            ativo = ensaio.LinkGaleriaAtivo,
            url   = ensaio.LinkGaleriaAtivo ? MontarUrl(ensaio.LinkGaleriaToken!) : null
        });
    }

    // POST /api/galeria/{ensaioId}/link?regenerar=true — cria (ou reativa) o link público
    [HttpPost("{ensaioId:int}/link")]
    public async Task<IActionResult> CriarLink(int ensaioId, [FromQuery] bool regenerar = false)
    {
        var fid = FotografoId;
        var ensaio = await _db.Ensaios
            .FirstOrDefaultAsync(e => e.Id == ensaioId && e.FotografoId == fid);
        if (ensaio is null) return NotFound();

        if (ensaio.LinkGaleriaToken is null || regenerar)
            ensaio.LinkGaleriaToken = GerarTokenSeguro();

        ensaio.LinkGaleriaAtivo = true;
        await _db.SaveChangesAsync();

        return Ok(new { ativo = true, url = MontarUrl(ensaio.LinkGaleriaToken!) });
    }

    // DELETE /api/galeria/{ensaioId}/link — revoga o acesso (mantém o token, só desativa)
    [HttpDelete("{ensaioId:int}/link")]
    public async Task<IActionResult> RevogarLink(int ensaioId)
    {
        var fid = FotografoId;
        var ensaio = await _db.Ensaios
            .FirstOrDefaultAsync(e => e.Id == ensaioId && e.FotografoId == fid);
        if (ensaio is null) return NotFound();

        ensaio.LinkGaleriaAtivo = false;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>192 bits de entropia, Base64 URL-safe — inadivinhável, mas curto o bastante pra caber numa URL.</summary>
    private static string GerarTokenSeguro()
        => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(24));

    private string MontarUrl(string token)
    {
        var baseUrl = (_config["App:FrontendBaseUrl"] ?? "http://localhost:5500").TrimEnd('/');
        return $"{baseUrl}/galeria-publica.html?token={Uri.EscapeDataString(token)}";
    }
}
