namespace WalletApi.Domain;

// Denetim kaydı: oluşturulduktan sonra ne değiştirilir ne silinir.
// Bir işlemin gerçekten yapıldığını sonradan kanıtlayan tek yer burasıdır.
public class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

    public AuditAction Action { get; set; }

    public AuditOutcome Outcome { get; set; }

    // Başarısız girişte kullanıcı henüz bilinmediği için boş olabilir.
    public Guid? UserId { get; set; }

    // İşlemin dokunduğu kayıt: para hareketlerinde işlem kimliği.
    public Guid? TargetId { get; set; }

    public decimal? Amount { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public string? Details { get; set; }
}
