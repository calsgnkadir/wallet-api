using Microsoft.EntityFrameworkCore;
using WalletApi.Data;
using WalletApi.Domain;

namespace WalletApi.Services;

public class WalletService : IWalletService
{
    private readonly WalletDbContext _db;
    private readonly IAuditLogger _audit;

    public WalletService(WalletDbContext db, IAuditLogger audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<Account> GetAccountAsync(Guid userId, CancellationToken ct = default)
    {
        var account = await _db.Accounts.SingleOrDefaultAsync(a => a.UserId == userId, ct);

        return account ?? throw new WalletException(
            WalletErrorCode.AccountNotFound, "Hesap bulunamadı.");
    }

    public async Task<Transaction> DepositAsync(
        Guid userId, decimal amount, string? description, CancellationToken ct = default)
    {
        amount = Normalize(amount);

        var account = await GetAccountAsync(userId, ct);
        account.Balance += amount;

        var transaction = Record(account, TransactionType.Deposit, amount, null, description);
        _audit.Record(AuditAction.Deposit, AuditOutcome.Success, userId, transaction.Id, amount);

        await SaveAsync(ct);
        return transaction;
    }

    public async Task<Transaction> WithdrawAsync(
        Guid userId, decimal amount, string? description, CancellationToken ct = default)
    {
        amount = Normalize(amount);

        var account = await GetAccountAsync(userId, ct);

        if (account.Balance < amount)
        {
            await RecordRejectionAsync(
                AuditAction.Withdrawal, userId, amount, "Yetersiz bakiye.", ct);

            throw new WalletException(WalletErrorCode.InsufficientFunds, "Yetersiz bakiye.");
        }

        account.Balance -= amount;

        var transaction = Record(account, TransactionType.Withdrawal, amount, null, description);
        _audit.Record(AuditAction.Withdrawal, AuditOutcome.Success, userId, transaction.Id, amount);

        await SaveAsync(ct);
        return transaction;
    }

    public async Task<Transaction> TransferAsync(
        Guid fromUserId, string toEmail, decimal amount, string? description, CancellationToken ct = default)
    {
        amount = Normalize(amount);

        var email = toEmail.Trim().ToLowerInvariant();

        var source = await GetAccountAsync(fromUserId, ct);

        var target = await _db.Accounts
            .SingleOrDefaultAsync(a => a.User!.Email == email, ct)
            ?? throw new WalletException(WalletErrorCode.AccountNotFound, "Alıcı hesap bulunamadı.");

        if (source.Id == target.Id)
        {
            throw new WalletException(WalletErrorCode.SelfTransfer, "Kendi hesabınıza transfer yapamazsınız.");
        }

        if (source.Currency != target.Currency)
        {
            throw new WalletException(WalletErrorCode.CurrencyMismatch, "Hesap para birimleri farklı.");
        }

        if (source.Balance < amount)
        {
            await RecordRejectionAsync(
                AuditAction.Transfer, fromUserId, amount, "Yetersiz bakiye.", ct);

            throw new WalletException(WalletErrorCode.InsufficientFunds, "Yetersiz bakiye.");
        }

        // İki hesap ve iki kayıt tek bir veritabanı işleminde değişir:
        // ya hepsi olur ya hiçbiri. Para havada kalmaz.
        await using var dbTransaction = await _db.Database.BeginTransactionAsync(ct);

        source.Balance -= amount;
        target.Balance += amount;

        var outgoing = Record(source, TransactionType.TransferOut, amount, target.Id, description);
        Record(target, TransactionType.TransferIn, amount, source.Id, description);

        _audit.Record(
            AuditAction.Transfer, AuditOutcome.Success, fromUserId, outgoing.Id, amount,
            $"Alıcı hesap: {target.Id}");

        await SaveAsync(ct);
        await dbTransaction.CommitAsync(ct);

        return outgoing;
    }

    public async Task<IReadOnlyList<Transaction>> GetHistoryAsync(
        Guid userId, int take, CancellationToken ct = default)
    {
        var account = await GetAccountAsync(userId, ct);

        return await _db.Transactions
            .Where(t => t.AccountId == account.Id)
            .OrderByDescending(t => t.CreatedAt)
            .Take(take)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    private Transaction Record(
        Account account, TransactionType type, decimal amount, Guid? counterpartyId, string? description)
    {
        var transaction = new Transaction
        {
            AccountId = account.Id,
            Type = type,
            Amount = amount,
            BalanceAfter = account.Balance,
            CounterpartyAccountId = counterpartyId,
            Description = description
        };

        _db.Transactions.Add(transaction);
        return transaction;
    }

    // Reddedilen hareketler de kayda geçer: denetim yalnızca olanları değil,
    // denenip engellenenleri de göstermelidir. Bakiye henüz değiştirilmediği
    // için bu kaydı tek başına yazmak güvenlidir.
    private async Task RecordRejectionAsync(
        AuditAction action, Guid userId, decimal amount, string reason, CancellationToken ct)
    {
        _audit.Record(action, AuditOutcome.Failure, userId, null, amount, reason);

        await _db.SaveChangesAsync(ct);
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Başka bir istek aynı hesabı bu arada değiştirdi; bizim elimizdeki
            // bakiye artık eski. Yazmayı reddediyoruz, çağıran tekrar denemeli.
            throw new WalletException(
                WalletErrorCode.ConcurrencyConflict,
                "Hesap başka bir işlem tarafından güncellendi, lütfen tekrar deneyin.");
        }
    }

    // Kuruşun altındaki basamaklar işlemi belirsiz kılar; girişi 2 haneye sabitliyoruz.
    private static decimal Normalize(decimal amount)
    {
        if (amount <= 0)
        {
            throw new WalletException(WalletErrorCode.InvalidAmount, "Tutar sıfırdan büyük olmalıdır.");
        }

        return decimal.Round(amount, 2, MidpointRounding.ToEven);
    }
}
