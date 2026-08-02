namespace WalletApi.Contracts;

public record AuthResponse(string Token, DateTimeOffset ExpiresAt);
