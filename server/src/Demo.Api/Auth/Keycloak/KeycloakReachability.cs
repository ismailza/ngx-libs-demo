using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Demo.Api.Auth.Keycloak;

/// <summary>Whether the realm's discovery document — and so its signing keys — can be fetched.</summary>
public sealed class KeycloakReachability(IHttpClientFactory httpClients, IOptions<KeycloakOptions> options) : IHealthCheck
{
    public const string HttpClientName = "keycloak";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var address = new Uri(options.Value.MetadataAddress);
        try
        {
            using var client = httpClients.CreateClient(HttpClientName);
            using var response = await client.GetAsync(address, cancellationToken);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"{address} answered {(int)response.StatusCode}.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy($"{address} is unreachable.", ex);
        }
    }
}

/// <summary>
/// Checks once at startup, retrying while Keycloak boots, and logs an error that names the
/// likely cause. Doesn't stop the API: Keycloak may come up later, and tokens are then accepted.
/// </summary>
public sealed partial class KeycloakStartupProbe(
    KeycloakReachability reachability, TimeProvider clock, ILogger<KeycloakStartupProbe> logger) : BackgroundService
{
    private const int Attempts = 6;
    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        HealthCheckResult result = default;
        for (var attempt = 1; attempt <= Attempts; attempt++)
        {
            result = await reachability.CheckHealthAsync(new HealthCheckContext(), stoppingToken);
            if (result.Status == HealthStatus.Healthy)
            {
                return;
            }

            if (attempt < Attempts)
            {
                await Task.Delay(Delay, clock, stoppingToken);
            }
        }

        LogUnreachable(logger, result.Description, result.Exception);
    }

    [LoggerMessage(Level = LogLevel.Error, Message =
        "Auth:Provider is Keycloak, but {Reason} Every request with a token will be rejected with 401. " +
        "Check Auth:Keycloak:Url/InternalUrl; with Docker Compose, set COMPOSE_PROFILES=keycloak so Keycloak starts.")]
    private static partial void LogUnreachable(ILogger logger, string? reason, Exception? exception);
}
