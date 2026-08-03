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
    // Bağlantı kapanınca bellek içi veritabanı silinir; bunu açık tutuyoruz.
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();

        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "WalletApi.Tests",
                ["Jwt:Audience"] = "WalletApi.Tests",
                ["Jwt:ExpiryMinutes"] = "60",

                // Teste özgü anahtar: gerçek gizli anahtar testlerde kullanılmaz.
                ["Jwt:Key"] = "test-only-signing-key-that-is-long-enough-32"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Uygulamanın PostgreSQL kaydını kaldırıp yerine SQLite'ı koyuyoruz.
            services.RemoveAll<DbContextOptions<WalletDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<WalletDbContext>>();
            services.RemoveAll<WalletDbContext>();

            services.AddDbContext<WalletDbContext>(options => options.UseSqlite(_connection));
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
            _connection.Dispose();
        }
    }
}
