using WalletApi.Domain;

namespace WalletApi.Services;

public interface IWalletService
{
    Task<Account> GetAccountAsync(Guid userId, CancellationToken ct = default);

    Task<Transaction> DepositAsync(Guid userId, decimal amount, string? description, CancellationToken ct = default);

    Task<Transaction> WithdrawAsync(Guid userId, decimal amount, string? description, CancellationToken ct = default);

    Task<Transaction> TransferAsync(Guid fromUserId, string toEmail, decimal amount, string? description, CancellationToken ct = default);

    Task<IReadOnlyList<Transaction>> GetHistoryAsync(Guid userId, int take, CancellationToken ct = default);
}
