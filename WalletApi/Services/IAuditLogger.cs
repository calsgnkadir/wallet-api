using WalletApi.Domain;

namespace WalletApi.Services;

public interface IAuditLogger
{
    // Kaydı yalnızca ekler, veritabanına yazmaz. Böylece çağıranın
    // SaveChanges'i ile aynı işleme dahil olur: para hareket ettiyse
    // denetim kaydı da yazılmıştır, ikisi birbirinden ayrılamaz.
    void Record(
        AuditAction action,
        AuditOutcome outcome,
        Guid? userId = null,
        Guid? targetId = null,
        decimal? amount = null,
        string? details = null);
}
