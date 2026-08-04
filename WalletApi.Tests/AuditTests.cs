using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using WalletApi.Contracts;
using WalletApi.Data;
using WalletApi.Domain;

namespace WalletApi.Tests;

public class AuditTests : IClassFixture<WalletApiFactory>
{
    private const string Password = "GucluSifre123";

    private readonly WalletApiFactory _factory;

    public AuditTests(WalletApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Kayit_DenetimeGecer()
    {
        var (client, userId) = await RegisterAsync();

        var events = await ReadAuditAsync(userId);

        Assert.Contains(events, e => e.Action == nameof(AuditAction.UserRegistered));
        _ = client;
    }

    [Fact]
    public async Task BasariliGiris_DenetimeGecer()
    {
        var (_, userId) = await RegisterAsync();

        var events = await ReadAuditAsync(userId);

        var login = Assert.Single(events, e => e.Action == nameof(AuditAction.LoginSucceeded));
        Assert.Equal(nameof(AuditOutcome.Success), login.Outcome);
    }

    [Fact]
    public async Task BasarisizGiris_DenetimeGecer()
    {
        var (_, userId) = await RegisterAsync();
        var email = await EmailOfAsync(userId);

        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/login", new { email, password = "YanlisSifre999" });

        var events = await ReadAuditAsync(userId);

        var failed = Assert.Single(events, e => e.Action == nameof(AuditAction.LoginFailed));
        Assert.Equal(nameof(AuditOutcome.Failure), failed.Outcome);
    }

    [Fact]
    public async Task ParaHareketi_TutariyleBirlikteDenetimeGecer()
    {
        var (client, userId) = await RegisterAsync();

        await client.PostAsJsonAsync("/api/transactions/deposit", new { amount = 400m });
        await client.PostAsJsonAsync("/api/transactions/withdraw", new { amount = 150m });

        var events = await ReadAuditAsync(userId);

        var deposit = Assert.Single(events, e => e.Action == nameof(AuditAction.Deposit));
        var withdrawal = Assert.Single(events, e => e.Action == nameof(AuditAction.Withdrawal));

        Assert.Equal(400m, deposit.Amount);
        Assert.Equal(150m, withdrawal.Amount);
        Assert.NotNull(deposit.TargetId);
    }

    [Fact]
    public async Task ReddedilenCekim_BasarisizOlarakDenetimeGecer()
    {
        var (client, userId) = await RegisterAsync();

        var response = await client.PostAsJsonAsync("/api/transactions/withdraw", new { amount = 100m });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var events = await ReadAuditAsync(userId);

        var rejected = Assert.Single(events, e => e.Action == nameof(AuditAction.Withdrawal));
        Assert.Equal(nameof(AuditOutcome.Failure), rejected.Outcome);
        Assert.Equal(100m, rejected.Amount);
    }

    [Fact]
    public async Task BasarisizTransfer_HicbirParaHareketiKaydiBirakmaz()
    {
        var (client, userId) = await RegisterAsync();
        var (_, targetId) = await RegisterAsync();
        var targetEmail = await EmailOfAsync(targetId);

        await client.PostAsJsonAsync(
            "/api/transactions/transfer", new { toEmail = targetEmail, amount = 500m });

        var events = await ReadAuditAsync(userId);

        var transfer = Assert.Single(events, e => e.Action == nameof(AuditAction.Transfer));
        Assert.Equal(nameof(AuditOutcome.Failure), transfer.Outcome);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WalletDbContext>();
        Assert.Empty(db.Transactions.Where(t => t.Account!.UserId == userId));
    }

    [Fact]
    public async Task DenetimKaydi_DegistirilemezVeSilinemez()
    {
        var (_, userId) = await RegisterAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WalletDbContext>();

        var recorded = db.AuditEvents.First(e => e.UserId == userId);

        recorded.Details = "gecmisi degistirme denemesi";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());

        db.ChangeTracker.Clear();

        db.AuditEvents.Remove(db.AuditEvents.First(e => e.UserId == userId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task DenetimUcNoktasi_SiradanKullaniciyiReddeder()
    {
        var (client, _) = await RegisterAsync();

        var response = await client.GetAsync("/api/audit");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DenetimUcNoktasi_TokensizErisimiReddeder()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/audit");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DenetimUcNoktasi_YoneticiyeAcik()
    {
        var admin = await CreateAdminAsync();

        var response = await admin.GetAsync("/api/audit?take=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(await response.Content.ReadFromJsonAsync<List<AuditEventResponse>>());
    }

    [Fact]
    public async Task DenetimUcNoktasi_KullaniciyaGoreSuzulebilir()
    {
        var (client, userId) = await RegisterAsync();
        await client.PostAsJsonAsync("/api/transactions/deposit", new { amount = 25m });

        var admin = await CreateAdminAsync();
        var events = await admin.GetFromJsonAsync<List<AuditEventResponse>>($"/api/audit?userId={userId}");

        Assert.NotEmpty(events!);
        Assert.All(events!, e => Assert.Equal(userId, e.UserId));
    }

    private async Task<(HttpClient Client, Guid UserId)> RegisterAsync()
    {
        var client = _factory.CreateClient();
        var email = $"kullanici-{Guid.NewGuid():N}@example.com";

        var registered = await client.PostAsJsonAsync(
            "/api/auth/register", new { email, password = Password });
        var user = await registered.Content.ReadFromJsonAsync<UserResponse>();

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);

        return (client, user!.Id);
    }

    // Yönetici HTTP ile oluşturulamaz; doğrudan veritabanına yazıp giriş yapıyoruz.
    private async Task<HttpClient> CreateAdminAsync()
    {
        var email = $"yonetici-{Guid.NewGuid():N}@example.com";

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WalletDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

            var admin = new User { Email = email, PasswordHash = string.Empty, Role = UserRole.Admin };
            admin.PasswordHash = hasher.HashPassword(admin, Password);

            db.Users.Add(admin);
            db.Accounts.Add(new Account { UserId = admin.Id });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);

        return client;
    }

    private async Task<string> EmailOfAsync(Guid userId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WalletDbContext>();

        return db.Users.First(u => u.Id == userId).Email;
    }

    private async Task<List<AuditEventResponse>> ReadAuditAsync(Guid userId)
    {
        var admin = await CreateAdminAsync();

        return (await admin.GetFromJsonAsync<List<AuditEventResponse>>(
            $"/api/audit?userId={userId}&take=200"))!;
    }
}
