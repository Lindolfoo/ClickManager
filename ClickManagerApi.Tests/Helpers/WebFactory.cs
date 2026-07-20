using System.Collections.Concurrent;
using ClickManagerApi.Data;
using ClickManagerApi.Models;
using ClickManagerApi.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClickManagerApi.Tests.Helpers;

public class WebFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"TestDb_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"]                      = "test-key-32-chars-minimum-for-hmac-sha256-ok!",
                ["Jwt:Issuer"]                   = "ClickManagerTest",
                ["Jwt:Audience"]                 = "ClickManagerTestClient",
                ["Jwt:ExpirationHours"]          = "24",
                ["EmailSettings:SmtpHost"]       = "localhost",
                ["EmailSettings:SmtpPort"]       = "1025",
                ["EmailSettings:SenderName"]     = "Test",
                ["EmailSettings:SenderEmail"]    = "test@test.com",
                ["EmailSettings:SenderPassword"] = "test",
                ["EmailSettings:AdminEmail"]     = "admin@test.com",
                // Desativa rate limiting nos testes: os 50 testes criam dezenas
                // de usuários por execução e estourariam a política "auth"
                ["RateLimiting:Enabled"]         = "false",
                ["App:FrontendBaseUrl"]          = "http://localhost:5500"
            }));

        builder.ConfigureServices(services =>
        {
            // Swap SQL Server → InMemory (unique DB per factory instance)
            var dbDesc = services.SingleOrDefault(d =>
                d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (dbDesc is not null) services.Remove(dbDesc);
            services.AddDbContext<ApplicationDbContext>(opt =>
                opt.UseInMemoryDatabase(_dbName));

            // Swap real file upload (writes to disk) → no-op fake
            var uploadDesc = services.SingleOrDefault(d =>
                d.ServiceType == typeof(IFileUploadService));
            if (uploadDesc is not null) services.Remove(uploadDesc);
            services.AddScoped<IFileUploadService, FakeFileUploadService>();

            // Swap real email service (connects to SMTP) → no-op fake
            var emailDesc = services.SingleOrDefault(d =>
                d.ServiceType == typeof(IEmailService));
            if (emailDesc is not null) services.Remove(emailDesc);
            services.AddScoped<IEmailService, FakeEmailService>();
        });
    }
}

public class FakeFileUploadService : IFileUploadService
{
    private static readonly HashSet<string> Allowed = [".jpg", ".jpeg", ".png", ".webp", ".gif"];
    private const long MaxBytes = 10 * 1024 * 1024; // 10 MB

    public void Validate(IFormFile file)
    {
        if (file.Length > MaxBytes)
            throw new InvalidOperationException("Arquivo excede 10 MB.");
        if (!Allowed.Contains(Path.GetExtension(file.FileName).ToLowerInvariant()))
            throw new InvalidOperationException("Formato não permitido. Use jpg, png, webp ou gif.");
    }

    public Task<string> UploadAsync(IFormFile file, string subfolder)
    {
        Validate(file);
        return Task.FromResult(
            $"http://localhost/uploads/{subfolder}/{Guid.NewGuid()}{Path.GetExtension(file.FileName)}");
    }

    public void Delete(string url) { }
}

/// <summary>
/// Não envia e-mail: captura o link de redefinição em memória para que os
/// testes consigam extrair o token e concluir o fluxo ponta a ponta.
/// Indexado por e-mail (único por teste), então é seguro em execução paralela.
/// </summary>
public class FakeEmailService : IEmailService
{
    public static readonly ConcurrentDictionary<string, string> ResetLinks = new();

    public Task SendContactEmailsAsync(ContactRequest request) => Task.CompletedTask;

    public Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetLink)
    {
        ResetLinks[toEmail] = resetLink;
        return Task.CompletedTask;
    }
}
