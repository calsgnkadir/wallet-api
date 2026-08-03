using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WalletApi.Data;

namespace WalletApi.Tests;

// Uygulamanın tamamını bellekte ayağa kaldırır: gerçek middleware zinciri,
// gerçek JWT doğrulaması, gerçek controller'lar. Yalnızca veritabanı ve
// imzalama anahtarı teste özgüdür.
public class WalletApiFactory : WebApplicationFactory<Program>
{
    // Paylaşımlı bellek içi veritabanı; her fabrika örneği kendi adını alır.
    private readonly string _connectionString =
        $"DataSource=file:{Guid.NewGuid()}?mode=memory&cache=shared";

    private SqliteConnection? _keepAlive;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Bağlantı kapanınca bellek içi veritabanı silinir; birini açık tutuyoruz.
        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();

        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _connectionString,
                ["Jwt:Issuer"] = "WalletApi.Tests",
                ["Jwt:Audience"] = "WalletApi.Tests",
                ["Jwt:ExpiryMinutes"] = "60",

                // Teste özgü anahtar: gerçek gizli anahtar testlerde kullanılmaz.
                ["Jwt:Key"] = "test-only-signing-key-that-is-long-enough-32"
            });
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
