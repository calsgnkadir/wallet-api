using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using WalletApi.Data;
using WalletApi.Domain;
using WalletApi.Extensions;

namespace WalletApi.Infrastructure;

// Ağ hatası yüzünden tekrarlanan bir isteğin parayı ikinci kez hareket
// ettirmesini engeller. İstemci "Idempotency-Key" başlığı gönderirse, aynı
// anahtarla gelen sonraki istekler işlemi tekrarlamaz; ilk yanıtı alır.
public class IdempotencyFilter : IAsyncActionFilter
{
    public const string HeaderName = "Idempotency-Key";
    public const string ReplayHeaderName = "Idempotency-Replayed";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly WalletDbContext _db;
    private readonly ILogger<IdempotencyFilter> _logger;

    public IdempotencyFilter(WalletDbContext db, ILogger<IdempotencyFilter> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var key = context.HttpContext.Request.Headers[HeaderName].ToString();

        // Başlık isteğe bağlıdır: gönderilmezse davranış değişmez.
        if (string.IsNullOrWhiteSpace(key))
        {
            await next();
            return;
        }

        if (key.Length > 128)
        {
            context.Result = new BadRequestObjectResult(
                new { code = "InvalidIdempotencyKey", message = "Idempotency-Key en fazla 128 karakter olabilir." });
            return;
        }

        var userId = context.HttpContext.User.GetUserId();
        var endpoint = context.HttpContext.Request.Path.Value ?? string.Empty;
        var requestHash = HashRequest(context.ActionArguments);

        var record = new IdempotencyRecord
        {
            UserId = userId,
            Key = key,
            Endpoint = endpoint,
            RequestHash = requestHash
        };

        _db.IdempotencyRecords.Add(record);

        try
        {
            // Anahtarı önce rezerve ediyoruz. Aynı anda gelen ikinci istek
            // benzersizlik kısıtına takılır ve işlemi tekrarlayamaz.
            await _db.SaveChangesAsync(context.HttpContext.RequestAborted);
        }
        catch (DbUpdateException)
        {
            _db.Entry(record).State = EntityState.Detached;

            context.Result = await ResolveExistingAsync(userId, key, endpoint, requestHash, context);
            return;
        }

        var executed = await next();

        // İstek hata ile bittiyse rezervasyonu geri alıyoruz: başarısız bir
        // çağrı aynı anahtarla yeniden denenebilmelidir.
        if (executed.Exception is not null && !executed.ExceptionHandled)
        {
            await ReleaseAsync(record);
            return;
        }

        var objectResult = executed.Result as ObjectResult;
        var statusCode = objectResult?.StatusCode ?? context.HttpContext.Response.StatusCode;

        if (statusCode is < 200 or >= 300)
        {
            await ReleaseAsync(record);
            return;
        }

        record.StatusCode = statusCode;
        record.ResponseBody = objectResult?.Value is null
            ? null
            : JsonSerializer.Serialize(objectResult.Value, JsonOptions);
        record.CompletedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(CancellationToken.None);
    }

    private async Task<IActionResult> ResolveExistingAsync(
        Guid userId, string key, string endpoint, string requestHash, ActionExecutingContext context)
    {
        var existing = await _db.IdempotencyRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.UserId == userId && r.Key == key);

        if (existing is null)
        {
            // Kayıt araya girip silinmiş olabilir; istemci tekrar denesin.
            return new ConflictObjectResult(new
            {
                code = "IdempotencyConflict",
                message = "İstek işlenemedi, lütfen tekrar deneyin."
            });
        }

        // Aynı anahtarın farklı bir istek için kullanılması bir istemci hatasıdır;
        // sessizce ilk yanıtı döndürmek gerçek bir işlemi yutmak olurdu.
        if (existing.Endpoint != endpoint || existing.RequestHash != requestHash)
        {
            return new ConflictObjectResult(new
            {
                code = "IdempotencyKeyReused",
                message = "Bu Idempotency-Key farklı bir istek için kullanılmış."
            });
        }

        if (existing.CompletedAt is null)
        {
            return new ConflictObjectResult(new
            {
                code = "IdempotentRequestInProgress",
                message = "Aynı istek şu anda işleniyor."
            });
        }

        _logger.LogInformation("Idempotent istek tekrarlandı: {Endpoint} {Key}", endpoint, key);

        context.HttpContext.Response.Headers[ReplayHeaderName] = "true";

        return new ContentResult
        {
            Content = existing.ResponseBody,
            ContentType = "application/json",
            StatusCode = existing.StatusCode
        };
    }

    private async Task ReleaseAsync(IdempotencyRecord record)
    {
        // Başarısız işlemden kalan bekleyen değişiklikler (örneğin çakışma
        // yüzünden yazılamamış bakiye) bu silme sırasında yeniden denenmesin.
        _db.ChangeTracker.Clear();

        _db.IdempotencyRecords.Remove(record);
        await _db.SaveChangesAsync(CancellationToken.None);
    }

    private static string HashRequest(IDictionary<string, object?> arguments)
    {
        // Yalnızca isteğin kendisini özetliyoruz. CancellationToken gibi çerçeve
        // parametreleri hem serileştirilemez hem de isteğin içeriğini tanımlamaz.
        var payload = arguments
            .Where(argument => argument.Value is not CancellationToken)
            .OrderBy(argument => argument.Key, StringComparer.Ordinal)
            .ToDictionary(argument => argument.Key, argument => argument.Value);

        var json = JsonSerializer.Serialize(payload, JsonOptions);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }
}
