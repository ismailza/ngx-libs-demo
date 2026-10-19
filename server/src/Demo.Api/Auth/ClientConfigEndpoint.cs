using Demo.Api.Auth.Keycloak;
using Demo.Api.Auth.Local;
using Microsoft.Extensions.Options;

namespace Demo.Api.Auth;

/// <summary>What the Angular app needs to pick and configure its auth adapter.</summary>
public sealed record ClientConfig(ClientAuthConfig Auth, ClientFeatures Features);

/// <summary>
/// Which optional parts of the API are mapped, so the client shows pages by what exists rather
/// than by provider name.
/// </summary>
/// <param name="Admin"><c>/api/v1/admin/*</c>: user and session management.</param>
/// <param name="SignUpForm"><c>POST /api/auth/register</c>: the app's own sign-up form.</param>
public sealed record ClientFeatures(bool Admin, bool SignUpForm);

/// <param name="Provider"><c>local</c> → <c>withHttpAuth()</c>, <c>keycloak</c> → <c>withKeycloak()</c>.</param>
public sealed record ClientAuthConfig(string Provider, LocalClientConfig? Local = null, KeycloakClientConfig? Keycloak = null);

public sealed record LocalClientConfig(LocalEndpoints Endpoints);

public sealed record LocalEndpoints(string SignIn, string Refresh, string Logout, string Profile, string Register);

public sealed record KeycloakClientConfig(string Url, string Realm, string ClientId);

public static class ClientConfigEndpoint
{
    public const string Path = "/api/config";

    /// <summary>
    /// Read by the client before it bootstraps, so switching provider is a server setting —
    /// no client rebuild.
    /// </summary>
    public static void MapClientConfig(this IEndpointRouteBuilder app) =>
        app.MapGet(Path, (IOptions<AuthOptions> auth, IServiceProvider services, HttpContext http) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            return TypedResults.Ok(auth.Value.Provider switch
            {
                // Both features exist only where this API owns the users.
                AuthProvider.Local => new ClientConfig(
                    new ClientAuthConfig("local", Local: new LocalClientConfig(LocalAuthEndpoints.Paths)),
                    new ClientFeatures(Admin: true, SignUpForm: true)),
                AuthProvider.Keycloak => new ClientConfig(
                    KeycloakAuthSetup.ClientConfig(services),
                    new ClientFeatures(Admin: false, SignUpForm: false)),
                _ => throw new InvalidOperationException("Unknown Auth:Provider."),
            });
        })
        .WithTags("Config")
        .AllowAnonymous();
}
