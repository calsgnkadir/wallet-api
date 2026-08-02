using WalletApi.Domain;

namespace WalletApi.Auth;

public interface ITokenService
{
    (string Token, DateTimeOffset ExpiresAt) CreateToken(User user);
}
