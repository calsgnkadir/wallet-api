using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using WalletApi.Auth;
using WalletApi.Contracts;
using WalletApi.Data;
using WalletApi.Domain;

namespace WalletApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly WalletDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly IPasswordHasher<User> _passwordHasher;

    public AuthController(
        WalletDbContext db,
        ITokenService tokenService,
        IPasswordHasher<User> passwordHasher)
    {
        _db = db;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
    }

    // POST /api/auth/register
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponse>> Register(RegisterRequest request)
    {
        var email = Normalize(request.Email);

        if (await _db.Users.AnyAsync(u => u.Email == email))
        {
            return Conflict(new { message = "Bu e-posta adresi zaten kayıtlı." });
        }

        var user = new User
        {
            Email = email,
            // Geçici değer: hash, kullanıcı nesnesi üzerinden üretilir.
            PasswordHash = string.Empty
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var response = ToResponse(user);
        return CreatedAtAction(nameof(Me), null, response);
    }

    // POST /api/auth/login
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var email = Normalize(request.Email);
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Email == email);

        if (user is null)
        {
            // Kullanıcı yoksa da hash doğrulaması yapıyoruz: aksi halde yanıt süresi
            // "bu e-posta kayıtlı mı?" sorusunu ele verir (timing attack).
            _passwordHasher.VerifyHashedPassword(
                new User { Email = email, PasswordHash = DummyHash },
                DummyHash,
                request.Password);

            return InvalidCredentials();
        }

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (result == PasswordVerificationResult.Failed)
        {
            return InvalidCredentials();
        }

        // Hash algoritması güncellendiyse şifreyi sessizce yeni formata taşı.
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
            await _db.SaveChangesAsync();
        }

        var (token, expiresAt) = _tokenService.CreateToken(user);
        return Ok(new AuthResponse(token, expiresAt));
    }

    // GET /api/auth/me — yalnızca geçerli token ile erişilir.
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserResponse>> Me()
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!Guid.TryParse(subject, out var userId))
        {
            return Unauthorized();
        }

        var user = await _db.Users.FindAsync(userId);

        // Token geçerli ama kullanıcı silinmiş olabilir.
        return user is null ? Unauthorized() : Ok(ToResponse(user));
    }

    // Kullanıcının var olup olmadığını ele vermemek için tek ve aynı mesaj.
    private ActionResult InvalidCredentials() =>
        Unauthorized(new { message = "E-posta veya şifre hatalı." });

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();

    private static UserResponse ToResponse(User user) =>
        new(user.Id, user.Email, user.Role.ToString(), user.CreatedAt);

    // Kullanıcı bulunamadığında sabit süreli doğrulama yapmak için kullanılan örnek hash.
    private const string DummyHash =
        "AQAAAAIAAYagAAAAEHxS0FDgnJZ8H3sGVvJ1kQ2xNvJ0bH7lD3PqR8mYt5wZfKcX1aB2cD3eF4gH5iJ6kA==";
}
