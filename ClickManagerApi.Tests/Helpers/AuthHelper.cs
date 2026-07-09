using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ClickManagerApi.Tests.Helpers;

public static class AuthHelper
{
    public static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Registers a new unique photographer and returns an authenticated HttpClient.
    /// Each call creates a fresh user, ensuring full test isolation.
    /// </summary>
    public static async Task<(HttpClient Client, string Token, int FotografoId)>
        CreateAuth(WebFactory factory, string? nome = null)
    {
        var email  = $"test_{Guid.NewGuid():N}@test.com";
        nome     ??= "Test Photographer";

        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/auth/register", new
        {
            nome,
            email,
            senha      = "Senha@Teste123",
            planoAtivo = "starter"
        });
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var token = body.GetProperty("token").GetString()!;
        var fid   = body.GetProperty("fotografo").GetProperty("id").GetInt32();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return (client, token, fid);
    }
}
