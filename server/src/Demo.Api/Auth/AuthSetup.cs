using Demo.Api.Auth.Keycloak;
using Demo.Api.Auth.Local;

namespace Demo.Api.Auth;

/// <summary>
/// The one place that knows which provider is configured. Everything downstream — feature
/// endpoints, authorization policies — only sees a validated <see cref="System.Security.Claims.ClaimsPrincipal"/>
/// with <c>sub</c> and <c>roles</c> claims.
/// </summary>
public static class AuthSetup
{
    public static AuthProvider GetAuthProvider(this IConfiguration configuration) =>
        configuration.GetValue($"{AuthOptions.Section}:{nameof(AuthOptions.Provider)}", AuthProvider.Local);

    public static WebApplicationBuilder AddDemoAuth(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<AuthOptions>().BindConfiguration(AuthOptions.Section);

        switch (builder.Configuration.GetAuthProvider())
        {
            case AuthProvider.Local:
                builder.AddLocalAuth();
                break;
            case AuthProvider.Keycloak:
                builder.AddKeycloakAuth();
                break;
            default:
                throw new InvalidOperationException("Unknown Auth:Provider.");
        }

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(Policies.Admin, policy => policy.RequireRole(Roles.Admin));

        return builder;
    }

    public static WebApplication MapDemoAuth(this WebApplication app)
    {
        app.MapClientConfig();

        if (app.Configuration.GetAuthProvider() == AuthProvider.Local)
        {
            app.MapLocalAuthEndpoints();
        }

        return app;
    }
}
