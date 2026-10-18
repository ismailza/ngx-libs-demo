using System.Text.Json.Serialization;
using Demo.Api.Auth;
using Demo.Api.Data;
using Demo.Api.Features.Admin;
using Demo.Api.Features.Notes;
using Demo.Api.Features.Playground;
using Demo.Api.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDemoDatabase(builder.Configuration);
builder.AddDemoAuth();
builder.Services.AddDemoRateLimiting(builder.Configuration);
builder.Services.AddDemoProblemDetails();
builder.Services.AddValidation();
builder.Services.AddPlayground();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

// Behind nginx in Docker: trust X-Forwarded-* so rate limiting sees the client IP. Only from
// loopback and private networks, where a proxy sits; anyone else could spoof their IP.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Clear();
    options.KnownIPNetworks.Clear();
    foreach (var network in (string[])["127.0.0.0/8", "::1/128", "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16"])
    {
        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
    }
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/api/health");
app.MapDemoAuth();
app.MapNotesEndpoints();
app.MapAdminEndpoints();
app.MapPlaygroundEndpoints();

await app.MigrateAndSeedAsync();

await app.RunAsync();

/// <summary>Entry point, public for <c>WebApplicationFactory</c> in the integration tests.</summary>
public partial class Program;
