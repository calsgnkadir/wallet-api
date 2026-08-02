namespace WalletApi.Domain;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Email { get; set; }

    // Şifrenin kendisi asla saklanmaz; yalnızca hash'i tutulur.
    public required string PasswordHash { get; set; }

    public UserRole Role { get; set; } = UserRole.User;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
