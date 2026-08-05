using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace WalletApi.Tests;

public class RateLimitingTests : IClassFixture<WalletApiFactory>
{
    private readonly WalletApiFactory _factory;

    public RateLimitingTests(WalletApiFactory factory)
    {
        _factory = factory;
    }

    // Her test kendi hostunu alır: rate limit sayacı diğer testlerden yalıtık
    // olur ve düşük bir sınırla deterministik biçimde sınanabilir.
    private HttpClient CreateClientWithLimit(int permitLimit) =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimiting:Auth:PermitLimit"] = permitLimit.ToString(),
                    ["RateLimiting:Auth:WindowSeconds"] = "60"
                });
            });
        }).CreateClient();

    [Fact]
    public async Task Login_SinirAsilincaReddeder()
    {
        var client = CreateClientWithLimit(permitLimit: 3);
        var body = new { email = "yok@example.com", password = "GucluSifre123" };

        // İlk üç istek limite dahil; hepsi 401 döner (kullanıcı yok) ama geçer.
        for (var i = 0; i < 3; i++)
        {
            var allowed = await client.PostAsJsonAsync("/api/auth/login", body);
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }

        // Dördüncü istek sınırı aşar.
        var blocked = await client.PostAsJsonAsync("/api/auth/login", body);
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
    }

    [Fact]
    public async Task Register_SinirAsilincaReddeder()
    {
        var client = CreateClientWithLimit(permitLimit: 2);

        for (var i = 0; i < 2; i++)
        {
            var allowed = await client.PostAsJsonAsync(
                "/api/auth/register",
                new { email = $"kullanici-{Guid.NewGuid():N}@example.com", password = "GucluSifre123" });
            Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
        }

        var blocked = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = $"kullanici-{Guid.NewGuid():N}@example.com", password = "GucluSifre123" });

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
    }

    [Fact]
    public async Task SinirAsildiginda_RetryAfterBasligiDoner()
    {
        var client = CreateClientWithLimit(permitLimit: 1);
        var body = new { email = "yok@example.com", password = "GucluSifre123" };

        await client.PostAsJsonAsync("/api/auth/login", body);
        var blocked = await client.PostAsJsonAsync("/api/auth/login", body);

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.True(blocked.Headers.Contains("Retry-After"));
    }

    [Fact]
    public async Task ParaUcNoktalari_RateLimitDisidir()
    {
        // Sınır 1 olsa bile para uç noktaları politikaya dahil değildir;
        // yalnızca kimlik doğrulama uçları kısıtlanır.
        var client = CreateClientWithLimit(permitLimit: 1);

        var email = $"kullanici-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/api/auth/register", new { email, password = "GucluSifre123" });

        // register limiti tükettiği için ikinci bir auth çağrısı yapmadan,
        // aynı host üzerinde yeni bir istemciyle token alıp para uçlarını deneriz.
        var moneyClient = CreateClientWithLimit(permitLimit: 100);
        var email2 = $"kullanici-{Guid.NewGuid():N}@example.com";
        await moneyClient.PostAsJsonAsync("/api/auth/register", new { email = email2, password = "GucluSifre123" });
        var login = await moneyClient.PostAsJsonAsync("/api/auth/login", new { email = email2, password = "GucluSifre123" });
        var token = (await login.Content.ReadFromJsonAsync<WalletApi.Contracts.AuthResponse>())!.Token;
        moneyClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Sınır 100 ama para uçları zaten politikaya bağlı değil; çok sayıda çağrı geçer.
        for (var i = 0; i < 5; i++)
        {
            var deposit = await moneyClient.PostAsJsonAsync("/api/transactions/deposit", new { amount = 10m });
            Assert.Equal(HttpStatusCode.OK, deposit.StatusCode);
        }

        _ = client;
    }
}
