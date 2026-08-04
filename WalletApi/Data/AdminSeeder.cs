using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WalletApi.Domain;

namespace WalletApi.Data;

// Denetim kaydını okuyabilecek ilk yönetici hesabını oluşturur.
// Kimlik bilgileri yapılandırmadan gelir; verilmezse hiçbir şey yapılmaz,
// yani varsayılan kurulumda repoda yazılı bir yönetici parolası bulunmaz.
public static class AdminSeeder
{
    public static async Task SeedAsync(
        IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        var email = configuration["Seed:AdminEmail"];
        var password = configuration["Seed:AdminPassword"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var db = services.GetRequiredService<WalletDbContext>();
        var hasher = services.GetRequiredService<IPasswordHasher<User>>();

        var normalized = email.Trim().ToLowerInvariant();

        // Var olan hesabın parolası ezilmez; tohumlama yalnızca ilk kurulumdadır.
        if (await db.Users.AnyAsync(u => u.Email == normalized))
        {
            return;
        }

        var admin = new User
        {
            Email = normalized,
            PasswordHash = string.Empty,
            Role = UserRole.Admin
        };
        admin.PasswordHash = hasher.HashPassword(admin, password);

        db.Users.Add(admin);
        db.Accounts.Add(new Account { UserId = admin.Id });

        await db.SaveChangesAsync();

        logger.LogInformation("Yönetici hesabı oluşturuldu: {Email}", normalized);
    }
}
