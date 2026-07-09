using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClickManagerApi.Tests.Helpers;
using Xunit;

namespace ClickManagerApi.Tests.FunctionalTests;

public class GaleriaTests(WebFactory factory) : IClassFixture<WebFactory>
{
    private static readonly JsonSerializerOptions J = AuthHelper.JsonOpts;

    // Minimal valid JPEG (1×1 pixel white)
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

    private static ByteArrayContent JpegContent(string filename = "test.jpg")
    {
        var c = new ByteArrayContent(MinJpeg);
        c.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        return c;
    }

    private async Task<(HttpClient Client, int EnsaioId)> SetupAsync()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);

        var cResp = await client.PostAsJsonAsync("/api/clientes", new
        {
            nome   = "Gal Client",
            email  = $"gal_{Guid.NewGuid():N}@test.com",
            status = "Ativo"
        });
        var cid = (await cResp.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();

        var eResp = await client.PostAsJsonAsync("/api/ensaios", new
        {
            clienteId = cid,
            titulo    = "Galeria Test",
            dataHora  = "2026-07-01T10:00:00",
            local     = "Studio",
            valor     = 1500.00
        });
        var eid = (await eResp.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();

        return (client, eid);
    }

    [Fact]
    public async Task GetAll_ForEnsaio_Returns200()
    {
        var (client, ensaioId) = await SetupAsync();
        var resp = await client.GetAsync($"/api/galeria/{ensaioId}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutAuth_Returns401()
    {
        var resp = await factory.CreateClient().GetAsync("/api/galeria/1");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Upload_SingleValidImage_Returns200WithRecord()
    {
        var (client, ensaioId) = await SetupAsync();

        var form = new MultipartFormDataContent();
        form.Add(JpegContent(), "arquivos", "photo.jpg");

        var resp = await client.PostAsync($"/api/galeria/{ensaioId}", form);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var list = await resp.Content.ReadFromJsonAsync<JsonElement>(J);
        Assert.Equal(1, list.GetArrayLength());
        Assert.Equal(ensaioId, list[0].GetProperty("ensaioId").GetInt32());
    }

    [Fact]
    public async Task Upload_MultipleImages_AllCreated()
    {
        var (client, ensaioId) = await SetupAsync();

        var form = new MultipartFormDataContent();
        for (var i = 0; i < 3; i++)
            form.Add(JpegContent($"img{i}.jpg"), "arquivos", $"img{i}.jpg");

        var resp = await client.PostAsync($"/api/galeria/{ensaioId}", form);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var list = await resp.Content.ReadFromJsonAsync<JsonElement>(J);
        Assert.Equal(3, list.GetArrayLength());
    }

    [Fact]
    public async Task Upload_IncrementsTotalImagens()
    {
        var (client, ensaioId) = await SetupAsync();

        var form = new MultipartFormDataContent();
        form.Add(JpegContent(), "arquivos", "a.jpg");
        await client.PostAsync($"/api/galeria/{ensaioId}", form);

        var ensaio = await (await client.GetAsync($"/api/ensaios/{ensaioId}"))
            .Content.ReadFromJsonAsync<JsonElement>(J);
        Assert.Equal(1, ensaio.GetProperty("totalImagens").GetInt32());
    }

    [Fact]
    public async Task Upload_ToNonExistentEnsaio_Returns404()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);

        var form = new MultipartFormDataContent();
        form.Add(JpegContent(), "arquivos", "x.jpg");

        var resp = await client.PostAsync("/api/galeria/999999", form);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Upload_ToOtherUsersEnsaio_Returns404()
    {
        var (clientA, ensaioId) = await SetupAsync();

        var (clientB, _, _) = await AuthHelper.CreateAuth(factory);
        var form = new MultipartFormDataContent();
        form.Add(JpegContent(), "arquivos", "hack.jpg");

        var resp = await clientB.PostAsync($"/api/galeria/{ensaioId}", form);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Upload_NoFiles_Returns400()
    {
        var (client, ensaioId) = await SetupAsync();

        var form = new MultipartFormDataContent();
        var resp = await client.PostAsync($"/api/galeria/{ensaioId}", form);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Delete_ExistingImage_Returns204()
    {
        var (client, ensaioId) = await SetupAsync();

        var form = new MultipartFormDataContent();
        form.Add(JpegContent(), "arquivos", "del.jpg");
        var uploaded = await (await client.PostAsync($"/api/galeria/{ensaioId}", form))
            .Content.ReadFromJsonAsync<JsonElement>(J);
        var imgId = uploaded[0].GetProperty("id").GetInt32();

        var resp = await client.DeleteAsync($"/api/galeria/{imgId}");
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    [Fact]
    public async Task Delete_NonExistentImage_Returns404()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);
        var resp = await client.DeleteAsync("/api/galeria/999999");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
