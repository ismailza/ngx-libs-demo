namespace Demo.Api.Auth;

/// <summary>Who issues the access tokens the API accepts.</summary>
public enum AuthProvider
{
    /// <summary>
    /// This API: ASP.NET Core Identity checks passwords and issues JWTs, with a rotating
    /// refresh cookie. The client uses <c>withHttpAuth()</c>.
    /// </summary>
    Local,

    /// <summary>
    /// An external Keycloak realm issues the tokens; this API only validates them. The client
    /// uses <c>withKeycloak()</c>. See docs/auth-providers.md.
    /// </summary>
    Keycloak,
}

public sealed class AuthOptions
{
    public const string Section = "Auth";

    public AuthProvider Provider { get; set; } = AuthProvider.Local;
}

public static class Roles
{
    public const string User = "user";
    public const string Admin = "admin";
}

public static class Policies
{
    public const string Admin = "admin";
}
