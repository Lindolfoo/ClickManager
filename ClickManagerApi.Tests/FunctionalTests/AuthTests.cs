using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClickManagerApi.Tests.Helpers;
using Xunit;

namespace ClickManagerApi.Tests.FunctionalTests;

public class AuthTests(WebFactory factory) : IClassFixture<WebFactory>
{
    private static readonly JsonSerializerOptions J = AuthHelper.JsonOpts;

    // Minimal valid JPEG (1×1 pixel white) — same fixture used in GaleriaTests.
    private static readonly byte[] MinJpeg =
    [
        0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01,
        0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0xFF, 0xDB, 0x00, 0x43,
        0x00, 0x08, 0x06, 0x06, 0x07, 0x06, 0x05, 0x08, 0x07, 0x07, 0x07, 0x09,
        0x09, 0x08, 0x0A, 0x0C, 0x14, 0x0D, 0x0C, 0x0B, 0x0B, 0x0C, 0x19, 0x12,
        0x13, 0x0F, 0x14, 0x1D, 0x1A, 0x1F, 0x1E, 0x1D, 0x1A, 0x1C, 0x1C, 0x20,
        0x24, 0x2E, 0x27, 0x20, 0x22, 0x2C, 0x23, 0x1C, 0x1C, 0x28, 0x37, 0x29,
        0x2C, 0x30, 0x31, 0x34, 0x34, 0x34, 0x1F, 0x27, 0x39, 0x3D, 0x38, 0x32,
        0x3C, 0x2E, 0x33, 0x34, 0x32, 0xFF, 0xC0, 0x00, 0x0B, 0x08, 0x00, 0x01,
        0x00, 0x01, 0x01, 0x01, 0x11, 0x00, 0xFF, 0xC4, 0x00, 0x1F, 0x00, 0x00,
        0x01, 0x05, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
        0x09, 0x0A, 0x0B, 0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3F,
        0x00, 0xFB, 0xDE, 0xCA, 0xC6, 0x80, 0xFF, 0xD9
    ];

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

    // ── Foto de perfil ───────────────────────────────────────────

