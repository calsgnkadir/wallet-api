using Microsoft.AspNetCore.Diagnostics;
using WalletApi.Domain;

namespace WalletApi.Infrastructure;

// İş kuralı hatalarını tek yerden HTTP durum koduna çevirir; controller'lar
// try/catch ile dolmaz. Spring'deki @RestControllerAdvice ile aynı rol.
public class WalletExceptionHandler : IExceptionHandler
{
    private readonly ILogger<WalletExceptionHandler> _logger;

    public WalletExceptionHandler(ILogger<WalletExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken ct)
    {
        // Tanımadığımız hatalar buradan geçmez: bilinmeyen bir hatayı 500 olarak
        // bırakmak, yanlış bir durum koduyla gizlemekten iyidir.
        if (exception is not WalletException walletException)
        {
            return false;
        }

        _logger.LogInformation(
            "İş kuralı reddi: {Code} - {Message}", walletException.Code, walletException.Message);

        context.Response.StatusCode = StatusCode(walletException.Code);

        await context.Response.WriteAsJsonAsync(
            new { code = walletException.Code.ToString(), message = walletException.Message }, ct);

        return true;
    }

    private static int StatusCode(WalletErrorCode code) => code switch
    {
        WalletErrorCode.AccountNotFound => StatusCodes.Status404NotFound,
        WalletErrorCode.InsufficientFunds => StatusCodes.Status422UnprocessableEntity,
        WalletErrorCode.ConcurrencyConflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest
    };
}
