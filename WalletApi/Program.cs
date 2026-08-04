using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using WalletApi.Auth;
using WalletApi.Data;
using WalletApi.Domain;
using WalletApi.Infrastructure;
using WalletApi.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Servis kayıtları (DI konteyneri) — Spring'in ApplicationContext'i ---
builder.Services.AddControllers();

// Veritabanı: EF Core + PostgreSQL (docker compose ile birlikte gelir)
builder.Services.AddDbContext<WalletDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// Şifre hash'leme (PBKDF2-HMAC-SHA256, ASP.NET Core'un yerleşik uygulaması)
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

// Cüzdan iş kuralları. Scoped: DbContext gibi istek başına bir örnek yaşar.
builder.Services.AddScoped<IWalletService, WalletService>();

// İş kuralı hatalarını HTTP durum koduna çeviren tek merkez
builder.Services.AddExceptionHandler<WalletExceptionHandler>();
builder.Services.AddProblemDetails();

// Tekrarlanan para isteklerini yakalayan filtre. Scoped: DbContext kullanır.
builder.Services.AddScoped<IdempotencyFilter>();

// JWT ayarları + token üreten servis.
// İmzalama anahtarı yoksa uygulama hiç başlamasın: eksik yapılandırmayla
// çalışan bir kimlik doğrulama, olmayandan daha tehlikelidir.
builder.Services
    .AddOptions<JwtSettings>()
    .Bind(builder.Configuration.GetSection(JwtSettings.SectionName))
    .Validate(
        settings => Encoding.UTF8.GetByteCount(settings.Key) >= 32,
        "Jwt:Key eksik veya çok kısa (en az 32 bayt olmalı). " +
        "Geliştirme için: dotnet user-secrets set \"Jwt:Key\" \"<uzun-rastgele-deger>\"")
    .ValidateOnStart();

builder.Services.AddSingleton<ITokenService, TokenService>();

// Doğrulama parametreleri de aynı JwtSettings'ten kurulur; token'ı imzalayan
// ve doğrulayan taraf böylece tek kaynaktan beslenir.
builder.Services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

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

// Şema göçünü uygulama başlarken çalıştırmak konteyner kurulumunu kolaylaştırır,
// ama birden fazla örnek aynı anda başlarsa yarışa girer. Bu yüzden varsayılan
// kapalı; docker compose tek örnek başlattığı için orada açıyoruz.
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<WalletDbContext>().Database.MigrateAsync();
}

// --- HTTP pipeline (middleware zinciri) — Spring'in Filter zinciri ---
// En başta: aşağıdaki hiçbir katmandan işlenmemiş hata sızmasın.
app.UseExceptionHandler();

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

// Üst düzey deyimlerle yazılan Program sınıfı varsayılan olarak internal'dır;
// entegrasyon testlerinin uygulamayı ayağa kaldırabilmesi için görünür kılıyoruz.
public partial class Program;
