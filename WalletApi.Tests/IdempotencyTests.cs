using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WalletApi.Contracts;

namespace WalletApi.Tests;

public class IdempotencyTests : IClassFixture<WalletApiFactory>
{
    private readonly WalletApiFactory _factory;

    public IdempotencyTests(WalletApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AyniAnahtarlaTekrarlananYatirma_YalnizcaBirKezIslenir()
    {
        var client = await AuthenticateAsync();
        var key = Guid.NewGuid().ToString();

        var first = await PostAsync(client, "/api/transactions/deposit", new { amount = 100m }, key);
        var second = await PostAsync(client, "/api/transactions/deposit", new { amount = 100m }, key);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        // Para yalnızca bir kez hareket etmeli.
        Assert.Equal(100m, await BalanceAsync(client));
    }

    [Fact]
    public async Task TekrarlananIstek_IlkYanitinAynisiniDondurur()
    {
        var client = await AuthenticateAsync();
        var key = Guid.NewGuid().ToString();

        var first = await PostAsync(client, "/api/transactions/deposit", new { amount = 250m }, key);
        var second = await PostAsync(client, "/api/transactions/deposit", new { amount = 250m }, key);

        var firstBody = await first.Content.ReadFromJsonAsync<TransactionResponse>();
        var secondBody = await second.Content.ReadFromJsonAsync<TransactionResponse>();

        // Aynı işlem kimliği: yeni bir kayıt oluşturulmadı.
        Assert.Equal(firstBody!.Id, secondBody!.Id);
        Assert.Equal(firstBody.BalanceAfter, secondBody.BalanceAfter);
    }

    [Fact]
    public async Task TekrarlananIstek_YanitiIsaretlenir()
    {
        var client = await AuthenticateAsync();
        var key = Guid.NewGuid().ToString();

        var first = await PostAsync(client, "/api/transactions/deposit", new { amount = 50m }, key);
        var second = await PostAsync(client, "/api/transactions/deposit", new { amount = 50m }, key);

        Assert.False(first.Headers.Contains("Idempotency-Replayed"));
        Assert.True(second.Headers.Contains("Idempotency-Replayed"));
    }

    [Fact]
    public async Task AyniAnahtarFarkliTutar_Reddedilir()
    {
        var client = await AuthenticateAsync();
        var key = Guid.NewGuid().ToString();

        await PostAsync(client, "/api/transactions/deposit", new { amount = 100m }, key);
        var second = await PostAsync(client, "/api/transactions/deposit", new { amount = 999m }, key);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        // Reddedilen istek bakiyeyi değiştirmemeli.
        Assert.Equal(100m, await BalanceAsync(client));
    }

    [Fact]
    public async Task AyniAnahtarFarkliUcNokta_Reddedilir()
    {
        var client = await AuthenticateAsync();
        var key = Guid.NewGuid().ToString();

        await PostAsync(client, "/api/transactions/deposit", new { amount = 500m }, key);
        var withdraw = await PostAsync(client, "/api/transactions/withdraw", new { amount = 500m }, key);

        Assert.Equal(HttpStatusCode.Conflict, withdraw.StatusCode);
        Assert.Equal(500m, await BalanceAsync(client));
    }

    [Fact]
    public async Task FarkliAnahtarlar_IkiIslemiDeUygular()
    {
        var client = await AuthenticateAsync();

        await PostAsync(client, "/api/transactions/deposit", new { amount = 100m }, Guid.NewGuid().ToString());
        await PostAsync(client, "/api/transactions/deposit", new { amount = 100m }, Guid.NewGuid().ToString());

        Assert.Equal(200m, await BalanceAsync(client));
    }

    [Fact]
    public async Task AnahtarsizIstekler_EskisiGibiCalisir()
    {
        var client = await AuthenticateAsync();

        await PostAsync(client, "/api/transactions/deposit", new { amount = 100m }, key: null);
        await PostAsync(client, "/api/transactions/deposit", new { amount = 100m }, key: null);

        Assert.Equal(200m, await BalanceAsync(client));
    }

    [Fact]
    public async Task BasarisizIstek_AnahtariSerbestBirakir()
    {
        var client = await AuthenticateAsync();
        var key = Guid.NewGuid().ToString();

        // Bakiye yokken çekim başarısız olur.
        var failed = await PostAsync(client, "/api/transactions/withdraw", new { amount = 100m }, key);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, failed.StatusCode);

        await PostAsync(client, "/api/transactions/deposit", new { amount = 300m }, Guid.NewGuid().ToString());

        // Aynı anahtar yeniden denenebilmeli: ilk çağrı para hareket ettirmedi.
        var retried = await PostAsync(client, "/api/transactions/withdraw", new { amount = 100m }, key);

        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
        Assert.Equal(200m, await BalanceAsync(client));
    }

    [Fact]
    public async Task BaskaKullanicininAnahtari_KendiIstegimiEtkilemez()
    {
        var ayse = await AuthenticateAsync();
        var mehmet = await AuthenticateAsync();
        var key = "ortak-anahtar-" + Guid.NewGuid();

        await PostAsync(ayse, "/api/transactions/deposit", new { amount = 100m }, key);
        var mehmetResponse = await PostAsync(mehmet, "/api/transactions/deposit", new { amount = 100m }, key);

        // Anahtarlar kullanıcı bazında ayrıdır.
        Assert.Equal(HttpStatusCode.OK, mehmetResponse.StatusCode);
        Assert.Equal(100m, await BalanceAsync(mehmet));
    }

    [Fact]
    public async Task CokUzunAnahtar_Reddedilir()
    {
        var client = await AuthenticateAsync();

        var response = await PostAsync(
            client, "/api/transactions/deposit", new { amount = 100m }, new string('k', 129));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0m, await BalanceAsync(client));
    }

    [Fact]
    public async Task AyniAnahtarlaEszamanliIstekler_ParayiBirKezHareketEttirir()
    {
        var client = await AuthenticateAsync();
        var key = Guid.NewGuid().ToString();

        var requests = Enumerable.Range(0, 8)
            .Select(_ => PostAsync(client, "/api/transactions/deposit", new { amount = 100m }, key));

        var responses = await Task.WhenAll(requests);

        // Bazıları 200 (biri işledi, diğerleri tekrar), bazıları 409 (hâlâ
        // işleniyor) olabilir; kabul edilemez olan paranın iki kez yatmasıdır.
        Assert.All(responses, response => Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict,
            $"beklenmeyen durum: {response.StatusCode}"));

        Assert.Equal(100m, await BalanceAsync(client));
    }

    private static Task<HttpResponseMessage> PostAsync(
        HttpClient client, string url, object body, string? key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body)
        };

        if (key is not null)
        {
            request.Headers.Add("Idempotency-Key", key);
        }

        return client.SendAsync(request);
    }

    private static async Task<decimal> BalanceAsync(HttpClient client)
    {
        var account = await client.GetFromJsonAsync<AccountResponse>("/api/accounts/me");
        return account!.Balance;
    }

    private async Task<HttpClient> AuthenticateAsync()
    {
        var client = _factory.CreateClient();
        var email = $"kullanici-{Guid.NewGuid():N}@example.com";

        await client.PostAsJsonAsync("/api/auth/register", new { email, password = "GucluSifre123" });

        var response = await client.PostAsJsonAsync(
            "/api/auth/login", new { email, password = "GucluSifre123" });

        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", payload!.Token);

        return client;
    }
}
