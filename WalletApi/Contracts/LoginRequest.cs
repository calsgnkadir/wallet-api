using System.ComponentModel.DataAnnotations;

namespace WalletApi.Contracts;

public record LoginRequest(
    [Required]
    [EmailAddress]
    string Email,

    [Required]
    string Password
);
