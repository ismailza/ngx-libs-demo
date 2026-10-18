using System.Collections.Concurrent;
using System.Security.Claims;
using Demo.Api.Auth;
using Demo.Api.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Demo.Api.Features.Playground;

public sealed record FlakyResult(int Attempts, string Message);

public sealed record NameParts(string First, string Last);

public sealed record VersionResult(int Version, string Note, string? Name, NameParts? NameParts);

/// <summary>
/// Attempt counters for <c>/flaky</c>, keyed by user and a client-chosen scenario id. In-memory,
/// demo-only. A run that never succeeds leaves its counter behind, so idle counters expire and
/// the total is capped.
/// </summary>
public sealed class AttemptCounter(TimeProvider clock)
{
    private const int MaxEntries = 10_000;
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<string, Entry> _attempts = new();

    public int Next(string key)
    {
        var now = clock.GetUtcNow();
        if (_attempts.Count >= MaxEntries)
        {
            Sweep(now);
        }

        return _attempts.AddOrUpdate(
            key,
            _ => new Entry(1, now),
            (_, entry) => new Entry(entry.Count + 1, now)).Count;
    }

    public void Reset(string key) => _attempts.TryRemove(key, out _);

    private void Sweep(DateTimeOffset now)
    {
        foreach (var (key, entry) in _attempts)
        {
            if (now - entry.LastSeen > IdleTimeout)
            {
                _attempts.TryRemove(key, out _);
            }
        }

        // Still full: someone is creating keys faster than they expire. Start over.
        if (_attempts.Count >= MaxEntries)
        {
            _attempts.Clear();
        }
    }

    private sealed record Entry(int Count, DateTimeOffset LastSeen);
}

/// <summary>
/// Endpoints that misbehave on purpose, to exercise <c>ngx-api-client</c>: retry with backoff,
/// <c>Retry-After</c>, error normalisation, the loading signal, and API versioning.
/// </summary>
public static class PlaygroundEndpoints
{
    private static readonly int[] RetryableStatuses = [429, 500, 502, 503, 504];

    public static IServiceCollection AddPlayground(this IServiceCollection services) =>
        services.AddSingleton<AttemptCounter>();

    public static void MapPlaygroundEndpoints(this IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/api/v1/playground").WithTags("Playground").RequireAuthorization();
        var v2 = app.MapGroup("/api/v2/playground").WithTags("Playground").RequireAuthorization();

        v1.MapGet("/flaky", Flaky);
        v1.MapGet("/status/{code:int}", Status);
        v1.MapGet("/slow", SlowAsync);

        // Same endpoint, two versions: the client picks one per request with `{ version: 2 }`.
        v1.MapGet("/version", () => TypedResults.Ok(new VersionResult(1, "v1 returns a flat name.", "Ada Lovelace", null)));
        v2.MapGet("/version", () => TypedResults.Ok(new VersionResult(2, "v2 splits the name.", null, new NameParts("Ada", "Lovelace"))));
    }

    /// <summary>
    /// Fails <paramref name="failures"/> times with <paramref name="status"/>, then succeeds.
    /// With 429 a <c>Retry-After: 1</c> header overrides the client's backoff.
    /// </summary>
    private static Results<Ok<FlakyResult>, ProblemHttpResult> Flaky(
        string key, int failures, int? status, AttemptCounter counter, ClaimsPrincipal user, HttpContext http)
    {
        if (key.Length is 0 or > 64)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, detail: "key must be 1 to 64 characters.");
        }

        // Scoped per user, so one user cannot read or reset another's counter.
        key = $"{user.GetUserId()}:{key}";
        var attempt = counter.Next(key);
        if (attempt <= Math.Clamp(failures, 0, 10))
        {
            var code = status is { } s && RetryableStatuses.Contains(s) ? s : StatusCodes.Status503ServiceUnavailable;
            if (code == StatusCodes.Status429TooManyRequests)
            {
                http.Response.Headers.RetryAfter = "1";
            }

            return TypedResults.Problem(statusCode: code, detail: $"Attempt {attempt} failed on purpose ({code}).");
        }

        counter.Reset(key);
        return TypedResults.Ok(new FlakyResult(attempt, $"Succeeded on attempt {attempt}."));
    }

    /// <summary>Any error status, with a problem body: shows how each one becomes an <c>ApiError</c>.</summary>
    private static ProblemHttpResult Status(int code) => Math.Clamp(code, 400, 599) switch
    {
        400 => TypedResults.Problem(new ProblemDetails
        {
            Status = 400,
            Detail = "Two fields are invalid.",
            Extensions =
            {
                ["errors"] = new[]
                {
                    new FieldError("email", "Enter a valid email."),
                    new FieldError("quantity", "Must be at least 1."),
                },
            },
        }),
        409 => TypedResults.Problem(
            statusCode: 409,
            detail: "This order was changed by someone else.",
            extensions: new Dictionary<string, object?> { ["code"] = "STALE_VERSION" }),
        var status => TypedResults.Problem(statusCode: status),
    };

    /// <summary>Slow response: watch <c>ApiLoadingService.loading()</c> drive the progress bar.</summary>
    private static async Task<Ok<object>> SlowAsync(int? ms, TimeProvider clock, CancellationToken ct)
    {
        var delay = Math.Clamp(ms ?? 1500, 0, 10_000);
        await Task.Delay(TimeSpan.FromMilliseconds(delay), clock, ct);
        return TypedResults.Ok<object>(new { delayedMs = delay });
    }
}
