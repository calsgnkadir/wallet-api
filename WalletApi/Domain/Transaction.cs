namespace WalletApi.Domain;

// İşlem kaydı: oluşturulduktan sonra asla değiştirilmez veya silinmez.
// Bakiye bu kayıtların sonucudur; her hareketin izi kalır.
public class Transaction
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AccountId { get; set; }

    public Account? Account { get; set; }

    public TransactionType Type { get; set; }

    public decimal Amount { get; set; }

    // İşlem sonrası bakiye: geçmişi yeniden hesaplamadan denetlenebilsin diye saklanır.
    public decimal BalanceAfter { get; set; }

    // Transferlerde karşı tarafın hesabı; yatırma/çekmede boştur.
    public Guid? CounterpartyAccountId { get; set; }

    public string? Description { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
