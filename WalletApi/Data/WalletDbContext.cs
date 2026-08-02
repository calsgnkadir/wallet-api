using Microsoft.EntityFrameworkCore;
using WalletApi.Domain;

namespace WalletApi.Data;

public class WalletDbContext : DbContext
{
    public WalletDbContext(DbContextOptions<WalletDbContext> options)
        : base(options)
    {
    }

    // Her DbSet bir tabloya karşılık gelir.
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(user =>
        {
            user.HasKey(u => u.Id);

            user.Property(u => u.Email)
                .IsRequired()
                .HasMaxLength(256);

            // Aynı e-posta ile ikinci kayıt açılamaz (veritabanı seviyesinde garanti).
            user.HasIndex(u => u.Email).IsUnique();

            user.Property(u => u.PasswordHash)
                .IsRequired();

            // Enum'ı veritabanına 0/1 yerine "User"/"Admin" olarak yaz: okunabilir kalsın.
            user.Property(u => u.Role)
                .HasConversion<string>()
                .HasMaxLength(20);
        });
    }
}
