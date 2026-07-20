using ClickManagerApi.Data;
using ClickManagerApi.Models;
using ClickManagerApi.Models.Entities;
using ClickManagerApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ClickManagerApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext     _db;
    private readonly IAuthService             _auth;
    private readonly IEmailService            _email;
    private readonly IFileUploadService       _upload;
    private readonly IConfiguration           _config;
    private readonly ILogger<AuthController>  _logger;

    public AuthController(
        ApplicationDbContext    db,
        IAuthService            auth,
        IEmailService           email,
        IFileUploadService      upload,
        IConfiguration          config,
        ILogger<AuthController> logger)
    {
        _db     = db;
        _auth   = auth;
        _email  = email;
        _upload = upload;
        _config = config;
        _logger = logger;
    }

    // POST /api/auth/login
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var fotografo = await _db.Fotografos
            .FirstOrDefaultAsync(f => f.Email == req.Email);

        if (fotografo is null || !BCrypt.Net.BCrypt.Verify(req.Senha, fotografo.SenhaHash))
            return Unauthorized(new { message = "Email ou senha inválidos." });

        return Ok(BuildResponse(fotografo));
    }

    // POST /api/auth/register
    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Senha) || req.Senha.Length < 8)
            return BadRequest(new { message = "A senha deve ter pelo menos 8 caracteres." });

        if (await _db.Fotografos.AnyAsync(f => f.Email == req.Email))
            return Conflict(new { message = "Este e-mail já está cadastrado." });

        var fotografo = new Fotografo
        {
            Nome         = req.Nome,
            Email        = req.Email,
            SenhaHash    = BCrypt.Net.BCrypt.HashPassword(req.Senha),
            Telefone     = req.Telefone,
            PlanoAtivo   = req.PlanoAtivo,
            CriadoEm    = DateTime.UtcNow,
            AtualizadoEm = DateTime.UtcNow
        };

        _db.Fotografos.Add(fotografo);
        await _db.SaveChangesAsync();

        return Created($"/api/fotografos/{fotografo.Id}", BuildResponse(fotografo));
    }

    // POST /api/auth/forgot-password
    // Sempre devolve 200 com mensagem genérica: revelar se o e-mail existe
    // permitiria enumerar contas cadastradas (user enumeration).
    [HttpPost("forgot-password")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req)
    {
        const string mensagemGenerica =
            "Se este e-mail estiver cadastrado, você receberá um link em breve.";

        var email = (req.Email ?? string.Empty).Trim();
        if (email.Length == 0)
            return Ok(new { message = mensagemGenerica });

        var fotografo = await _db.Fotografos.FirstOrDefaultAsync(f => f.Email == email);
        if (fotografo is null)
            return Ok(new { message = mensagemGenerica });

        // Invalida tokens anteriores ainda válidos deste usuário
        var agora = DateTime.UtcNow;
        var anteriores = await _db.TokensRecuperacaoSenha
            .Where(t => t.FotografoId == fotografo.Id && t.UsadoEm == null && t.ExpiraEm > agora)
            .ToListAsync();
        foreach (var t in anteriores) t.UsadoEm = agora;

        // Token em claro vai só no e-mail; no banco guardamos o hash SHA-256
        var tokenClaro = GerarTokenSeguro();

        _db.TokensRecuperacaoSenha.Add(new TokenRecuperacaoSenha
        {
            FotografoId = fotografo.Id,
            TokenHash   = HashToken(tokenClaro),
            ExpiraEm    = agora.AddHours(1),
            CriadoEm    = agora
        });
        await _db.SaveChangesAsync();

        var baseUrl = (_config["App:FrontendBaseUrl"] ?? "http://localhost:5500").TrimEnd('/');
        var link    = $"{baseUrl}/redefinir-senha.html?token={Uri.EscapeDataString(tokenClaro)}";

        try
        {
            await _email.SendPasswordResetEmailAsync(fotografo.Email, fotografo.Nome, link);
        }
        catch (Exception ex)
        {
            // Falha de SMTP não deve revelar nada ao cliente nem derrubar a requisição
            _logger.LogError(ex, "Falha ao enviar e-mail de redefinição para {Email}", fotografo.Email);
        }

        return Ok(new { message = mensagemGenerica });
    }

    // POST /api/auth/reset-password
    [HttpPost("reset-password")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Token))
            return BadRequest(new { message = "Token inválido ou expirado." });

        if (string.IsNullOrWhiteSpace(req.NovaSenha) || req.NovaSenha.Length < 8)
            return BadRequest(new { message = "A nova senha deve ter pelo menos 8 caracteres." });

        var hash  = HashToken(req.Token);
        var agora = DateTime.UtcNow;

        var token = await _db.TokensRecuperacaoSenha
            .Include(t => t.Fotografo)
            .FirstOrDefaultAsync(t => t.TokenHash == hash);

        // Mesma mensagem para token inexistente, já usado ou expirado
        if (token is null || token.UsadoEm != null || token.ExpiraEm <= agora || token.Fotografo is null)
            return BadRequest(new { message = "Token inválido ou expirado." });

        token.Fotografo.SenhaHash    = BCrypt.Net.BCrypt.HashPassword(req.NovaSenha);
        token.Fotografo.AtualizadoEm = agora;
        token.UsadoEm                = agora;   // uso único

        await _db.SaveChangesAsync();

        _logger.LogInformation("Senha redefinida para o fotógrafo {Id}", token.FotografoId);
        return Ok(new { message = "Senha redefinida com sucesso. Faça login com a nova senha." });
    }

    // PUT /api/auth/senha — troca de senha estando logado (exige a senha atual).
    [HttpPut("senha")]
    [Authorize]
    public async Task<IActionResult> AlterarSenha([FromBody] AlterarSenhaRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.NovaSenha) || req.NovaSenha.Length < 8)
            return BadRequest(new { message = "A nova senha deve ter pelo menos 8 caracteres." });

        var fotografo = await _db.Fotografos.FindAsync(FotografoId);
        if (fotografo is null) return NotFound();

        if (!BCrypt.Net.BCrypt.Verify(req.SenhaAtual, fotografo.SenhaHash))
            return Unauthorized(new { message = "Senha atual incorreta." });

        fotografo.SenhaHash    = BCrypt.Net.BCrypt.HashPassword(req.NovaSenha);
        fotografo.AtualizadoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Senha alterada pelo próprio fotógrafo {Id}", fotografo.Id);
        return Ok(new { message = "Senha alterada com sucesso." });
    }

    /// <summary>256 bits de entropia, codificados em Base64 URL-safe.</summary>
    private static string GerarTokenSeguro()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return WebEncoders.Base64UrlEncode(bytes);
    }

    /// <summary>SHA-256 em hexadecimal minúsculo (64 caracteres).</summary>
    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private int FotografoId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private FotografoDto BuildDto(Fotografo f) => new()
    {
        Id         = f.Id,
        Nome       = f.Nome,
        Email      = f.Email,
        Telefone   = f.Telefone,
        Instagram  = f.Instagram,
        Bio        = f.Bio,
        FotoUrl    = f.FotoUrl,
        BannerPreset = f.BannerPreset,
        BannerUrl    = f.BannerUrl,
        PlanoAtivo = f.PlanoAtivo
    };

    private static readonly HashSet<string> PresetsValidos = ["flores", "ceu", "vintage", "estrelas"];

    private AuthResponse BuildResponse(Fotografo f)
    {
        var generated = _auth.GenerateToken(f);
        return new AuthResponse
        {
            Token     = generated.Token,
            ExpiresAt = generated.ExpiresAt,
            Fotografo = BuildDto(f)
        };
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var fotografo = await _db.Fotografos.FindAsync(FotografoId);
        if (fotografo is null) return NotFound();
        return Ok(BuildDto(fotografo));
    }

    [HttpPut("profile")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest req)
    {
        var fotografo = await _db.Fotografos.FindAsync(FotografoId);
        if (fotografo is null) return NotFound();

        fotografo.Nome      = string.IsNullOrWhiteSpace(req.Nome) ? fotografo.Nome : req.Nome.Trim();
        fotografo.Telefone  = string.IsNullOrWhiteSpace(req.Telefone) ? null : req.Telefone.Trim();
        fotografo.Instagram = string.IsNullOrWhiteSpace(req.Instagram) ? null : req.Instagram.Trim();
        fotografo.Bio       = string.IsNullOrWhiteSpace(req.Bio) ? null : req.Bio.Trim();
        fotografo.AtualizadoEm = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(BuildDto(fotografo));
    }

    // POST /api/auth/foto — foto de perfil salva no servidor (antes só ficava
    // no localStorage do navegador, então sumia ao trocar de aparelho).
    [HttpPost("foto")]
    [Authorize]
    public async Task<IActionResult> UploadFoto([FromForm] IFormFile arquivo)
    {
        var fotografo = await _db.Fotografos.FindAsync(FotografoId);
        if (fotografo is null) return NotFound();

        string url;
        try { url = await _upload.UploadAsync(arquivo, "perfil"); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }

        if (fotografo.FotoUrl is not null) _upload.Delete(fotografo.FotoUrl);

        fotografo.FotoUrl      = url;
        fotografo.AtualizadoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(BuildDto(fotografo));
    }

    // DELETE /api/auth/foto
    [HttpDelete("foto")]
    [Authorize]
    public async Task<IActionResult> DeleteFoto()
    {
        var fotografo = await _db.Fotografos.FindAsync(FotografoId);
        if (fotografo is null) return NotFound();

        if (fotografo.FotoUrl is not null)
        {
            _upload.Delete(fotografo.FotoUrl);
            fotografo.FotoUrl      = null;
            fotografo.AtualizadoEm = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return Ok(BuildDto(fotografo));
    }

    // PUT /api/auth/banner — escolhe um dos presets de gradiente do banner do
    // portfólio (limpa o banner customizado, se houver).
    [HttpPut("banner")]
    [Authorize]
    public async Task<IActionResult> AtualizarBanner([FromBody] AtualizarBannerRequest req)
    {
        if (!PresetsValidos.Contains(req.Preset))
            return BadRequest(new { message = "Preset de banner inválido." });

        var fotografo = await _db.Fotografos.FindAsync(FotografoId);
        if (fotografo is null) return NotFound();

        if (fotografo.BannerUrl is not null) _upload.Delete(fotografo.BannerUrl);

        fotografo.BannerPreset = req.Preset;
        fotografo.BannerUrl    = null;
        fotografo.AtualizadoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(BuildDto(fotografo));
    }

    // POST /api/auth/banner-imagem — banner customizado com imagem própria.
    [HttpPost("banner-imagem")]
    [Authorize]
    public async Task<IActionResult> UploadBannerImagem([FromForm] IFormFile arquivo)
    {
        var fotografo = await _db.Fotografos.FindAsync(FotografoId);
        if (fotografo is null) return NotFound();

        string url;
        try { url = await _upload.UploadAsync(arquivo, "banner"); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }

        if (fotografo.BannerUrl is not null) _upload.Delete(fotografo.BannerUrl);

        fotografo.BannerUrl    = url;
        fotografo.BannerPreset = "custom";
        fotografo.AtualizadoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(BuildDto(fotografo));
    }

    // DELETE /api/auth/me — exclusão de conta e de todos os dados (direito ao
    // esquecimento, LGPD art. 18). Some é irreversível: exige a senha atual.
    [HttpDelete("me")]
    [Authorize]
    public async Task<IActionResult> DeleteAccount([FromBody] DeleteAccountRequest req)
    {
        var fid = FotografoId;
        var fotografo = await _db.Fotografos.FindAsync(fid);
        if (fotografo is null) return NotFound();

        if (!BCrypt.Net.BCrypt.Verify(req.Senha, fotografo.SenhaHash))
            return Unauthorized(new { message = "Senha incorreta." });

        // Carrega tudo explicitamente e remove numa única transação: o
        // change tracker do EF ordena os DELETEs pelo grafo de dependências
        // (Pagamentos/Contratos/Imagens antes de Ensaios, Ensaios antes de
        // Clientes/Fotografo) — funciona tanto no Postgres real quanto no
        // provider InMemory dos testes, ao contrário de ExecuteDeleteAsync
        // (que o InMemory não suporta) ou de contar só com o cascade do
        // banco (FotografoId/ClienteId em Ensaio são NoAction).
        var ensaios    = await _db.Ensaios.Where(e => e.FotografoId == fid).ToListAsync();
        var ensaioIds  = ensaios.Select(e => e.Id).ToList();

        var contratos         = await _db.Contratos.Where(c => ensaioIds.Contains(c.EnsaioId)).ToListAsync();
        var pagamentos        = await _db.Pagamentos.Where(p => ensaioIds.Contains(p.EnsaioId)).ToListAsync();
        var imagensGaleria    = await _db.ImagensGaleria.Where(i => ensaioIds.Contains(i.EnsaioId)).ToListAsync();
        var clientes          = await _db.Clientes.Where(c => c.FotografoId == fid).ToListAsync();
        var disponibilidades  = await _db.Disponibilidades.Where(d => d.FotografoId == fid).ToListAsync();
        var imagensPortfolio  = await _db.ImagensPortfolio.Where(i => i.FotografoId == fid).ToListAsync();
        var tokensRecuperacao = await _db.TokensRecuperacaoSenha.Where(t => t.FotografoId == fid).ToListAsync();

        // URLs dos arquivos físicos: o banco só apaga as linhas, não os arquivos em disco.
        var urlsArquivos = imagensGaleria.Select(i => i.Url)
            .Concat(imagensPortfolio.Select(i => i.Url))
            .Concat(fotografo.FotoUrl is not null ? [fotografo.FotoUrl] : Enumerable.Empty<string>())
            .Concat(fotografo.BannerUrl is not null ? [fotografo.BannerUrl] : Enumerable.Empty<string>())
            .ToList();

        _db.Pagamentos.RemoveRange(pagamentos);
        _db.Contratos.RemoveRange(contratos);
        _db.ImagensGaleria.RemoveRange(imagensGaleria);
        _db.Ensaios.RemoveRange(ensaios);
        _db.Clientes.RemoveRange(clientes);
        _db.Disponibilidades.RemoveRange(disponibilidades);
        _db.ImagensPortfolio.RemoveRange(imagensPortfolio);
        _db.TokensRecuperacaoSenha.RemoveRange(tokensRecuperacao);
        _db.Fotografos.Remove(fotografo);

        await _db.SaveChangesAsync();

        foreach (var url in urlsArquivos) _upload.Delete(url);

        _logger.LogInformation("Conta excluída (LGPD) pelo próprio fotógrafo {Id}", fid);
        return NoContent();
    }
}
