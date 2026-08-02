using System.ComponentModel.DataAnnotations;

namespace WalletApi.Contracts;

public record RegisterRequest(
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    string Email,

    [Required]
    [MinLength(8, ErrorMessage = "Şifre en az 8 karakter olmalıdır.")]
    string Password
);
