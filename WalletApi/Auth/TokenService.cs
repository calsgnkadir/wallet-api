using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using WalletApi.Domain;

namespace WalletApi.Auth;

public class TokenService : ITokenService
{
    public const string RoleClaimType = "role";

    private readonly JwtSettings _settings;

    public TokenService(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;
    }

    public (string Token, DateTimeOffset ExpiresAt) CreateToken(User user)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_settings.ExpiryMinutes);

        var claims = new List<Claim>
        {
            // "sub" = token'ın sahibi. Kullanıcıyı e-posta ile değil, kimliğiyle taşıyoruz.
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(RoleClaimType, user.Role.ToString()),

            // "jti" = token'a özgü kimlik. İleride token iptali (blacklist) için gerekli.
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt.UtcDateTime,
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);

        return (token, expiresAt);
    }
}
