using Microsoft.EntityFrameworkCore;
using WalletApi.Data.Converters;
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

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<Transaction> Transactions => Set<Transaction>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // PostgreSQL DateTimeOffset'i (timestamptz) yerel olarak destekler; dönüşüm
        // yalnızca bu tipi tanımayan SQLite için gerekli (testler onu kullanır).
        if (Database.IsSqlite())
        {
            configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcTicksConverter>();
        }
    }

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

        modelBuilder.Entity<Account>(account =>
        {
            account.HasKey(a => a.Id);

            account.HasOne(a => a.User)
                .WithOne()
                .HasForeignKey<Account>(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Kullanıcı başına tek cüzdan hesabı.
            account.HasIndex(a => a.UserId).IsUnique();

            account.Property(a => a.Balance)
                .HasPrecision(18, 2);

            account.Property(a => a.Currency)
                .IsRequired()
                .HasMaxLength(3);

            // Güncelleme sorgusunun WHERE'ine eklenir: değer değiştiyse satır bulunamaz
            // ve EF Core DbUpdateConcurrencyException fırlatır.
            account.Property(a => a.RowVersion)
                .IsConcurrencyToken();
        });

        modelBuilder.Entity<Transaction>(transaction =>
        {
            transaction.HasKey(t => t.Id);

            transaction.HasOne(t => t.Account)
                .WithMany()
                .HasForeignKey(t => t.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            transaction.Property(t => t.Type)
                .HasConversion<string>()
                .HasMaxLength(20);

            transaction.Property(t => t.Amount)
                .HasPrecision(18, 2);

            transaction.Property(t => t.BalanceAfter)
                .HasPrecision(18, 2);

            transaction.Property(t => t.Description)
                .HasMaxLength(256);

            // Hesap geçmişi tarih sırasına göre sorgulanır.
            transaction.HasIndex(t => new { t.AccountId, t.CreatedAt });
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Değişen her hesabın jetonunu tazele; eski değer WHERE'de kullanıldığı için
        // araya giren başka bir güncelleme varsa bu kayıt çakışma olarak yakalanır.
        foreach (var entry in ChangeTracker.Entries<Account>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.RowVersion = Guid.NewGuid();
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
