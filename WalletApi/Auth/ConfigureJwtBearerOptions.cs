using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace WalletApi.Auth;

// Doğrulama parametrelerini yapılandırmadan doğrudan okumak yerine DI'dan gelen
// JwtSettings üzerinden kurar. Böylece token'ı imzalayan TokenService ile onu
// doğrulayan middleware tek ve aynı ayarı görür.
public class ConfigureJwtBearerOptions : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly JwtSettings _settings;

    public ConfigureJwtBearerOptions(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;
    }

    public void Configure(string? name, JwtBearerOptions options) => Configure(options);

    public void Configure(JwtBearerOptions options)
    {
        // Varsayılan davranış claim adlarını uzun URI'lere çevirir ("sub" ->
        // ".../nameidentifier"). Kapatıyoruz: token'daki adlar neyse o kalsın.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _settings.Issuer,
            ValidAudience = _settings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key)),

            // Süresi dolan token varsayılan olarak 5 dakika daha kabul edilir; kapatıyoruz.
            ClockSkew = TimeSpan.Zero,

            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = TokenService.RoleClaimType
        };
    }
}
