using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WalletApi.Data;
using WalletApi.Domain;

namespace WalletApi.Tests;

// Her test kendi bellek içi SQLite veritabanını alır: testler birbirini etkilemez
// ve gerçek EF Core davranışı (kısıtlar, eşzamanlılık jetonu) korunur.
// Bağlantı kapanırsa veritabanı yok olur, bu yüzden bağlantıyı açık tutuyoruz.
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public WalletDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<WalletDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new WalletDbContext(options);
    }

    // Testlerin çoğu "hesabı olan bir kullanıcı" ile başlar; tekrarı buraya topladık.
    public async Task<(Guid UserId, Guid AccountId)> AddUserWithAccountAsync(
        string email, decimal balance = 0m)
    {
        await using var context = CreateContext();

        var user = new User { Email = email, PasswordHash = "test-hash" };
        var account = new Account { UserId = user.Id, Balance = balance };

        context.Users.Add(user);
        context.Accounts.Add(account);
        await context.SaveChangesAsync();

        return (user.Id, account.Id);
    }

    public void Dispose() => _connection.Dispose();
}
