using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClickManagerApi.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace ClickManagerApi.Tests;

/// <summary>
/// Mede latências e verifica que o sistema suporta carga básica.
/// Limites são conservadores (InMemory é mais rápido que SQL Server real).
/// </summary>
public class PerformanceTests(WebFactory factory, ITestOutputHelper out_) : IClassFixture<WebFactory>
{
    private static readonly JsonSerializerOptions J = AuthHelper.JsonOpts;

    [Fact]
    public async Task Login_CompletesUnder1000ms()
    {
        var email  = $"perf_{Guid.NewGuid():N}@test.com";
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register",
            new { nome = "Perf User", email, senha = "Perf@123" });

        var sw = Stopwatch.StartNew();
        var resp = await client.PostAsJsonAsync("/api/auth/login", new { email, senha = "Perf@123" });
        sw.Stop();

        out_.WriteLine($"[Login] {sw.ElapsedMilliseconds} ms");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.True(sw.ElapsedMilliseconds < 1000,
            $"Login demorou {sw.ElapsedMilliseconds}ms (limite: 1000ms)");
    }

    [Fact]
    public async Task GetClientes_With10Records_CompletesUnder500ms()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);
        for (var i = 0; i < 10; i++)
            await client.PostAsJsonAsync("/api/clientes", new
                { nome = $"Perf {i}", email = $"p{i}_{Guid.NewGuid():N}@test.com", status = "Ativo" });

        var sw = Stopwatch.StartNew();
        var resp = await client.GetAsync("/api/clientes");
        sw.Stop();

        out_.WriteLine($"[GET /clientes (10 records)] {sw.ElapsedMilliseconds} ms");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.True(sw.ElapsedMilliseconds < 500,
            $"GET /clientes demorou {sw.ElapsedMilliseconds}ms (limite: 500ms)");
    }

    [Fact]
    public async Task GetEnsaios_CompletesUnder500ms()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);

        var sw = Stopwatch.StartNew();
        var resp = await client.GetAsync("/api/ensaios");
        sw.Stop();

        out_.WriteLine($"[GET /ensaios] {sw.ElapsedMilliseconds} ms");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.True(sw.ElapsedMilliseconds < 500,
            $"GET /ensaios demorou {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task Sequential10ClienteCreations_CompletesUnder3000ms()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);
        var sw = Stopwatch.StartNew();

        for (var i = 0; i < 10; i++)
        {
            var resp = await client.PostAsJsonAsync("/api/clientes", new
            {
                nome   = $"Seq {i}",
                email  = $"seq{i}_{Guid.NewGuid():N}@test.com",
                status = "Ativo"
            });
            Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        }

        sw.Stop();
        out_.WriteLine($"[10 POSTs /clientes] {sw.ElapsedMilliseconds} ms");
        Assert.True(sw.ElapsedMilliseconds < 3000,
            $"10 criações demoraram {sw.ElapsedMilliseconds}ms (limite: 3000ms)");
    }

    [Fact]
    public async Task Concurrent10Registrations_AllSucceed()
    {
        var tasks = Enumerable.Range(0, 10).Select(async i =>
        {
            var email  = $"conc{i}_{Guid.NewGuid():N}@test.com";
            using var c = factory.CreateClient();
            return await c.PostAsJsonAsync("/api/auth/register",
                new { nome = $"Conc {i}", email, senha = "Conc@123" });
        }).ToArray();

        var sw      = Stopwatch.StartNew();
        var results = await Task.WhenAll(tasks);
        sw.Stop();

        var ok = results.Count(r => r.StatusCode == HttpStatusCode.Created);
        out_.WriteLine($"[10 registros concorrentes] {ok}/10 ok · {sw.ElapsedMilliseconds} ms");
        Assert.Equal(10, ok);
    }

    [Fact]
    public async Task Dashboard_CompletesUnder500ms()
    {
        var (client, _, _) = await AuthHelper.CreateAuth(factory);

        var sw = Stopwatch.StartNew();
        // O dashboard do frontend usa GET /api/ensaios e GET /api/clientes
        var t1 = client.GetAsync("/api/ensaios");
        var t2 = client.GetAsync("/api/clientes");
        await Task.WhenAll(t1, t2);
        sw.Stop();

        out_.WriteLine($"[Dashboard parallel fetch] {sw.ElapsedMilliseconds} ms");
        Assert.True(sw.ElapsedMilliseconds < 500,
            $"Dashboard fetch demorou {sw.ElapsedMilliseconds}ms (limite: 500ms)");
    }
}
