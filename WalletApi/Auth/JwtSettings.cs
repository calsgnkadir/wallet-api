namespace WalletApi.Auth;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    // İmzalama anahtarı: gizli bilgidir, appsettings.json'a YAZILMAZ.
    // Geliştirmede "dotnet user-secrets", üretimde ortam değişkeni ile verilir.
    public string Key { get; set; } = string.Empty;

    public int ExpiryMinutes { get; set; } = 60;
}
