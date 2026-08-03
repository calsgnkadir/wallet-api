namespace WalletApi.Contracts;

public record AccountResponse(Guid Id, decimal Balance, string Currency, DateTimeOffset CreatedAt);
