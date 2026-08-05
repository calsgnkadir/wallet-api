using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace WalletApi.Infrastructure;

public static class RateLimitingSetup
{
    // Kimlik doğrulama uç noktalarına uygulanan politika adı.
    public const string AuthPolicy = "auth";

    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services)
    {
        // Sınırlar yapılandırmadan tembel okunur: değer, testte olduğu gibi sonradan
        // eklenen bir yapılandırma kaynağıyla ezilse bile burada görünür.
        services.AddOptions<RateLimitingOptions>().BindConfiguration(RateLimitingOptions.SectionName);

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Her IP için ayrı sayaç: bir saldırganın denemeleri başka
            // kullanıcıların giriş yapmasını engellemesin.
            options.AddPolicy(AuthPolicy, httpContext =>
            {
                var settings = httpContext.RequestServices
                    .GetRequiredService<IOptions<RateLimitingOptions>>().Value;

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.PermitLimit,
                        Window = TimeSpan.FromSeconds(settings.WindowSeconds),
                        // Sınır aşıldığında istekleri sıraya alma, hemen reddet.
                        QueueLimit = 0
                    });
            });

            options.OnRejected = async (context, token) =>
            {
                var settings = context.HttpContext.RequestServices
                    .GetRequiredService<IOptions<RateLimitingOptions>>().Value;

                context.HttpContext.Response.Headers.RetryAfter = settings.WindowSeconds.ToString();

                // Reddedilen denemeler yapılandırılmış loga yazılır, veritabanına
                // değil: aksi halde bir saldırgan sel gönderip denetim tablosunu
                // şişirebilir (kendi kaydını amplifiye eder).
                context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("RateLimiting")
                    .LogWarning(
                        "Rate limit aşıldı: {Path} kaynak {Ip}",
                        context.HttpContext.Request.Path,
                        context.HttpContext.Connection.RemoteIpAddress);

                await context.HttpContext.Response.WriteAsJsonAsync(
                    new
                    {
                        code = "TooManyRequests",
                        message = "Çok fazla deneme yaptınız, lütfen biraz sonra tekrar deneyin."
                    },
                    token);
            };
        });
    }
}
