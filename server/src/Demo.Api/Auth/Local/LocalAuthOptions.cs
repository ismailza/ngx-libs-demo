using System.ComponentModel.DataAnnotations;

namespace Demo.Api.Auth.Local;

public sealed class JwtOptions
{
    public const string Section = "Auth:Local:Jwt";

    [Required] public string Issuer { get; set; } = "";
    [Required] public string Audience { get; set; } = "";

    /// <summary>HMAC-SHA256 key. At least 32 characters.</summary>
    [Required, MinLength(32)] public string SigningKey { get; set; } = "";

    /// <summary>Access tokens are short-lived: the refresh cookie is what keeps a session going.</summary>
    [Range(1, 60)] public int AccessTokenMinutes { get; set; } = 5;
}

public sealed class RefreshTokenOptions
{
    public const string Section = "Auth:Local:RefreshToken";

    /// <summary>Sliding lifetime: each refresh extends the session by this much.</summary>
    [Range(1, 90)] public int Days { get; set; } = 7;

    /// <summary>
    /// How long a just-rotated token is still accepted. Two tabs reloading at the same moment
    /// both send the same cookie; without a grace window the slower one looks like token theft
    /// and the whole session is revoked.
    /// </summary>
    [Range(0, 60)] public int ReuseGraceSeconds { get; set; } = 10;

    [Required] public string CookieName { get; set; } = "refresh_token";

    /// <summary>The cookie is only sent to the auth endpoints, never to the rest of the API.</summary>
    [Required] public string CookiePath { get; set; } = "/api/auth";

    public bool Secure { get; set; } = true;
}
