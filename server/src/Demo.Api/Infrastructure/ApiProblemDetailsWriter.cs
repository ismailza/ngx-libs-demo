using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace Demo.Api.Infrastructure;

/// <summary>A field-level error, in the shape <c>ngx-api-client</c> expects.</summary>
public sealed record FieldError(string Field, string Message);

/// <summary>
/// Writes every error under <c>/api</c> as the RFC 9457 dialect <c>ngx-api-client</c> reads:
/// <c>{ type, title, status, detail, instance, code, timestamp, traceId, errors: [{ field, message }] }</c>.
/// </summary>
/// <remarks>
/// ASP.NET Core's default validation body is <c>errors: { "Field": ["…"] }</c>. The client's
/// <c>apiErrorInterceptor</c> copies <c>errors</c> as-is and types it as an array, so the
/// dictionary is flattened here.
/// </remarks>
public sealed class ApiProblemDetailsWriter(IOptions<JsonOptions> jsonOptions, TimeProvider clock) : IProblemDetailsWriter
{
    private static readonly HashSet<string> Reserved = ["code", "errors", "traceId", "timestamp"];

    public bool CanWrite(ProblemDetailsContext context) =>
        context.HttpContext.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);

    public ValueTask WriteAsync(ProblemDetailsContext context)
    {
        var http = context.HttpContext;
        var problem = context.ProblemDetails;
        var status = problem.Status ?? http.Response.StatusCode;
        var json = jsonOptions.Value.SerializerOptions;

        var body = new Dictionary<string, object?>
        {
            ["type"] = problem.Type ?? "about:blank",
            ["title"] = problem.Title ?? ReasonPhrases.GetReasonPhrase(status),
            ["status"] = status,
            ["detail"] = problem.Detail ?? DefaultDetail(status),
            ["instance"] = problem.Instance ?? http.Request.Path.Value,
            ["code"] = problem.Extensions.TryGetValue("code", out var code) ? code : CodeFor(status),
            ["timestamp"] = clock.GetUtcNow(),
            ["traceId"] = Activity.Current?.TraceId.ToString() ?? http.TraceIdentifier,
        };

        var errors = FieldErrors(problem, json);
        if (errors.Count > 0)
        {
            body["errors"] = errors;
        }

        foreach (var (key, value) in problem.Extensions)
        {
            if (!Reserved.Contains(key))
            {
                body.TryAdd(key, value);
            }
        }

        http.Response.StatusCode = status;
        return new ValueTask(http.Response.WriteAsJsonAsync(body, json, "application/problem+json"));
    }

    private static List<FieldError> FieldErrors(ProblemDetails problem, JsonSerializerOptions json)
    {
        if (problem is HttpValidationProblemDetails validation)
        {
            var naming = json.PropertyNamingPolicy ?? JsonNamingPolicy.CamelCase;
            return [.. validation.Errors.SelectMany(entry => entry.Value.Select(message =>
                new FieldError(string.IsNullOrEmpty(entry.Key) ? "" : naming.ConvertName(entry.Key), message)))];
        }

        return problem.Extensions.TryGetValue("errors", out var value) && value is IEnumerable<FieldError> list
            ? [.. list]
            : [];
    }

    private static string CodeFor(int status) => status switch
    {
        400 or 422 => "VALIDATION_ERROR",
        401 => "UNAUTHENTICATED",
        403 => "ACCESS_DENIED",
        404 => "ENTITY_NOT_FOUND",
        409 => "CONFLICT",
        429 => "RATE_LIMITED",
        502 or 503 or 504 => "EXTERNAL_SERVICE_ERROR",
        _ when status >= 500 => "INTERNAL_ERROR",
        _ => "ERROR",
    };

    private static string DefaultDetail(int status) => status switch
    {
        400 or 422 => "Some fields are invalid.",
        401 => "You need to sign in.",
        403 => "You don't have permission to do this.",
        404 => "Not found.",
        429 => "Too many requests. Please wait a moment.",
        503 => "The service is temporarily unavailable.",
        _ when status >= 500 => "Something went wrong on our side.",
        _ => "The request failed.",
    };
}

public static class ProblemDetailsSetup
{
    public static IServiceCollection AddDemoProblemDetails(this IServiceCollection services)
    {
        // Registered before AddProblemDetails() so it is picked over the default writer for /api.
        services.AddSingleton<IProblemDetailsWriter, ApiProblemDetailsWriter>();
        return services.AddProblemDetails();
    }
}
