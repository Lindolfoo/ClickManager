using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClickManagerApi.Tests.Helpers;
using Xunit;

namespace ClickManagerApi.Tests.FunctionalTests;

public class AuthTests(WebFactory factory) : IClassFixture<WebFactory>
{
    private static readonly JsonSerializerOptions J = AuthHelper.JsonOpts;

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
}
