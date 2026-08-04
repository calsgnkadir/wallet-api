namespace WalletApi.Domain;

// İstemcinin gönderdiği Idempotency-Key başlığını ve o isteğin sonucunu tutar.
// Aynı anahtarla gelen ikinci istek işlemi tekrarlamaz, saklanan yanıtı alır.
public class IdempotencyRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public required string Key { get; set; }

    // Anahtar yalnızca gönderildiği uç nokta için geçerlidir.
    public required string Endpoint { get; set; }

    // İstek gövdesinin özeti: aynı anahtarla farklı bir tutar gönderilirse
    // bunu sessizce tekrar saymak yerine hata veririz.
    public required string RequestHash { get; set; }

    public int? StatusCode { get; set; }

    public string? ResponseBody { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Boşsa istek hâlâ işleniyor demektir.
    public DateTimeOffset? CompletedAt { get; set; }
}
