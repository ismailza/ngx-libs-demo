using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Demo.Api.Auth.Keycloak;

public sealed class KeycloakOptions
{
    public const string Section = "Auth:Keycloak";

    /// <summary>
    /// Keycloak base URL as the browser reaches it, e.g. <c>http://localhost:8180</c>. Tokens are
    /// issued for this URL, so it is also the expected <c>iss</c>.
    /// </summary>
    [Required, Url] public string Url { get; set; } = "";

    /// <summary>
    /// Keycloak base URL as this API reaches it, when that differs from <see cref="Url"/> — in
    /// Docker the API talks to <c>http://keycloak:8080</c>. Used only to fetch signing keys.
    /// </summary>
    [Url] public string? InternalUrl { get; set; }

    [Required] public string Realm { get; set; } = "";

    /// <summary>Public client the Angular app signs in with.</summary>
    [Required] public string ClientId { get; set; } = "";

    /// <summary>Expected <c>aud</c> of access tokens sent to this API.</summary>
    [Required] public string Audience { get; set; } = "";

    /// <summary>Off only where Keycloak is served over plain HTTP (local dev, the Docker stack).</summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    public string Issuer => $"{Url.TrimEnd('/')}/realms/{Realm}";

    public string MetadataAddress =>
        $"{(InternalUrl ?? Url).TrimEnd('/')}/realms/{Realm}/.well-known/openid-configuration";
}

/// <summary>
/// The API only validates Keycloak's access tokens: users, passwords, sessions and refresh all
/// live in Keycloak, and the Angular app talks to it directly through <c>withKeycloak()</c>.
/// </summary>
public static class KeycloakAuthSetup
{
    public static WebApplicationBuilder AddKeycloakAuth(this WebApplicationBuilder builder)
    {
        var services = builder.Services;

        services.AddOptions<KeycloakOptions>()
            .BindConfiguration(KeycloakOptions.Section)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<KeycloakOptions>>((options, keycloak) =>
            {
                var kc = keycloak.Value;
                // Signing keys come from the realm's discovery document, fetched on first use and
                // refreshed when Keycloak rotates them.
                options.MetadataAddress = kc.MetadataAddress;
                options.RequireHttpsMetadata = kc.RequireHttpsMetadata;
                // Keep claim names as issued (`sub`, `roles`) instead of mapping them to long URIs.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    // Set explicitly: the discovery document fetched through InternalUrl may name
                    // a different host than the one the browser got its token from.
                    ValidIssuer = kc.Issuer,
                    ValidAudience = kc.Audience,
                    // Keycloak signs with RS256 by default; refuse anything else (notably HS256,
                    // which would let a public key be used as a shared secret).
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    NameClaimType = "preferred_username",
                    // The realm's "roles" mapper puts realm roles in a top-level `roles` array,
                    // the same claim the Local provider issues.
                    RoleClaimType = "roles",
                    // Lifetime is checked against the system clock: JwtBearerOptions.TimeProvider
                    // does not reach token validation, so it is not set here.
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        // A missing Keycloak otherwise shows up only as a 401 on every call. Say so at startup
        // and in /api/health instead.
        services.AddHttpClient(KeycloakReachability.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(5));
        services.AddSingleton<KeycloakReachability>();
        services.AddHealthChecks().AddCheck<KeycloakReachability>("keycloak");
        services.AddHostedService<KeycloakStartupProbe>();

        return builder;
    }

    public static ClientAuthConfig ClientConfig(IServiceProvider services)
    {
        var options = services.GetRequiredService<IOptions<KeycloakOptions>>().Value;
        return new ClientAuthConfig(
            "keycloak",
            Keycloak: new KeycloakClientConfig(options.Url, options.Realm, options.ClientId));
    }
}
