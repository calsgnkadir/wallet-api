using System.ComponentModel.DataAnnotations;

namespace WalletApi.Contracts;

public record TransferRequest(
    [Required]
    [EmailAddress]
    string ToEmail,

    [Required]
    [Range(typeof(decimal), "0.01", "1000000",
        ParseLimitsInInvariantCulture = true,
        ErrorMessage = "Tutar 0,01 ile 1.000.000 arasında olmalıdır.")]
    decimal Amount,

    [MaxLength(256)]
    string? Description
);
