using System.Text;
using System.Threading.RateLimiting;
using ClickManagerApi.Data;
using Microsoft.AspNetCore.RateLimiting;
using ClickManagerApi.Models;
using ClickManagerApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Atrás de proxy (Railway/Render/nginx), a conexão TCP chega do proxy, não do
// cliente real — sem isso, RemoteIpAddress é sempre o IP do proxy e o rate
// limit (que particiona por IP) vira um balde só compartilhado por todo mundo.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                             | ForwardedHeaders.XForwardedProto;
    // Em PaaS o IP do proxy varia e não é conhecido de antemão; limpar as
    // listas aceita o cabeçalho de qualquer origem. Se um dia isso rodar
    // atrás de um nginx próprio (IP fixo), restrinja com KnownProxies.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// EF Core + PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.EnableRetryOnFailure(3)
    ));

builder.Services.AddControllers()
    .AddJsonOptions(o => {
        o.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        o.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// CORS: origens vêm da configuração (Cors:AllowedOrigins).
// Em produção, defina o domínio real via appsettings.Production.json
// ou variável de ambiente Cors__AllowedOrigins__0.
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(opt =>
    opt.AddPolicy("FrontEnd", p =>
        p.WithOrigins(allowedOrigins)
         .AllowAnyHeader()
         .AllowAnyMethod()));

// JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

// Rate Limiting (proteção contra brute force e abuso)
// - Global: 100 requisições/minuto por IP
// - Política "auth": 10 tentativas/15min por IP (login, registro, reset de senha)
// Desativável via config (RateLimiting:Enabled=false) — usado nos testes.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            "{\"message\":\"Muitas requisições. Aguarde alguns instantes e tente novamente.\"}",
            cancellationToken);
    };

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window      = TimeSpan.FromMinutes(1),
                QueueLimit  = 0
            }));

    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window      = TimeSpan.FromMinutes(15),
                QueueLimit  = 0
            }));
});

// Services
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IFileUploadService, FileUploadService>();
builder.Services.AddHttpContextAccessor();

// Tratamento global de erros (ProblemDetails, RFC 7807)
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Health check real: um load balancer precisa saber se o banco está de pé,
// não só se o processo dotnet subiu (dotnet --info sempre "passa").
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>();

var app = builder.Build();

// Falha rápido em produção se segredos críticos ainda estiverem com os valores
// de exemplo do appsettings.json — esses valores estão no histórico do Git,
// então subir com eles ativos permitiria forjar tokens JWT ou expor a senha
// de exemplo do banco. Não roda em Development/Testing para não incomodar
// devs locais nem quebrar a WebApplicationFactory dos testes.
if (app.Environment.IsProduction())
{
    var jwtKey = app.Configuration["Jwt:Key"];
    if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32 ||
        jwtKey == "SUA_CHAVE_JWT_COM_PELO_MENOS_32_CARACTERES")
    {
        throw new InvalidOperationException(
            "Jwt:Key não está configurado com um valor seguro para produção. Defina a variável de " +
            "ambiente Jwt__Key com uma chave aleatória de pelo menos 32 caracteres " +
            "(gere com: openssl rand -base64 48).");
    }

    var connectionString = app.Configuration.GetConnectionString("DefaultConnection");
    if (connectionString is null || connectionString.Contains("your_db_password"))
    {
        throw new InvalidOperationException(
            "ConnectionStrings:DefaultConnection ainda usa a senha de exemplo do appsettings.json. " +
            "Defina ConnectionStrings__DefaultConnection com as credenciais reais do banco.");
    }
}

// Auto-migration (InMemory uses EnsureCreated; SQL Server uses Migrate)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        db.Database.EnsureCreated();
    else
        db.Database.Migrate();
}

// Primeiro middleware do pipeline: tudo depois (rate limit, HTTPS redirect,
// logs) precisa já enxergar o IP/scheme reais do cliente, não os do proxy.
app.UseForwardedHeaders();

// Tratamento global de exceções — primeiro do pipeline para capturar tudo
app.UseExceptionHandler();

// Security headers em todas as respostas
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";                    // impede MIME sniffing
    headers["X-Frame-Options"]        = "DENY";                       // impede clickjacking via iframe
    headers["Referrer-Policy"]        = "no-referrer";                // não vaza URLs em Referer
    headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
    headers["Permissions-Policy"]     = "camera=(), microphone=(), geolocation=()";
    await next();
});

// HTTPS obrigatório em produção (em dev a API roda em http://localhost:5000
// e o redirect quebraria o front local)
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors("FrontEnd");
app.UseStaticFiles();

if (app.Configuration.GetValue("RateLimiting:Enabled", true))
    app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Exposes Program to the test project (WebApplicationFactory<Program>)
public partial class Program { }
