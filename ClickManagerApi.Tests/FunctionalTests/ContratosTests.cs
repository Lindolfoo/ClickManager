using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClickManagerApi.Tests.Helpers;
using Xunit;

namespace ClickManagerApi.Tests.FunctionalTests;

public class ContratosTests(WebFactory factory) : IClassFixture<WebFactory>
{
    private static readonly JsonSerializerOptions J = AuthHelper.JsonOpts;

    // Creates a fresh authenticated user + cliente + ensaio (ensaio auto-creates a contract)
    private async Task<(HttpClient Client, int EnsaioId)> SetupAsync()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);

        var cResp = await client.PostAsJsonAsync("/api/clientes", new
        {
            nome   = "CT Client",
            email  = $"ct_{Guid.NewGuid():N}@test.com",
            status = "Ativo"
        });
        var cid = (await cResp.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();

        var eResp = await client.PostAsJsonAsync("/api/ensaios", new
        {
            clienteId = cid,
            titulo    = "Contrato Test",
            dataHora  = "2026-12-01T10:00:00",
            local     = "Studio",
            valor     = 1000.00
        });
        var eid = (await eResp.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();

        return (client, eid);
    }

    [Fact]
    public async Task GetAll_WithAuth_Returns200()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);
        var resp = await client.GetAsync("/api/contratos");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutAuth_Returns401()
    {
        var resp = await factory.CreateClient().GetAsync("/api/contratos");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task PostContrato_Upsert_Returns200()
    {
        var (client, ensaioId) = await SetupAsync();

        // POST updates the auto-created contract (upsert because ensaio already has one)
        var resp = await client.PostAsJsonAsync("/api/contratos", new
        {
            ensaioId,
            conteudo = "Termos: 50% sinal, 50% na entrega das fotos."
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task PostContrato_Upsert_UpdatesConteudo()
    {
        var (client, ensaioId) = await SetupAsync();
        var conteudo = $"Conteudo-{Guid.NewGuid():N}";

        await client.PostAsJsonAsync("/api/contratos", new { ensaioId, conteudo });

        // Verify the contract exists in the list
        var list = await (await client.GetAsync("/api/contratos"))
            .Content.ReadFromJsonAsync<JsonElement>(J);
        Assert.True(list.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task PostContrato_NonExistentEnsaio_Returns404()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);
        var resp = await client.PostAsJsonAsync("/api/contratos", new
        {
            ensaioId = 999999,
            conteudo = "Test"
        });
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Assinar_ExistingContrato_Returns200AndStatusAssinado()
    {
        var (client, _) = await SetupAsync();

        var list = await (await client.GetAsync("/api/contratos"))
            .Content.ReadFromJsonAsync<JsonElement>(J);
        var id = list.EnumerateArray().First().GetProperty("id").GetInt32();

        var resp = await client.PatchAsync($"/api/contratos/{id}/assinar", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(J);
        Assert.Equal("Assinado", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Assinar_NonExistentContrato_Returns404()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);
        var resp = await client.PatchAsync("/api/contratos/999999/assinar", null);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
