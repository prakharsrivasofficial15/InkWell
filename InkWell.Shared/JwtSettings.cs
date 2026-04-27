namespace InkWell.Shared;

// bound from appsettings.json JwtSettings section via IOptions<T>
public class JwtSettings
{
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;

    // how long the access token lives, default 60 mins
    public int ExpiryMinutes { get; set; } = 60;

    // refresh token lasts longer, default 7 days
    public int RefreshTokenExpiryDays { get; set; } = 7;
}
