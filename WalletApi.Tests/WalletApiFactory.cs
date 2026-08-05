using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using WalletApi.Data;

namespace WalletApi.Tests;

// Uygulamanın tamamını bellekte ayağa kaldırır: gerçek middleware zinciri,
// gerçek JWT doğrulaması, gerçek controller'lar. Yalnızca veritabanı ve
// imzalama anahtarı teste özgüdür.
//
// Üretimde PostgreSQL kullanılır; testler bellek içi SQLite'a geçer, böylece
// paket kurulu bir veritabanı sunucusu olmadan da "dotnet test" çalışır.
public class WalletApiFactory : WebApplicationFactory<Program>
{
    // Paylaşımlı bellek içi veritabanı: her DbContext kendi bağlantısını açar.
    // Tek bir bağlantı nesnesini paylaşmak, eşzamanlı isteklerde SQLite'ı
    // güvensiz biçimde birden çok iş parçacığından kullanmak olurdu.
    private readonly string _connectionString =
        $"DataSource=file:{Guid.NewGuid():N}?mode=memory&cache=shared";

    // Son bağlantı kapanınca bellek içi veritabanı silinir; birini açık tutuyoruz.
    private SqliteConnection? _keepAlive;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();

        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "WalletApi.Tests",
                ["Jwt:Audience"] = "WalletApi.Tests",
                ["Jwt:ExpiryMinutes"] = "60",

                // Teste özgü anahtar: gerçek gizli anahtar testlerde kullanılmaz.
                ["Jwt:Key"] = "test-only-signing-key-that-is-long-enough-32",

                // Diğer testler login/register'ı çok çağırır; rate limit'e
                // takılmasınlar diye sınırı yükseğe alıyoruz. Rate limit davranışı
                // kendi testinde, düşük bir sınırla ayrıca sınanır.
                ["RateLimiting:Auth:PermitLimit"] = "100000"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Uygulamanın PostgreSQL kaydını kaldırıp yerine SQLite'ı koyuyoruz.
            services.RemoveAll<DbContextOptions<WalletDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<WalletDbContext>>();
            services.RemoveAll<WalletDbContext>();

            services.AddDbContext<WalletDbContext>(options => options.UseSqlite(_connectionString));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<WalletDbContext>().Database.EnsureCreated();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _keepAlive?.Dispose();
        }
    }
}