    [Fact]
    public async Task UploadFoto_Valida_RetornaFotoUrl()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);

        var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(MinJpeg);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(content, "arquivo", "foto.jpg");

        var resp = await client.PostAsync("/api/auth/foto", form);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(J);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("fotoUrl").GetString()));
    }

    [Fact]
    public async Task UploadFoto_SemAuth_Returns401()
    {
        var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(MinJpeg);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(content, "arquivo", "foto.jpg");

        var resp = await factory.CreateClient().PostAsync("/api/auth/foto", form);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteFoto_ApagaFotoUrl()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);

        var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(MinJpeg);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(content, "arquivo", "foto.jpg");
        await client.PostAsync("/api/auth/foto", form);

        var resp = await client.DeleteAsync("/api/auth/foto");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        // fotoUrl nulo é omitido do JSON (DefaultIgnoreCondition.WhenWritingNull) — a
        // ausência da propriedade é o próprio sinal de que a foto foi removida.
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(J);
        Assert.False(body.TryGetProperty("fotoUrl", out _));
    }

    [Fact]
    public async Task Login_ThenUploadFoto_FotoUrlApareceNoLoginSeguinte()
    {
        var email  = $"foto_{Guid.NewGuid():N}@test.com";
        var senha  = "Foto@123";
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", new { nome = "Foto Field", email, senha });
        var login1 = await (await client.PostAsJsonAsync("/api/auth/login", new { email, senha }))
            .Content.ReadFromJsonAsync<JsonElement>(J);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login1.GetProperty("token").GetString());

        var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(MinJpeg);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(content, "arquivo", "foto.jpg");
        await client.PostAsync("/api/auth/foto", form);

        var login2 = await (await client.PostAsJsonAsync("/api/auth/login", new { email, senha }))
            .Content.ReadFromJsonAsync<JsonElement>(J);
        Assert.False(string.IsNullOrWhiteSpace(login2.GetProperty("fotografo").GetProperty("fotoUrl").GetString()));
    }

    // ── Troca de senha ─────────────────────────────────────────

    [Fact]
    public async Task AlterarSenha_SemAuth_Returns401()
    {
        var resp = await factory.CreateClient().PutAsJsonAsync("/api/auth/senha",
            new { senhaAtual = "qualquer", novaSenha = "NovaSenha123" });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task AlterarSenha_SenhaAtualErrada_Returns401ENaoAltera()
    {
        var email  = $"senhaerrada_{Guid.NewGuid():N}@test.com";
        var senha  = "SenhaCerta123";
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", new { nome = "Senha Errada", email, senha });
        var login = await (await client.PostAsJsonAsync("/api/auth/login", new { email, senha }))
            .Content.ReadFromJsonAsync<JsonElement>(J);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.GetProperty("token").GetString());

        var resp = await client.PutAsJsonAsync("/api/auth/senha",
            new { senhaAtual = "SenhaTotalmenteErrada", novaSenha = "NovaSenha123" });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);

        // Login com a senha original ainda funciona: nada foi alterado.
        var loginDepois = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, senha });
        Assert.Equal(HttpStatusCode.OK, loginDepois.StatusCode);
    }

    [Fact]
    public async Task AlterarSenha_NovaSenhaCurta_Returns400()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);
        var resp = await client.PutAsJsonAsync("/api/auth/senha",
            new { senhaAtual = "Senha@Teste123", novaSenha = "curta" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task AlterarSenha_Correta_TrocaEPermiteLoginComNova()
    {
        var email  = $"senhaok_{Guid.NewGuid():N}@test.com";
        var senha  = "SenhaCerta123";
        var novaSenha = "SenhaNova456";
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", new { nome = "Senha Ok", email, senha });
        var login = await (await client.PostAsJsonAsync("/api/auth/login", new { email, senha }))
            .Content.ReadFromJsonAsync<JsonElement>(J);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.GetProperty("token").GetString());

        var resp = await client.PutAsJsonAsync("/api/auth/senha", new { senhaAtual = senha, novaSenha });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        // Senha antiga não funciona mais, a nova sim.
        var loginAntiga = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, senha });
        Assert.Equal(HttpStatusCode.Unauthorized, loginAntiga.StatusCode);

        var loginNova = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, senha = novaSenha });
        Assert.Equal(HttpStatusCode.OK, loginNova.StatusCode);
    }

    // ── Banner do portfólio ────────────────────────────────────

    [Fact]
    public async Task AtualizarBanner_PresetValido_RetornaAtualizado()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);
        var resp = await client.PutAsJsonAsync("/api/auth/banner", new { preset = "ceu" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(J);
        Assert.Equal("ceu", body.GetProperty("bannerPreset").GetString());
        Assert.False(body.TryGetProperty("bannerUrl", out var bu) && bu.ValueKind != JsonValueKind.Null);
    }

    [Fact]
    public async Task AtualizarBanner_PresetInvalido_Returns400()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);
        var resp = await client.PutAsJsonAsync("/api/auth/banner", new { preset = "arco-iris" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task AtualizarBanner_SemAuth_Returns401()
    {
        var resp = await factory.CreateClient().PutAsJsonAsync("/api/auth/banner", new { preset = "ceu" });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task UploadBannerImagem_Valida_RetornaBannerUrlESetaPresetCustom()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);

        var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(MinJpeg);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(content, "arquivo", "banner.jpg");

        var resp = await client.PostAsync("/api/auth/banner-imagem", form);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(J);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("bannerUrl").GetString()));
        Assert.Equal("custom", body.GetProperty("bannerPreset").GetString());
    }

    [Fact]
    public async Task AtualizarBanner_AposUploadImagem_LimpaBannerUrl()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);

        var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(MinJpeg);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(content, "arquivo", "banner.jpg");
        await client.PostAsync("/api/auth/banner-imagem", form);

        var resp = await client.PutAsJsonAsync("/api/auth/banner", new { preset = "vintage" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(J);
        Assert.Equal("vintage", body.GetProperty("bannerPreset").GetString());
        Assert.False(body.TryGetProperty("bannerUrl", out var bu) && bu.ValueKind != JsonValueKind.Null);
    }
}
