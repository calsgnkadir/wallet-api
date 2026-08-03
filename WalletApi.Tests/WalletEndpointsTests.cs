using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WalletApi.Contracts;

namespace WalletApi.Tests;

public class WalletEndpointsTests : IClassFixture<WalletApiFactory>
{
    private readonly WalletApiFactory _factory;

    public WalletEndpointsTests(WalletApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Kayit_HesabiSifirBakiyeIleAcar()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, Email());

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var account = await client.GetFromJsonAsync<AccountResponse>("/api/accounts/me");

        Assert.NotNull(account);
        Assert.Equal(0m, account.Balance);
        Assert.Equal("TRY", account.Currency);
    }

    [Fact]
    public async Task Kayit_AyniEpostaIcinCakismaDoner()
    {
        var client = _factory.CreateClient();
        var email = Email();

        var first = await client.PostAsJsonAsync("/api/auth/register", new { email, password = "GucluSifre123" });
        var second = await client.PostAsJsonAsync("/api/auth/register", new { email, password = "GucluSifre123" });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Kayit_KisaSifreyiReddeder()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register", new { email = Email(), password = "kisa" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Giris_YanlisSifreyiReddeder()
    {
        var client = _factory.CreateClient();
        var email = Email();
        await client.PostAsJsonAsync("/api/auth/register", new { email, password = "GucluSifre123" });

        var response = await client.PostAsJsonAsync(
            "/api/auth/login", new { email, password = "YanlisSifre999" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Giris_TanimsizKullaniciyiAyniSekildeReddeder()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login", new { email = Email(), password = "GucluSifre123" });

        // Kullanıcının var olmadığını ele vermemek için yanlış şifreyle aynı yanıt.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/auth/me")]
    [InlineData("/api/accounts/me")]
    [InlineData("/api/transactions")]
    public async Task KorumaliUcNoktalar_TokensizErisimiReddeder(string url)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task KorumaliUcNokta_KurcalanmisTokeniReddeder()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, Email());

        var tampered = token[..^6] + "AAAAAA";
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tampered);

        var response = await client.GetAsync("/api/accounts/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ParaHareketleri_UctanUcaCalisir()
    {
        var ayse = _factory.CreateClient();
        var mehmet = _factory.CreateClient();

        var ayseEmail = Email();
        var mehmetEmail = Email();

        await AuthenticateAsync(ayse, ayseEmail);
        await AuthenticateAsync(mehmet, mehmetEmail);

        await ayse.PostAsJsonAsync("/api/transactions/deposit", new { amount = 1000m });
        await ayse.PostAsJsonAsync("/api/transactions/withdraw", new { amount = 250.50m });

        var transfer = await ayse.PostAsJsonAsync(
            "/api/transactions/transfer", new { toEmail = mehmetEmail, amount = 300m, description = "kira" });

        Assert.Equal(HttpStatusCode.OK, transfer.StatusCode);

        var ayseAccount = await ayse.GetFromJsonAsync<AccountResponse>("/api/accounts/me");
        var mehmetAccount = await mehmet.GetFromJsonAsync<AccountResponse>("/api/accounts/me");

        Assert.Equal(449.50m, ayseAccount!.Balance);
        Assert.Equal(300m, mehmetAccount!.Balance);

        var history = await ayse.GetFromJsonAsync<List<TransactionResponse>>("/api/transactions");
        Assert.Equal(new[] { "TransferOut", "Withdrawal", "Deposit" }, history!.Select(t => t.Type));
    }

    [Fact]
    public async Task Cekim_YetersizBakiyedeIslenemezDoner()
    {
        var client = _factory.CreateClient();
        await AuthenticateAsync(client, Email());

        var response = await client.PostAsJsonAsync("/api/transactions/withdraw", new { amount = 100m });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_TanimsizAliciyaBulunamadiDoner()
    {
        var client = _factory.CreateClient();
        await AuthenticateAsync(client, Email());
        await client.PostAsJsonAsync("/api/transactions/deposit", new { amount = 500m });

        var response = await client.PostAsJsonAsync(
            "/api/transactions/transfer", new { toEmail = "yok@example.com", amount = 10m });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Kullanicilar_BirbirininHesabiniGoremez()
    {
        var ayse = _factory.CreateClient();
        var mehmet = _factory.CreateClient();

        await AuthenticateAsync(ayse, Email());
        await AuthenticateAsync(mehmet, Email());

        await ayse.PostAsJsonAsync("/api/transactions/deposit", new { amount = 750m });

        var ayseAccount = await ayse.GetFromJsonAsync<AccountResponse>("/api/accounts/me");
        var mehmetAccount = await mehmet.GetFromJsonAsync<AccountResponse>("/api/accounts/me");

        Assert.NotEqual(ayseAccount!.Id, mehmetAccount!.Id);
        Assert.Equal(750m, ayseAccount.Balance);
        Assert.Equal(0m, mehmetAccount.Balance);
    }

    [Fact]
    public async Task Gecmis_SayfaBoyutunuSinirlar()
    {
        var client = _factory.CreateClient();
        await AuthenticateAsync(client, Email());
        await client.PostAsJsonAsync("/api/transactions/deposit", new { amount = 10m });

        // Aşırı büyük "take" değeri sunucuyu yormamalı, sessizce kırpılmalı.
        var response = await client.GetAsync("/api/transactions?take=100000");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static string Email() => $"kullanici-{Guid.NewGuid():N}@example.com";

    private static async Task<string> RegisterAndLoginAsync(HttpClient client, string email)
    {
        await client.PostAsJsonAsync("/api/auth/register", new { email, password = "GucluSifre123" });

        var response = await client.PostAsJsonAsync(
            "/api/auth/login", new { email, password = "GucluSifre123" });

        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return payload!.Token;
    }

    private static async Task AuthenticateAsync(HttpClient client, string email)
    {
        var token = await RegisterAndLoginAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
