using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClickManagerApi.Tests.Helpers;
using Xunit;

namespace ClickManagerApi.Tests.FunctionalTests;

public class ContactTests(WebFactory factory) : IClassFixture<WebFactory>
{
    private static readonly JsonSerializerOptions J = AuthHelper.JsonOpts;

    [Fact]
    public async Task Post_ValidRequest_Returns200EMensagemDeConfirmacao()
    {
        var email = $"lead_{Guid.NewGuid():N}@test.com";
        var resp = await factory.CreateClient().PostAsJsonAsync("/api/contact", new
        {
            name  = "Fotógrafo Interessado",
            email,
            phone = "(11) 91234-5678",
            plan  = "starter",
            message = "Quero saber mais sobre o plano."
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(J);
        Assert.True(body.GetProperty("success").GetBoolean());
        Assert.Contains(email, body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Post_SemTelefoneNemMensagem_AindaAssimReturna200()
    {
        // Phone e Message são opcionais no DTO — só Name/Email/Plan são obrigatórios.
        var resp = await factory.CreateClient().PostAsJsonAsync("/api/contact", new
        {
            name  = "Sem Telefone",
            email = $"lead_{Guid.NewGuid():N}@test.com",
            plan  = "pro"
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Health_Returns200EStatusOnline()
    {
        var resp = await factory.CreateClient().GetAsync("/api/contact/health");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(J);
        Assert.Equal("online", body.GetProperty("status").GetString());
    }
}
