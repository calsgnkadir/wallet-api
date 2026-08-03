namespace WalletApi.Contracts;

public record TransactionResponse(
    Guid Id,
    string Type,
    decimal Amount,
    decimal BalanceAfter,
    Guid? CounterpartyAccountId,
    string? Description,
    DateTimeOffset CreatedAt
);
