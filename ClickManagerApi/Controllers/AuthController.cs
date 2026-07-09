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
    private readonly IConfiguration           _config;
    private readonly ILogger<AuthController>  _logger;

    public AuthController(
        ApplicationDbContext    db,
        IAuthService            auth,
        IEmailService           email,
        IConfiguration          config,
        ILogger<AuthController> logger)
    {
        _db     = db;
        _auth   = auth;
        _email  = email;
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
        PlanoAtivo = f.PlanoAtivo
    };

    private AuthResponse BuildResponse(Fotografo f) => new()
    {
        Token     = _auth.GenerateToken(f),
        ExpiresAt = DateTime.UtcNow.AddHours(24),
        Fotografo = BuildDto(f)
    };

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
}
