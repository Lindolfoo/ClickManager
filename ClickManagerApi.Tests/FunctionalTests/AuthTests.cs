using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using ClickManagerApi.Tests.Helpers;
using Xunit;

namespace ClickManagerApi.Tests.FunctionalTests;

public class AuthTests(WebFactory factory) : IClassFixture<WebFactory>
{
    private static readonly JsonSerializerOptions J = AuthHelper.JsonOpts;

    private static Task<HttpResponseMessage> DeleteWithBodyAsync(HttpClient client, string url, object body)
    {
        var req = new HttpRequestMessage(HttpMethod.Delete, url) { Content = JsonContent.Create(body) };
        return client.SendAsync(req);
    }

    [Fact]
    public async Task Register_WithValidData_Returns201AndToken()
    {
        var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/register", new
        {
            nome  = "Foto Grapher",
            email = $"reg_{Guid.NewGuid():N}@test.com",
            senha = "SenhaSegura123"
        });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(J);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("token").GetString()));
        Assert.Equal("Foto Grapher", body.GetProperty("fotografo").GetProperty("nome").GetString());
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var email  = $"dup_{Guid.NewGuid():N}@test.com";
        var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/register", new { nome = "A", email, senha = "abc" });
        var resp2 = await client.PostAsJsonAsync("/api/auth/register", new { nome = "B", email, senha = "abc" });

        Assert.Equal(HttpStatusCode.Conflict, resp2.StatusCode);
    }

    [Fact]
    public async Task Login_WithValidCredentials_Returns200AndToken()
    {
        var email  = $"login_{Guid.NewGuid():N}@test.com";
        var senha  = "LoginSenha456";
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", new { nome = "Login User", email, senha });

        var resp = await client.PostAsJsonAsync("/api/auth/login", new { email, senha });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(J);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("token").GetString()));
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var email  = $"pw_{Guid.NewGuid():N}@test.com";
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", new { nome = "X", email, senha = "Correto123" });

        var resp = await client.PostAsJsonAsync("/api/auth/login", new { email, senha = "Errado456" });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Login_WithNonExistentEmail_Returns401()
    {
        var client = factory.CreateClient();
        var resp   = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = $"naoexiste_{Guid.NewGuid():N}@test.com",
            senha = "qualquer123"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Login_ResponseContainsFotografoFields()
    {
        var email  = $"fields_{Guid.NewGuid():N}@test.com";
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", new { nome = "Fields Test", email, senha = "Fields@123" });

        var resp = await client.PostAsJsonAsync("/api/auth/login", new { email, senha = "Fields@123" });
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(J);
        var fot  = body.GetProperty("fotografo");

        Assert.Equal(email,         fot.GetProperty("email").GetString());
        Assert.Equal("Fields Test", fot.GetProperty("nome").GetString());
        Assert.True(fot.GetProperty("id").GetInt32() > 0);
        Assert.False(string.IsNullOrWhiteSpace(fot.GetProperty("planoAtivo").GetString()));
    }

    [Fact]
    public async Task Register_WithPlano_ReturnsCorrectPlano()
    {
        var client = factory.CreateClient();
        var resp   = await client.PostAsJsonAsync("/api/auth/register", new
        {
            nome       = "Pro User",
            email      = $"pro_{Guid.NewGuid():N}@test.com",
            senha      = "Pro@123",
            planoAtivo = "pro"
        });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(J);
        Assert.Equal("pro", body.GetProperty("fotografo").GetProperty("planoAtivo").GetString());
    }

    // ── Exclusão de conta (LGPD) ────────────────────────────────

    [Fact]
    public async Task DeleteAccount_WithoutAuth_Returns401()
    {
        var client = factory.CreateClient();
        var resp = await DeleteWithBodyAsync(client, "/api/auth/me", new { senha = "qualquer" });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_WrongPassword_Returns401()
    {
        var email  = $"delwrong_{Guid.NewGuid():N}@test.com";
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", new { nome = "Del Wrong", email, senha = "SenhaCerta123" });
        var login = await (await client.PostAsJsonAsync("/api/auth/login", new { email, senha = "SenhaCerta123" }))
            .Content.ReadFromJsonAsync<JsonElement>(J);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", login.GetProperty("token").GetString());

        var resp = await DeleteWithBodyAsync(client, "/api/auth/me", new { senha = "SenhaErrada999" });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_CorrectPassword_Returns204EApagaConta()
    {
        var email  = $"delok_{Guid.NewGuid():N}@test.com";
        var senha  = "SenhaCerta123";
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", new { nome = "Del Ok", email, senha });
        var login = await (await client.PostAsJsonAsync("/api/auth/login", new { email, senha }))
            .Content.ReadFromJsonAsync<JsonElement>(J);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", login.GetProperty("token").GetString());

        var resp = await DeleteWithBodyAsync(client, "/api/auth/me", new { senha });
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        // A conta não existe mais: login com as mesmas credenciais falha
        var loginDepois = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, senha });
        Assert.Equal(HttpStatusCode.Unauthorized, loginDepois.StatusCode);

        // E-mail liberado: dá pra registrar de novo com o mesmo e-mail
        var registroNovo = await factory.CreateClient().PostAsJsonAsync("/api/auth/register", new { nome = "Outra Pessoa", email, senha });
        Assert.Equal(HttpStatusCode.Created, registroNovo.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_ApagaClientesEnsaiosEDadosRelacionados()
    {
        var email  = $"delcascade_{Guid.NewGuid():N}@test.com";
        var senha  = "SenhaCerta123";
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", new { nome = "Del Cascade", email, senha });
        var login = await (await client.PostAsJsonAsync("/api/auth/login", new { email, senha }))
            .Content.ReadFromJsonAsync<JsonElement>(J);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", login.GetProperty("token").GetString());

        var cResp = await client.PostAsJsonAsync("/api/clientes", new { nome = "Cliente Del", email = "cd@test.com", status = "Ativo" });
        var cid = (await cResp.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();
        await client.PostAsJsonAsync("/api/ensaios", new
        {
            clienteId = cid, titulo = "Ensaio Del", dataHora = "2026-09-01T10:00:00", local = "X", valor = 100
        });

        // Se a exclusão não der conta do FK NoAction entre Ensaio→Fotografo/Cliente,
        // isso falharia com erro de integridade referencial em vez de 204.
        var resp = await DeleteWithBodyAsync(client, "/api/auth/me", new { senha });
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }
}
