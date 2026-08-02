namespace WalletApi.Contracts;

// Kullanıcıyı dışarı verirken PasswordHash asla yer almaz.
public record UserResponse(Guid Id, string Email, string Role, DateTimeOffset CreatedAt);
