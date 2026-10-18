using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace Demo.Api.Auth.Keycloak;

public sealed class KeycloakOptions
{
    public const string Section = "Auth:Keycloak";

    /// <summary>Keycloak base URL as the browser reaches it, e.g. <c>http://localhost:8180</c>.</summary>
    [Required, Url] public string Url { get; set; } = "";

    [Required] public string Realm { get; set; } = "";

    /// <summary>Public client the Angular app signs in with.</summary>
    [Required] public string ClientId { get; set; } = "";

    /// <summary>Expected <c>aud</c> of access tokens sent to this API.</summary>
    [Required] public string Audience { get; set; } = "";
}

/// <summary>
/// Placeholder for the Keycloak provider. The shape is settled (options, client config); the
/// token validation is not written yet. See docs/auth-providers.md for the plan.
/// </summary>
public static class KeycloakAuthSetup
{
    public static WebApplicationBuilder AddKeycloakAuth(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<KeycloakOptions>()
            .BindConfiguration(KeycloakOptions.Section)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // TODO(keycloak): AddJwtBearer with Authority = {Url}/realms/{Realm} and Audience, plus an
        // IClaimsTransformation that copies realm_access.roles into `roles` claims.
        throw new NotSupportedException(
            "Auth:Provider 'Keycloak' is not implemented yet. Use 'Local', or see docs/auth-providers.md.");
    }

    public static ClientAuthConfig ClientConfig(IServiceProvider services)
    {
        var options = services.GetRequiredService<IOptions<KeycloakOptions>>().Value;
        return new ClientAuthConfig(
            "keycloak",
            Keycloak: new KeycloakClientConfig(options.Url, options.Realm, options.ClientId));
    }
}
