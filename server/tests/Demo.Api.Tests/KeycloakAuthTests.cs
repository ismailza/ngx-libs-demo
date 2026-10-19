using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Demo.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Demo.Api.Tests;

/// <summary>
/// The API with <c>Auth:Provider=Keycloak</c>. No Keycloak runs here: the API trusts a test RSA
/// key instead of the realm's, and the tests mint tokens shaped like the realm's (see
/// <c>keycloak/demo-realm.json</c>). Same database as the other tests; one host for the class.
/// </summary>
public sealed class KeycloakApi : IAsyncDisposable
{
    public const string KeycloakUrl = "http://keycloak.test";
    public const string Issuer = $"{KeycloakUrl}/realms/demo";

    public static readonly RsaSecurityKey RealmKey = new(RSA.Create(2048)) { KeyId = "realm" };

    private readonly WebApplicationFactory<Program> _factory;

    public KeycloakApi(ApiFactory api)
    {
        _factory = api.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Auth:Provider", "Keycloak");
            builder.UseSetting("Auth:Keycloak:Url", KeycloakUrl);
            builder.ConfigureTestServices(services =>
                services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    // A static configuration replaces fetching the realm's discovery document.
                    var configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
                    configuration.SigningKeys.Add(RealmKey);
                    options.Configuration = configuration;
                }));
        });
        Client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
    }

    public HttpClient Client { get; }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _factory.DisposeAsync();
    }
}

public sealed class KeycloakAuthTests(KeycloakApi keycloak) : IClassFixture<KeycloakApi>
{
    private const string KeycloakUrl = KeycloakApi.KeycloakUrl;
    private const string Issuer = KeycloakApi.Issuer;

    private static readonly RsaSecurityKey RealmKey = KeycloakApi.RealmKey;
    private static readonly RsaSecurityKey OtherKey = new(RSA.Create(2048)) { KeyId = "other" };

    private readonly HttpClient _client = keycloak.Client;

    [Fact]
    public async Task Config_endpoint_tells_the_client_to_use_the_keycloak_adapter()
    {
        var config = await _client.GetFromJsonAsync<JsonElement>("/api/config", TestContext.Current.CancellationToken);

        var auth = config.GetProperty("auth");
        Assert.Equal("keycloak", auth.GetProperty("provider").GetString());
        var keycloak = auth.GetProperty("keycloak");
        Assert.Equal(KeycloakUrl, keycloak.GetProperty("url").GetString());
        Assert.Equal("demo", keycloak.GetProperty("realm").GetString());
        Assert.Equal("demo-web", keycloak.GetProperty("clientId").GetString());
        Assert.False(auth.TryGetProperty("local", out _));
        var features = config.GetProperty("features");
        Assert.False(features.GetProperty("admin").GetBoolean());
        Assert.False(features.GetProperty("signUpForm").GetBoolean());
    }

    [Fact]
    public async Task Health_reports_an_unreachable_keycloak()
    {
        using var response = await _client.GetAsync(new Uri("/api/health", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task A_realm_token_is_accepted_and_notes_are_keyed_by_its_sub()
    {
        var owner = Token(sub: Guid.NewGuid().ToString());
        var stranger = Token(sub: Guid.NewGuid().ToString());

        using var created = await _client.SendAsync(HttpMethod.Post, "/api/v1/notes", owner, new { text = "from keycloak" });
        var id = (await created.JsonAsync()).GetProperty("id").GetString();
        using var ownList = await _client.SendAsync(HttpMethod.Get, "/api/v1/notes", owner);
        using var otherList = await _client.SendAsync(HttpMethod.Get, "/api/v1/notes", stranger);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Contains((await ownList.JsonAsync()).EnumerateArray(), n => n.GetProperty("id").GetString() == id);
        Assert.DoesNotContain((await otherList.JsonAsync()).EnumerateArray(), n => n.GetProperty("id").GetString() == id);
    }

    [Fact]
    public async Task A_token_without_the_api_audience_is_rejected()
    {
        // What a realm without the audience mapper issues to demo-web.
        using var response = await _client.SendAsync(HttpMethod.Get, "/api/v1/notes", Token(audience: ["account"]));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("UNAUTHENTICATED", (await response.JsonAsync()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_token_from_another_realm_is_rejected()
    {
        using var response = await _client.SendAsync(HttpMethod.Get, "/api/v1/notes", Token(issuer: $"{KeycloakUrl}/realms/master"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_token_signed_with_another_key_is_rejected()
    {
        using var response = await _client.SendAsync(HttpMethod.Get, "/api/v1/notes", Token(key: OtherKey));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_expired_token_is_rejected()
    {
        // Minted already expired: lifetime checks use the system clock, not api.Clock.
        using var response = await _client.SendAsync(HttpMethod.Get, "/api/v1/notes", Token(age: TimeSpan.FromMinutes(10)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/auth/login")]
    [InlineData("POST", "/api/auth/refresh")]
    [InlineData("POST", "/api/auth/register")]
    [InlineData("GET", "/api/v1/admin/users")]
    public async Task Local_only_endpoints_are_not_mapped(string method, string url)
    {
        // Even for an admin: users and sessions are managed in Keycloak.
        using var response = await _client.SendAsync(new HttpMethod(method), url, Token(roles: ["user", "admin"]), new { });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>An access token with the claims the demo realm puts in one.</summary>
    private static string Token(
        string sub = "8f9c4a52-0000-4000-8000-000000000001",
        string[]? roles = null,
        string[]? audience = null,
        string issuer = Issuer,
        RsaSecurityKey? key = null,
        TimeSpan age = default)
    {
        roles ??= ["default-roles-demo", "user"];
        // The system clock, like the validator: api.Clock may have been moved by other tests.
        var now = DateTime.UtcNow - age;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(5),
            SigningCredentials = new SigningCredentials(key ?? RealmKey, SecurityAlgorithms.RsaSha256),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = sub,
                ["aud"] = audience ?? ["demo-api", "account"],
                ["azp"] = "demo-web",
                ["typ"] = "Bearer",
                ["preferred_username"] = "alice",
                ["roles"] = roles,
                ["realm_access"] = new Dictionary<string, object> { ["roles"] = roles },
            },
        });
    }
}
