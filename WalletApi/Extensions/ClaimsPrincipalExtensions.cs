using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace WalletApi.Extensions;

public static class ClaimsPrincipalExtensions
{
    // Token'daki "sub" claim'i kullanıcının kimliğidir.
    // [Authorize] geçildiyse burası her zaman doludur.
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return Guid.TryParse(subject, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("Token geçerli bir kullanıcı kimliği taşımıyor.");
    }
}
