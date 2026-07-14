using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClickManagerApi.Tests.Helpers;
using Xunit;

namespace ClickManagerApi.Tests.FunctionalTests;

public class PortfolioTests(WebFactory factory) : IClassFixture<WebFactory>
{
    private static readonly JsonSerializerOptions J = AuthHelper.JsonOpts;

    // Minimal valid JPEG (1×1 pixel white) — mesmo fixture usado em GaleriaTests.
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

    private static ByteArrayContent JpegContent()
    {
        var c = new ByteArrayContent(MinJpeg);
        c.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        return c;
    }

    private static MultipartFormDataContent UploadForm(string filename = "foto.jpg")
    {
        var form = new MultipartFormDataContent();
        form.Add(JpegContent(), "arquivo", filename);
        return form;
    }

    [Fact]
    public async Task GetAll_Public_Returns200SemAuth()
    {
        var (client, _, fid) = await AuthHelper.CreateAuth(factory);
        await client.PostAsync("/api/portfolio/upload", UploadForm());

        var anon = factory.CreateClient();
        var resp = await anon.GetAsync($"/api/portfolio?fotografoId={fid}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var list = await resp.Content.ReadFromJsonAsync<JsonElement>(J);
        Assert.Equal(1, list.GetArrayLength());
    }

    [Fact]
    public async Task Upload_ValidImage_Returns200EPersisteUrl()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);
        var resp = await client.PostAsync("/api/portfolio/upload", UploadForm());

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(J);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("url").GetString()));
        Assert.True(body.GetProperty("id").GetInt32() > 0);
    }

    [Fact]
    public async Task Upload_SemAuth_Returns401()
    {
        var resp = await factory.CreateClient().PostAsync("/api/portfolio/upload", UploadForm());
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Upload_MultiplasImagens_IncrementaOrdem()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);
        var r1 = await (await client.PostAsync("/api/portfolio/upload", UploadForm("a.jpg")))
            .Content.ReadFromJsonAsync<JsonElement>(J);
        var r2 = await (await client.PostAsync("/api/portfolio/upload", UploadForm("b.jpg")))
            .Content.ReadFromJsonAsync<JsonElement>(J);

        Assert.True(r2.GetProperty("ordem").GetInt32() > r1.GetProperty("ordem").GetInt32());
    }

    [Fact]
    public async Task Delete_ExistingImage_Returns204ENaoApareceMais()
    {
        var (client, _, fid) = await AuthHelper.CreateAuth(factory);
        var uploaded = await (await client.PostAsync("/api/portfolio/upload", UploadForm()))
            .Content.ReadFromJsonAsync<JsonElement>(J);
        var id = uploaded.GetProperty("id").GetInt32();

        var resp = await client.DeleteAsync($"/api/portfolio/{id}");
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        var list = await (await factory.CreateClient().GetAsync($"/api/portfolio?fotografoId={fid}"))
            .Content.ReadFromJsonAsync<JsonElement>(J);
        Assert.Equal(0, list.GetArrayLength());
    }

    [Fact]
    public async Task Delete_NonExistentImage_Returns404()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);
        var resp = await client.DeleteAsync("/api/portfolio/999999");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // Regression: um fotógrafo não pode apagar imagem de portfólio de outro (IDOR)
    [Fact]
    public async Task Delete_OtherUsersImage_Returns404ENaoApaga()
    {
        var (clientA, _, fidA) = await AuthHelper.CreateAuth(factory);
        var uploaded = await (await clientA.PostAsync("/api/portfolio/upload", UploadForm()))
            .Content.ReadFromJsonAsync<JsonElement>(J);
        var id = uploaded.GetProperty("id").GetInt32();

        var (clientB, _, _) = await AuthHelper.CreateAuth(factory);
        var resp = await clientB.DeleteAsync($"/api/portfolio/{id}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);

        var list = await (await factory.CreateClient().GetAsync($"/api/portfolio?fotografoId={fidA}"))
            .Content.ReadFromJsonAsync<JsonElement>(J);
        Assert.Equal(1, list.GetArrayLength());
    }

    [Fact]
    public async Task Reordenar_AtualizaOrdemDasImagens()
    {
        var (client, _, fid) = await AuthHelper.CreateAuth(factory);
        var r1 = await (await client.PostAsync("/api/portfolio/upload", UploadForm("a.jpg")))
            .Content.ReadFromJsonAsync<JsonElement>(J);
        var r2 = await (await client.PostAsync("/api/portfolio/upload", UploadForm("b.jpg")))
            .Content.ReadFromJsonAsync<JsonElement>(J);
        var id1 = r1.GetProperty("id").GetInt32();
        var id2 = r2.GetProperty("id").GetInt32();

        var resp = await client.PatchAsJsonAsync("/api/portfolio/reordenar", new object[]
        {
            new { id = id1, ordem = 9 },
            new { id = id2, ordem = 1 }
        });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var list = await (await client.GetAsync($"/api/portfolio?fotografoId={fid}"))
            .Content.ReadFromJsonAsync<JsonElement>(J);
        // A API ordena por Ordem asc: a imagem 2 (ordem 1) deve vir antes da imagem 1 (ordem 9)
        Assert.Equal(id2, list[0].GetProperty("id").GetInt32());
        Assert.Equal(id1, list[1].GetProperty("id").GetInt32());
    }

    // Regression: reordenar não pode alterar imagem de outro fotógrafo (IDOR)
    [Fact]
    public async Task Reordenar_IgnoraImagemDeOutroFotografo()
    {
        var (clientA, _, fidA) = await AuthHelper.CreateAuth(factory);
        var uploaded = await (await clientA.PostAsync("/api/portfolio/upload", UploadForm()))
            .Content.ReadFromJsonAsync<JsonElement>(J);
        var idA = uploaded.GetProperty("id").GetInt32();
        var ordemOriginal = uploaded.GetProperty("ordem").GetInt32();

        var (clientB, _, _) = await AuthHelper.CreateAuth(factory);
        var resp = await clientB.PatchAsJsonAsync("/api/portfolio/reordenar", new object[]
        {
            new { id = idA, ordem = 500 }
        });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var list = await (await factory.CreateClient().GetAsync($"/api/portfolio?fotografoId={fidA}"))
            .Content.ReadFromJsonAsync<JsonElement>(J);
        Assert.Equal(ordemOriginal, list[0].GetProperty("ordem").GetInt32());
    }
}
