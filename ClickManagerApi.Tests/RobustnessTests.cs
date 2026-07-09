using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ClickManagerApi.Tests.Helpers;
using Xunit;

namespace ClickManagerApi.Tests;

/// <summary>
/// Testa comportamento com entradas inválidas, limites de campos e payloads malformados.
/// </summary>
public class RobustnessTests(WebFactory factory) : IClassFixture<WebFactory>
{
    private static readonly JsonSerializerOptions J = AuthHelper.JsonOpts;

    // ── Validação de campos ──────────────────────────────────────────

    [Fact]
    public async Task CreateCliente_NomeTooLong_Returns400()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);
        var resp = await client.PostAsJsonAsync("/api/clientes", new
        {
            nome   = new string('X', 300),  // MaxLength = 150
            email  = $"long_{Guid.NewGuid():N}@test.com",
            status = "Ativo"
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task CreateEnsaio_TituloTooLong_Returns400()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);
        var cResp = await client.PostAsJsonAsync("/api/clientes", new
            { nome = "C", email = $"t_{Guid.NewGuid():N}@test.com", status = "Ativo" });
        var cid = (await cResp.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();

        var resp = await client.PostAsJsonAsync("/api/ensaios", new
        {
            clienteId = cid,
            titulo    = new string('T', 300),  // MaxLength = 200
            dataHora  = "2026-10-01T10:00:00",
            local     = "X",
            valor     = 100
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task CreateCliente_EmailTooLong_Returns400()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);
        var resp = await client.PostAsJsonAsync("/api/clientes", new
        {
            nome   = "Valid Name",
            email  = new string('a', 250) + "@test.com",  // MaxLength = 200
            status = "Ativo"
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task CreateEnsaio_TituloMissing_Returns400()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);
        var cResp = await client.PostAsJsonAsync("/api/clientes", new
            { nome = "C2", email = $"c2_{Guid.NewGuid():N}@test.com", status = "Ativo" });
        var cid = (await cResp.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();

        // Envia titulo = null → [Required] → 400
        var json = $"{{\"clienteId\":{cid},\"titulo\":null,\"dataHora\":\"2026-10-01T10:00:00\",\"local\":\"X\",\"valor\":100}}";
        var resp = await client.PostAsync("/api/ensaios",
            new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ── Payloads malformados ────────────────────────────────────────

    [Fact]
    public async Task InvalidJson_Returns400()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);
        var resp = await client.PostAsync("/api/clientes",
            new StringContent("{ NOT VALID JSON }", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task EmptyBody_Returns400()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);
        var resp = await client.PostAsync("/api/clientes",
            new StringContent("", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task WrongContentType_Returns415()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);
        var resp = await client.PostAsync("/api/clientes",
            new StringContent("{\"nome\":\"X\"}", Encoding.UTF8, "text/plain"));
        // ASP.NET Core retorna 415 Unsupported Media Type
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, resp.StatusCode);
    }

    // ── Recursos inexistentes ──────────────────────────────────────

    [Fact]
    public async Task GetCliente_NonExistent_Returns404()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/api/clientes/999999")).StatusCode);
    }

    [Fact]
    public async Task DeleteCliente_NonExistent_Returns404()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.DeleteAsync("/api/clientes/999999")).StatusCode);
    }

    [Fact]
    public async Task GetEnsaio_NonExistent_Returns404()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/api/ensaios/999999")).StatusCode);
    }

    // ── Validação de contact ────────────────────────────────────────

    [Fact]
    public async Task Contact_InvalidEmail_Returns400()
    {
        var resp = await factory.CreateClient().PostAsJsonAsync("/api/contact", new
        {
            name  = "Usuário Teste",
            email = "nao-e-email",
            plan  = "starter"
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Contact_NameTooShort_Returns400()
    {
        var resp = await factory.CreateClient().PostAsJsonAsync("/api/contact", new
        {
            name  = "AB",  // < 3 chars
            email = "ok@test.com",
            plan  = "starter"
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Contact_MissingPlan_Returns400()
    {
        var resp = await factory.CreateClient().PostAsJsonAsync("/api/contact", new
        {
            name  = "Nome Valido",
            email = "valido@test.com",
            plan  = ""
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ── Valores extremos ────────────────────────────────────────────

    [Fact]
    public async Task CreatePagamento_ZeroValor_DoesNotCrash()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);
        // ensaioId inválido → 400 ou 404/500; o importante é não crashar
        var resp = await client.PostAsJsonAsync("/api/pagamentos", new
        {
            ensaioId = 0, valor = 0m, tipo = "Total", metodo = "Pix", status = "Pendente"
        });
        Assert.NotEqual(HttpStatusCode.InternalServerError, resp.StatusCode);
    }

    [Fact]
    public async Task DateFilter_InvalidDates_DoesNotCrash()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);
        var resp = await client.GetAsync("/api/ensaios?de=nao-e-data&ate=tampouco");
        Assert.NotEqual(HttpStatusCode.InternalServerError, resp.StatusCode);
    }
}
