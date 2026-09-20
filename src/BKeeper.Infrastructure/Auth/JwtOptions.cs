namespace BKeeper.Infrastructure.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "BKeeper";
    public string Audience { get; set; } = "BKeeper";
    public string SigningKey { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 480;
}
