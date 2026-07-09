using System.Security.Claims;
using ClickManagerApi.Data;
using ClickManagerApi.Models.Entities;
using ClickManagerApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClickManagerApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PortfolioController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IFileUploadService   _upload;

    public PortfolioController(ApplicationDbContext db, IFileUploadService upload)
    {
        _db     = db;
        _upload = upload;
    }

    private int FotografoId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET /api/portfolio?fotografoId=1  (público — galeria de portfólio)
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll([FromQuery] int? fotografoId = null)
    {
        var fid = fotografoId ?? (User.Identity?.IsAuthenticated == true ? FotografoId : 0);
        var imgs = await _db.ImagensPortfolio
            .Where(i => i.FotografoId == fid)
            .OrderBy(i => i.Ordem)
            .ToListAsync();
        return Ok(imgs);
    }

    // POST /api/portfolio/upload  — upload de arquivo
    [HttpPost("upload")]
    [Authorize]
    public async Task<IActionResult> Upload([FromForm] IFormFile arquivo)
    {
        if (arquivo is null || arquivo.Length == 0)
            return BadRequest(new { message = "Nenhum arquivo enviado." });

        string url;
        try { url = await _upload.UploadAsync(arquivo, "portfolio"); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }

        var fid      = FotografoId;
        var ordemMax = await _db.ImagensPortfolio
            .Where(i => i.FotografoId == fid)
            .MaxAsync(i => (int?)i.Ordem) ?? 0;

        var imagem = new ImagemPortfolio
        {
            FotografoId  = fid,
            NomeArquivo  = arquivo.FileName,
            Url          = url,
            Ordem        = ordemMax + 1,
            TamanhoBytes = arquivo.Length,
            CriadoEm    = DateTime.UtcNow
        };

        _db.ImagensPortfolio.Add(imagem);
        await _db.SaveChangesAsync();
        return Ok(imagem);
    }

    // POST /api/portfolio  — adiciona por URL (sem upload de arquivo)
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Add([FromBody] AdicionarPortfolioRequest req)
    {
        var fid      = FotografoId;
        var ordemMax = await _db.ImagensPortfolio
            .Where(i => i.FotografoId == fid)
            .MaxAsync(i => (int?)i.Ordem) ?? 0;

        var imagem = new ImagemPortfolio
        {
            FotografoId = fid,
            NomeArquivo = req.NomeArquivo ?? Path.GetFileName(req.Url),
            Url         = req.Url,
            Ordem       = req.Ordem > 0 ? req.Ordem : ordemMax + 1,
            CriadoEm   = DateTime.UtcNow
        };

        _db.ImagensPortfolio.Add(imagem);
        await _db.SaveChangesAsync();
        return Ok(imagem);
    }

    // DELETE /api/portfolio/5
    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> Delete(int id)
    {
        var img = await _db.ImagensPortfolio
            .FirstOrDefaultAsync(i => i.Id == id && i.FotografoId == FotografoId);
        if (img is null) return NotFound();

        _upload.Delete(img.Url);
        _db.ImagensPortfolio.Remove(img);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // PATCH /api/portfolio/reordenar
    [HttpPatch("reordenar")]
    [Authorize]
    public async Task<IActionResult> Reordenar([FromBody] List<OrdemItem> itens)
    {
        var fid = FotografoId;
        foreach (var item in itens)
        {
            var img = await _db.ImagensPortfolio
                .FirstOrDefaultAsync(i => i.Id == item.Id && i.FotografoId == fid);
            if (img is not null) img.Ordem = item.Ordem;
        }
        await _db.SaveChangesAsync();
        return Ok();
    }
}

public record OrdemItem(int Id, int Ordem);
public record AdicionarPortfolioRequest(string Url, string? NomeArquivo = null, int Ordem = 0);
