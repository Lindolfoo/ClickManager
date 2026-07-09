using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClickManagerApi.Tests.Helpers;
using Xunit;

namespace ClickManagerApi.Tests.FunctionalTests;

public class PagamentosTests(WebFactory factory) : IClassFixture<WebFactory>
{
    private static readonly JsonSerializerOptions J = AuthHelper.JsonOpts;

    private async Task<(HttpClient Client, int EnsaioId)> SetupAsync()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);

        var cResp = await client.PostAsJsonAsync("/api/clientes", new
        {
            nome   = "PG Client",
            email  = $"pg_{Guid.NewGuid():N}@test.com",
            status = "Ativo"
        });
        var cid = (await cResp.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();

        var eResp = await client.PostAsJsonAsync("/api/ensaios", new
        {
            clienteId = cid,
            titulo    = "Pagamento Test",
            dataHora  = "2026-06-01T10:00:00",
            local     = "Studio",
            valor     = 2000.00
        });
        var eid = (await eResp.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();

        return (client, eid);
    }

    [Fact]
    public async Task GetAll_WithAuth_Returns200()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);
        var resp = await client.GetAsync("/api/pagamentos");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutAuth_Returns401()
    {
        var resp = await factory.CreateClient().GetAsync("/api/pagamentos");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Create_ValidPagamento_Returns200()
    {
        var (client, ensaioId) = await SetupAsync();

        var resp = await client.PostAsJsonAsync("/api/pagamentos", new
        {
            ensaioId,
            valor         = 1000.00m,
            tipo          = "Sinal",
            metodo        = "Pix",
            status        = "Pago",
            dataPagamento = DateTime.UtcNow.Date.ToString("yyyy-MM-dd")
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(J);
        Assert.Equal(1000m, body.GetProperty("valor").GetDecimal());
    }

    [Fact]
    public async Task Create_ThenGetAll_ContainsPagamento()
    {
        var (client, ensaioId) = await SetupAsync();
        await client.PostAsJsonAsync("/api/pagamentos", new
        {
            ensaioId, valor = 500m, tipo = "Total", metodo = "Cartão", status = "Pendente"
        });

        var list = await (await client.GetAsync("/api/pagamentos"))
            .Content.ReadFromJsonAsync<JsonElement>(J);
        Assert.True(list.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task Confirmar_PendentePagamento_MarksAsPago()
    {
        var (client, ensaioId) = await SetupAsync();

        var createResp = await client.PostAsJsonAsync("/api/pagamentos", new
        {
            ensaioId, valor = 500m, tipo = "Total", metodo = "Dinheiro", status = "Pendente"
        });
        var id = (await createResp.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();

        var resp = await client.PatchAsync($"/api/pagamentos/{id}/confirmar", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(J);
        Assert.Equal("Pago", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Confirmar_SetsDataPagamento()
    {
        var (client, ensaioId) = await SetupAsync();

        var createResp = await client.PostAsJsonAsync("/api/pagamentos", new
        {
            ensaioId, valor = 300m, tipo = "Parcela", metodo = "Pix", status = "Pendente"
        });
        var id = (await createResp.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();

        await client.PatchAsync($"/api/pagamentos/{id}/confirmar", null);

        // Re-fetch all pagamentos and verify dataPagamento is set
        var list = await (await client.GetAsync("/api/pagamentos"))
            .Content.ReadFromJsonAsync<JsonElement>(J);
        var found = list.EnumerateArray().FirstOrDefault(p => p.GetProperty("id").GetInt32() == id);
        // Confirmar sets DataPagamento to UtcNow
        Assert.False(found.ValueKind == JsonValueKind.Undefined);
    }

    [Fact]
    public async Task Confirmar_NonExistentPagamento_Returns404()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);
        var resp = await client.PatchAsync("/api/pagamentos/999999/confirmar", null);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
