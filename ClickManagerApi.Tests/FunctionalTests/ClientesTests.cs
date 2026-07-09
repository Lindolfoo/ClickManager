using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClickManagerApi.Tests.Helpers;
using Xunit;

namespace ClickManagerApi.Tests.FunctionalTests;

public class ClientesTests(WebFactory factory) : IClassFixture<WebFactory>
{
    private static readonly JsonSerializerOptions J = AuthHelper.JsonOpts;

    private static object NewCliente(string nome = "Maria Silva") => new
    {
        nome,
        email      = $"c_{Guid.NewGuid():N}@test.com",
        tipoEnsaio = "Casamento",
        status     = "Ativo"
    };

    [Fact]
    public async Task GetAll_WithAuth_Returns200()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);
        var resp = await client.GetAsync("/api/clientes");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutAuth_Returns401()
    {
        var resp = await factory.CreateClient().GetAsync("/api/clientes");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Create_ValidCliente_Returns201WithCorrectData()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);
        var resp = await client.PostAsJsonAsync("/api/clientes", NewCliente("Maria Silva"));

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(J);
        Assert.Equal("Maria Silva", body.GetProperty("nome").GetString());
        Assert.True(body.GetProperty("id").GetInt32() > 0);
    }

    [Fact]
    public async Task GetAll_AfterCreate_ContainsCliente()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);
        var nome = $"Carlos_{Guid.NewGuid():N}";
        await client.PostAsJsonAsync("/api/clientes", NewCliente(nome));

        var resp = await client.GetAsync("/api/clientes");
        var list = await resp.Content.ReadFromJsonAsync<JsonElement>(J);

        Assert.Contains(list.EnumerateArray(),
            c => c.GetProperty("nome").GetString() == nome);
    }

    [Fact]
    public async Task GetById_ExistingCliente_Returns200()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);
        var createResp = await client.PostAsJsonAsync("/api/clientes", NewCliente());
        var created    = await createResp.Content.ReadFromJsonAsync<JsonElement>(J);
        var id         = created.GetProperty("id").GetInt32();

        var resp = await client.GetAsync($"/api/clientes/{id}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task GetById_NonExistent_Returns404()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);
        var resp = await client.GetAsync("/api/clientes/999999");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Update_ExistingCliente_Returns200WithNewData()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);
        var createResp = await client.PostAsJsonAsync("/api/clientes", NewCliente("Original"));
        var id         = (await createResp.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();

        var resp = await client.PutAsJsonAsync($"/api/clientes/{id}", new
        {
            nome = "Atualizado", email = "upd@test.com", status = "Ativo", tipoEnsaio = "Newborn"
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(J);
        Assert.Equal("Atualizado", body.GetProperty("nome").GetString());
    }

    [Fact]
    public async Task Delete_ExistingCliente_Returns204()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);
        var createResp = await client.PostAsJsonAsync("/api/clientes", NewCliente("Delete Me"));
        var id         = (await createResp.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();

        var resp = await client.DeleteAsync($"/api/clientes/{id}");
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    [Fact]
    public async Task Delete_ThenGetById_Returns404()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);
        var createResp = await client.PostAsJsonAsync("/api/clientes", NewCliente());
        var id         = (await createResp.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();

        await client.DeleteAsync($"/api/clientes/{id}");
        var resp = await client.GetAsync($"/api/clientes/{id}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Update_OtherUsersCliente_Returns404()
    {
        // User A creates a client
        var (clientA, _, _) = await AuthHelper.CreateAuth(factory);
        var createResp      = await clientA.PostAsJsonAsync("/api/clientes", NewCliente("Private"));
        var id              = (await createResp.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();

        // User B tries to update it → 404 (not their data)
        var (clientB, _, _) = await AuthHelper.CreateAuth(factory);
        var resp            = await clientB.PutAsJsonAsync($"/api/clientes/{id}", new { nome = "Hacked", status = "Ativo" });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
