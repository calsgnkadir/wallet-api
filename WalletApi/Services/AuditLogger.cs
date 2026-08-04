using WalletApi.Data;
using WalletApi.Domain;

namespace WalletApi.Services;

public class AuditLogger : IAuditLogger
{
    private const int UserAgentMaxLength = 512;

    private readonly WalletDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditLogger(WalletDbContext db, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    public void Record(
        AuditAction action,
        AuditOutcome outcome,
        Guid? userId = null,
        Guid? targetId = null,
        decimal? amount = null,
        string? details = null)
    {
        var context = _httpContextAccessor.HttpContext;

        var userAgent = context?.Request.Headers.UserAgent.ToString();

        _db.AuditEvents.Add(new AuditEvent
        {
            Action = action,
            Outcome = outcome,
            UserId = userId,
            TargetId = targetId,
            Amount = amount,
            Details = details,
            IpAddress = context?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Truncate(userAgent, UserAgentMaxLength)
        });
    }

    // İstemcinin gönderdiği başlık uzun olabilir; sütun sınırını aşıp
    // asıl işlemi başarısız kılmasına izin vermiyoruz.
    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrEmpty(value)
            ? null
            : value.Length <= maxLength ? value : value[..maxLength];
}
