using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClickManagerApi.Tests.Helpers;
using Xunit;

namespace ClickManagerApi.Tests.FunctionalTests;

public class EnsaiosTests(WebFactory factory) : IClassFixture<WebFactory>
{
    private static readonly JsonSerializerOptions J = AuthHelper.JsonOpts;

    private async Task<(HttpClient Client, int ClienteId)> SetupAsync()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);
        var resp = await client.PostAsJsonAsync("/api/clientes", new
        {
            nome   = "Ensaio Client",
            email  = $"ec_{Guid.NewGuid():N}@test.com",
            status = "Ativo"
        });
        var cid = (await resp.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();
        return (client, cid);
    }

    [Fact]
    public async Task GetAll_WithAuth_Returns200()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);
        var resp = await client.GetAsync("/api/ensaios");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutAuth_Returns401()
    {
        var resp = await factory.CreateClient().GetAsync("/api/ensaios");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Create_ValidEnsaio_Returns201()
    {
        var (client, cid) = await SetupAsync();
        var resp = await client.PostAsJsonAsync("/api/ensaios", new
        {
            clienteId = cid,
            titulo    = "Ensaio de Casamento",
            dataHora  = "2026-08-15T14:00:00",
            local     = "Parque do Ibirapuera",
            valor     = 1500.00
        });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(J);
        Assert.Equal("Ensaio de Casamento", body.GetProperty("titulo").GetString());
    }

    [Fact]
    public async Task Create_AutoCreatesContract()
    {
        var (client, cid) = await SetupAsync();
        await client.PostAsJsonAsync("/api/ensaios", new
        {
            clienteId = cid,
            titulo    = "Auto Contract Test",
            dataHora  = "2026-09-01T10:00:00",
            local     = "Studio",
            valor     = 800.00
        });

        var contratos = await (await client.GetAsync("/api/contratos"))
            .Content.ReadFromJsonAsync<JsonElement>(J);
        Assert.True(contratos.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task DateFilter_ReturnsOnlyMatchingEnsaios()
    {
        var (client, cid) = await SetupAsync();
        await client.PostAsJsonAsync("/api/ensaios", new { clienteId = cid, titulo = "Jan", dataHora = "2026-01-15T10:00:00", local = "A", valor = 100 });
        await client.PostAsJsonAsync("/api/ensaios", new { clienteId = cid, titulo = "Jul", dataHora = "2026-07-15T10:00:00", local = "B", valor = 100 });

        var resp = await client.GetAsync("/api/ensaios?de=2026-06-01&ate=2026-12-31");
        var list = await resp.Content.ReadFromJsonAsync<JsonElement>(J);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.True(list.GetArrayLength() >= 1);
        Assert.All(list.EnumerateArray(), e =>
        {
            var d = DateTime.Parse(e.GetProperty("dataHora").GetString()!);
            Assert.True(d >= new DateTime(2026, 6, 1));
        });
    }

    [Fact]
    public async Task Update_ExistingEnsaio_Returns200()
    {
        var (client, cid) = await SetupAsync();
        var createResp    = await client.PostAsJsonAsync("/api/ensaios", new
        {
            clienteId = cid, titulo = "Original", dataHora = "2026-10-01T10:00:00", local = "Aqui", valor = 500
        });
        var id = (await createResp.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();

        var resp = await client.PutAsJsonAsync($"/api/ensaios/{id}", new
        {
            clienteId = cid, titulo = "Atualizado", dataHora = "2026-10-02T10:00:00", local = "Ali", valor = 600
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(J);
        Assert.Equal("Atualizado", body.GetProperty("titulo").GetString());
    }

    [Fact]
    public async Task Delete_ExistingEnsaio_Returns204()
    {
        var (client, cid) = await SetupAsync();
        var createResp    = await client.PostAsJsonAsync("/api/ensaios", new
        {
            clienteId = cid, titulo = "Delete", dataHora = "2026-11-01T10:00:00", local = "X", valor = 200
        });
        var id = (await createResp.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();

        var resp = await client.DeleteAsync($"/api/ensaios/{id}");
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    [Fact]
    public async Task Delete_OtherUsersEnsaio_Returns404()
    {
        var (clientA, cidA) = await SetupAsync();
        var createResp      = await clientA.PostAsJsonAsync("/api/ensaios", new
        {
            clienteId = cidA, titulo = "Cross", dataHora = "2026-12-01T10:00:00", local = "Y", valor = 300
        });
        var id = (await createResp.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();

        var (clientB, _, _) = await AuthHelper.CreateAuth(factory);
        var resp            = await clientB.DeleteAsync($"/api/ensaios/{id}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task GetById_ExistingEnsaio_Returns200()
    {
        var (client, cid) = await SetupAsync();
        var createResp    = await client.PostAsJsonAsync("/api/ensaios", new
        {
            clienteId = cid, titulo = "GetById", dataHora = "2026-08-01T10:00:00", local = "Z", valor = 400
        });
        var id   = (await createResp.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();
        var resp = await client.GetAsync($"/api/ensaios/{id}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
}
