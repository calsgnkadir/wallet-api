namespace WalletApi.Domain;

public class Account
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public User? User { get; set; }

    // Para için decimal kullanılır. double/float ikili kayan noktadır ve
    // 0.1 + 0.2 != 0.3 gibi hatalar üretir; kuruş kaybı kabul edilemez.
    public decimal Balance { get; set; }

    public string Currency { get; set; } = "TRY";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Eşzamanlılık jetonu: her güncellemede değişir. İki istek aynı bakiyeyi
    // aynı anda güncellemeye çalışırsa ikincisi bu değerin değiştiğini görür.
    public Guid RowVersion { get; set; } = Guid.NewGuid();
}
