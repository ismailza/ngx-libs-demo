using Demo.Api.Auth.Keycloak;
using Demo.Api.Auth.Local;
using Microsoft.Extensions.Options;

namespace Demo.Api.Auth;

/// <summary>What the Angular app needs to pick and configure its auth adapter.</summary>
public sealed record ClientConfig(ClientAuthConfig Auth);

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
            return TypedResults.Ok(new ClientConfig(auth.Value.Provider switch
            {
                AuthProvider.Local => new ClientAuthConfig("local", Local: new LocalClientConfig(LocalAuthEndpoints.Paths)),
                AuthProvider.Keycloak => KeycloakAuthSetup.ClientConfig(services),
                _ => throw new InvalidOperationException("Unknown Auth:Provider."),
            }));
        })
        .WithTags("Config")
        .AllowAnonymous();
}
