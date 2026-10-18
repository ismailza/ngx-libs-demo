using Microsoft.Extensions.Options;

namespace Demo.Api.Auth.Local;

/// <summary>Reads and writes the refresh-token cookie: HttpOnly, SameSite=Strict, scoped to the auth path.</summary>
public sealed class RefreshCookie(IOptions<RefreshTokenOptions> options)
{
    private readonly RefreshTokenOptions _options = options.Value;

    public string? Read(HttpRequest request) =>
        request.Cookies.TryGetValue(_options.CookieName, out var value) && !string.IsNullOrEmpty(value) ? value : null;

    public void Write(HttpResponse response, IssuedRefreshToken token) =>
        response.Cookies.Append(_options.CookieName, token.Value, CookieOptions(token.ExpiresAt));

    public void Clear(HttpResponse response) =>
        response.Cookies.Delete(_options.CookieName, CookieOptions(expires: null));

    private CookieOptions CookieOptions(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = _options.Secure,
        SameSite = SameSiteMode.Strict,
        Path = _options.CookiePath,
        Expires = expires,
        IsEssential = true,
    };
}
