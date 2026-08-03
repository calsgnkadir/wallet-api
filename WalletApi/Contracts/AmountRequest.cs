using System.ComponentModel.DataAnnotations;

namespace WalletApi.Contracts;

// Yatırma ve çekme işlemleri için ortak istek gövdesi.
public record AmountRequest(
    [Required]
    // Sınırlar makinenin kültürüne göre değil, her zaman "0.01" olarak okunur:
    // tr-TR'de ondalık ayırıcı virgüldür ve aksi halde ayrıştırma patlar.
    [Range(typeof(decimal), "0.01", "1000000",
        ParseLimitsInInvariantCulture = true,
        ErrorMessage = "Tutar 0,01 ile 1.000.000 arasında olmalıdır.")]
    decimal Amount,

    [MaxLength(256)]
    string? Description
);
