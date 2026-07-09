using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClickManagerApi.Tests.Helpers;
using Xunit;

namespace ClickManagerApi.Tests;

public class SecurityTests(WebFactory factory) : IClassFixture<WebFactory>
{
    private static readonly JsonSerializerOptions J = AuthHelper.JsonOpts;

    // ── 1. Endpoints protegidos retornam 401 sem token ──────────────

    [Theory]
    [InlineData("GET",   "/api/clientes")]
    [InlineData("GET",   "/api/ensaios")]
    [InlineData("GET",   "/api/contratos")]
    [InlineData("GET",   "/api/pagamentos")]
    [InlineData("GET",   "/api/galeria/1")]
    [InlineData("GET",   "/api/portfolio")]  // portfolio GET é AllowAnonymous → espera 200
    public async Task WithoutToken_ProtectedEndpoints_Return401Or200(string method, string path)
    {
        var req  = new HttpRequestMessage(new HttpMethod(method), path);
        var resp = await factory.CreateClient().SendAsync(req);

        // /api/portfolio GET é público (AllowAnonymous)
        if (path == "/api/portfolio")
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        else
            Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task MalformedJwt_Returns401()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "not.a.valid.jwt");
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/clientes")).StatusCode);
    }

    [Fact]
    public async Task RandomBase64AsToken_Returns401()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer",
                Convert.ToBase64String("fake.fake.fake"u8.ToArray()));
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/ensaios")).StatusCode);
    }

    [Fact]
    public async Task EmptyBearerToken_Returns401()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "");
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/contratos")).StatusCode);
    }

    // ── 2. Isolamento entre usuários ────────────────────────────────

    [Fact]
    public async Task CrossUser_CannotReadOthersCliente()
    {
        var (clientA, _, _) = await AuthHelper.CreateAuth(factory);
        var resp = await clientA.PostAsJsonAsync("/api/clientes", new
        {
            nome = "Segredo", email = $"sec_{Guid.NewGuid():N}@test.com", status = "Ativo"
        });
        var id = (await resp.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();

        var (clientB, _, _) = await AuthHelper.CreateAuth(factory);
        Assert.Equal(HttpStatusCode.NotFound,
            (await clientB.GetAsync($"/api/clientes/{id}")).StatusCode);
    }

    [Fact]
    public async Task CrossUser_CannotDeleteOthersEnsaio()
    {
        var (clientA, _, _) = await AuthHelper.CreateAuth(factory);
        var cResp = await clientA.PostAsJsonAsync("/api/clientes", new
            { nome = "X", email = $"cx_{Guid.NewGuid():N}@test.com", status = "Ativo" });
        var cid = (await cResp.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();
        var eResp = await clientA.PostAsJsonAsync("/api/ensaios", new
            { clienteId = cid, titulo = "Private", dataHora = "2026-09-01T10:00:00", local = "X", valor = 500 });
        var eid = (await eResp.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();

        var (clientB, _, _) = await AuthHelper.CreateAuth(factory);
        Assert.Equal(HttpStatusCode.NotFound,
            (await clientB.DeleteAsync($"/api/ensaios/{eid}")).StatusCode);
    }

    [Fact]
    public async Task CrossUser_CannotUploadToOthersGaleria()
    {
        var (clientA, _, _) = await AuthHelper.CreateAuth(factory);
        var cResp = await clientA.PostAsJsonAsync("/api/clientes", new
            { nome = "GX", email = $"gx_{Guid.NewGuid():N}@test.com", status = "Ativo" });
        var cid = (await cResp.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();
        var eResp = await clientA.PostAsJsonAsync("/api/ensaios", new
            { clienteId = cid, titulo = "GX Ensaio", dataHora = "2026-10-01T10:00:00", local = "Y", valor = 600 });
        var eid = (await eResp.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();

        var (clientB, _, _) = await AuthHelper.CreateAuth(factory);
        var form = new MultipartFormDataContent();
        var content = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xD9]);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(content, "arquivos", "hack.jpg");

        Assert.Equal(HttpStatusCode.NotFound,
            (await clientB.PostAsync($"/api/galeria/{eid}", form)).StatusCode);
    }

    // ── 3. Injeção / payloads maliciosos ────────────────────────────

    [Fact]
    public async Task SqlInjection_InQueryParam_DoesNotCrash()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);
        // EF Core parameteriza todas as queries → injeção impossível
        var resp = await client.GetAsync(
            "/api/ensaios?de='; DROP TABLE Ensaios; --&ate=2099-01-01");
        // Deve retornar 200 ou 400 — jamais 500
        Assert.NotEqual(HttpStatusCode.InternalServerError, resp.StatusCode);
    }

    [Fact]
    public async Task XssPayload_StoredAsLiteralText()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);
        var xss = "<script>alert('xss')</script>";

        var resp = await client.PostAsJsonAsync("/api/clientes", new
        {
            nome   = xss,
            email  = $"xss_{Guid.NewGuid():N}@test.com",
            status = "Ativo"
        });

        // API é um back-end JSON: armazena e devolve o texto literal (não HTML)
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(J);
        Assert.Equal(xss, body.GetProperty("nome").GetString());
    }

    [Fact]
    public async Task OverlyLargePayload_DoesNotCrash()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);
        var huge = new string('A', 100_000); // 100 KB de texto
        var resp = await client.PostAsJsonAsync("/api/clientes", new
        {
            nome   = huge,
            email  = $"big_{Guid.NewGuid():N}@test.com",
            status = "Ativo"
        });
        // [MaxLength(150)] → 400 (ou pode ser 413 se request size limit ativo)
        Assert.NotEqual(HttpStatusCode.InternalServerError, resp.StatusCode);
    }

    // ── 4. Endpoint público de health ────────────────────────────────

    [Fact]
    public async Task ContactHealth_Public_Returns200()
    {
        var resp = await factory.CreateClient().GetAsync("/api/contact/health");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
}
