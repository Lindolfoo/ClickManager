using System.Net;
using System.Net.Http.Json;
using ClickManagerApi.Tests.Helpers;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace ClickManagerApi.Tests.FunctionalTests;

public class PasswordResetTests(WebFactory factory) : IClassFixture<WebFactory>
{
    /// <summary>Registra um fotógrafo e devolve (email, senha).</summary>
    private async Task<(string Email, string Senha)> RegistrarAsync(HttpClient client)
    {
        var email = $"reset_{Guid.NewGuid():N}@test.com";
        var senha = "SenhaAntiga123";

        var resp = await client.PostAsJsonAsync("/api/auth/register",
            new { nome = "Reset User", email, senha });
        resp.EnsureSuccessStatusCode();

        return (email, senha);
    }

    /// <summary>Extrai o token do link capturado pelo FakeEmailService.</summary>
    private static string ExtrairToken(string email)
    {
        Assert.True(FakeEmailService.ResetLinks.TryGetValue(email, out var link),
            "Nenhum e-mail de redefinição foi enviado.");

        var query = QueryHelpers.ParseQuery(new Uri(link!).Query);
        Assert.True(query.TryGetValue("token", out var token), "Link não contém token.");

        var valor = token.ToString();
        Assert.False(string.IsNullOrWhiteSpace(valor), "Token vazio.");
        return valor;
    }

    [Fact]
    public async Task ForgotPassword_ComEmailExistente_EnviaLinkComToken()
    {
        var client = factory.CreateClient();
        var (email, _) = await RegistrarAsync(client);

        var resp = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(ExtrairToken(email)));
    }

    [Fact]
    public async Task ForgotPassword_ComEmailInexistente_Retorna200SemEnviarEmail()
    {
        var client = factory.CreateClient();
        var email  = $"naoexiste_{Guid.NewGuid():N}@test.com";

        var resp = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email });

        // 200 genérico evita enumeração de contas; nenhum e-mail deve sair
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.False(FakeEmailService.ResetLinks.ContainsKey(email));
    }

    [Fact]
    public async Task ResetPassword_ComTokenValido_AlteraSenhaEPermiteLogin()
    {
        var client = factory.CreateClient();
        var (email, senhaAntiga) = await RegistrarAsync(client);

        await client.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        var token = ExtrairToken(email);

        var novaSenha = "SenhaNova@2026";
        var reset = await client.PostAsJsonAsync("/api/auth/reset-password",
            new { token, novaSenha });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        // Nova senha funciona
        var loginNovo = await client.PostAsJsonAsync("/api/auth/login",
            new { email, senha = novaSenha });
        Assert.Equal(HttpStatusCode.OK, loginNovo.StatusCode);

        // Senha antiga deixa de funcionar
        var loginAntigo = await client.PostAsJsonAsync("/api/auth/login",
            new { email, senha = senhaAntiga });
        Assert.Equal(HttpStatusCode.Unauthorized, loginAntigo.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_ComTokenJaUsado_Retorna400()
    {
        var client = factory.CreateClient();
        var (email, _) = await RegistrarAsync(client);

        await client.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        var token = ExtrairToken(email);

        var primeiro = await client.PostAsJsonAsync("/api/auth/reset-password",
            new { token, novaSenha = "PrimeiraSenha123" });
        Assert.Equal(HttpStatusCode.OK, primeiro.StatusCode);

        // Uso único: a segunda tentativa com o mesmo token deve falhar
        var segundo = await client.PostAsJsonAsync("/api/auth/reset-password",
            new { token, novaSenha = "SegundaSenha123" });
        Assert.Equal(HttpStatusCode.BadRequest, segundo.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_ComTokenInexistente_Retorna400()
    {
        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/auth/reset-password",
            new { token = "token-que-nunca-existiu", novaSenha = "QualquerSenha123" });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_ComSenhaCurta_Retorna400()
    {
        var client = factory.CreateClient();
        var (email, _) = await RegistrarAsync(client);

        await client.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        var token = ExtrairToken(email);

        var resp = await client.PostAsJsonAsync("/api/auth/reset-password",
            new { token, novaSenha = "1234567" });   // 7 caracteres

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_SolicitadoDuasVezes_InvalidaTokenAnterior()
    {
        var client = factory.CreateClient();
        var (email, _) = await RegistrarAsync(client);

        await client.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        var tokenAntigo = ExtrairToken(email);

        await client.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        var tokenNovo = ExtrairToken(email);

        Assert.NotEqual(tokenAntigo, tokenNovo);

        // O token antigo foi invalidado ao gerar um novo
        var comAntigo = await client.PostAsJsonAsync("/api/auth/reset-password",
            new { token = tokenAntigo, novaSenha = "SenhaComAntigo123" });
        Assert.Equal(HttpStatusCode.BadRequest, comAntigo.StatusCode);

        // O token novo continua válido
        var comNovo = await client.PostAsJsonAsync("/api/auth/reset-password",
            new { token = tokenNovo, novaSenha = "SenhaComNovo123" });
        Assert.Equal(HttpStatusCode.OK, comNovo.StatusCode);
    }
}
