using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using WalletApi.Auth;
using WalletApi.Data;
using WalletApi.Domain;

var builder = WebApplication.CreateBuilder(args);

// --- Servis kayıtları (DI konteyneri) — Spring'in ApplicationContext'i ---
builder.Services.AddControllers();

// Veritabanı: EF Core + SQLite
builder.Services.AddDbContext<WalletDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

// Şifre hash'leme (PBKDF2-HMAC-SHA256, ASP.NET Core'un yerleşik uygulaması)
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

// JWT ayarları + token üreten servis
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection(JwtSettings.SectionName));
builder.Services.AddSingleton<ITokenService, TokenService>();

var jwtSettings = builder.Configuration
    .GetSection(JwtSettings.SectionName)
    .Get<JwtSettings>() ?? new JwtSettings();

// İmzalama anahtarı yoksa uygulama hiç başlamasın: eksik yapılandırmayla
// çalışan bir kimlik doğrulama, olmayandan daha tehlikelidir.
if (Encoding.UTF8.GetByteCount(jwtSettings.Key) < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key eksik veya çok kısa (en az 32 bayt olmalı). " +
        "Geliştirme için: dotnet user-secrets set \"Jwt:Key\" \"<uzun-rastgele-deger>\"");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
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
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),

            // Süresi dolan token varsayılan olarak 5 dakika daha kabul edilir; kapatıyoruz.
            ClockSkew = TimeSpan.Zero,

            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = TokenService.RoleClaimType
        };
    });

builder.Services.AddAuthorization();

// Swagger: API'yi tarayıcıdan görüp test etmeni sağlar (springdoc karşılığı)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Swagger arayüzüne "Authorize" düğmesi ekler: token'ı oraya yapıştırıp test edersin.
    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Login'den dönen token'ı buraya yapıştır (başına 'Bearer' yazma)."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme, document)] = []
    });
});

var app = builder.Build();

// --- HTTP pipeline (middleware zinciri) — Spring'in Filter zinciri ---
// Swagger UI'ı yalnızca geliştirme ortamında aç
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();     // /swagger/v1/swagger.json (OpenAPI şeması)
    app.UseSwaggerUI();   // /swagger (tarayıcıdaki arayüz)
}

app.UseHttpsRedirection();

// Sıra önemli: önce "kimsin?" (authentication), sonra "yetkin var mı?" (authorization).
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
