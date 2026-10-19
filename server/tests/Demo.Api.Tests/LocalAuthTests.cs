using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Demo.Api.Tests.Infrastructure;

namespace Demo.Api.Tests;

/// <summary>The backend contract of <c>withHttpAuth()</c>.</summary>
public sealed class LocalAuthTests(ApiFactory api)
{
    private readonly HttpClient _client = api.CreateApiClient();

    [Fact]
    public async Task Config_endpoint_tells_the_client_to_use_the_http_adapter()
    {
        var config = await _client.GetFromJsonAsync<JsonElement>("/api/config", TestContext.Current.CancellationToken);

        var auth = config.GetProperty("auth");
        Assert.Equal("local", auth.GetProperty("provider").GetString());
        Assert.Equal("/api/auth/login", auth.GetProperty("local").GetProperty("endpoints").GetProperty("signIn").GetString());
        Assert.False(auth.TryGetProperty("keycloak", out _));
        var features = config.GetProperty("features");
        Assert.True(features.GetProperty("admin").GetBoolean());
        Assert.True(features.GetProperty("signUpForm").GetBoolean());
    }

    [Fact]
    public async Task Login_returns_a_token_and_a_hardened_refresh_cookie()
    {
        using var response = await _client.LoginAsync("alice");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.JsonAsync();
        Assert.False(string.IsNullOrEmpty(body.GetProperty("accessToken").GetString()));
        Assert.Equal(300, body.GetProperty("expiresIn").GetInt32());

        var cookie = response.RefreshSetCookie()?.ToLowerInvariant();
        Assert.NotNull(cookie);
        Assert.Contains("httponly", cookie);
        Assert.Contains("secure", cookie);
        Assert.Contains("samesite=strict", cookie);
        Assert.Contains("path=/api/auth", cookie);
    }

    [Fact]
    public async Task Access_token_has_sub_and_roles_as_an_array_even_with_one_role()
    {
        var session = await _client.SignInAsync("alice");

        var claims = HttpExtensions.JwtPayload(session.AccessToken);
        Assert.False(string.IsNullOrEmpty(claims.GetProperty("sub").GetString()));
        Assert.Equal("alice", claims.GetProperty("preferred_username").GetString());
        Assert.Equal(JsonValueKind.Array, claims.GetProperty("roles").ValueKind);
        Assert.Equal(["user"], claims.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
    }

    [Theory]
    [InlineData("alice", "wrong-password")]
    [InlineData("nobody", "Passw0rd!")]
    public async Task Bad_credentials_are_one_indistinguishable_401(string username, string password)
    {
        using var response = await _client.LoginAsync(username, password);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Invalid username or password.", (await response.JsonAsync()).GetProperty("detail").GetString());
        Assert.Null(response.RefreshSetCookie());
    }

    [Fact]
    public async Task Refresh_without_a_cookie_is_401()
    {
        using var response = await _client.RefreshAsync(null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_rotates_the_cookie()
    {
        var session = await _client.SignInAsync("alice");

        using var response = await _client.RefreshAsync(session.RefreshCookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(session.RefreshCookie, response.RefreshCookie());
        Assert.NotEqual(session.AccessToken, (await response.JsonAsync()).GetProperty("accessToken").GetString());
    }

    [Fact]
    public async Task Replaying_a_just_rotated_cookie_is_tolerated_for_concurrent_tabs()
    {
        var session = await _client.SignInAsync("alice");
        using var first = await _client.RefreshAsync(session.RefreshCookie);

        using var second = await _client.RefreshAsync(session.RefreshCookie);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task Replaying_a_rotated_cookie_after_the_grace_window_revokes_the_session()
    {
        var session = await _client.SignInAsync("alice");
        using var rotated = await _client.RefreshAsync(session.RefreshCookie);
        var current = rotated.RefreshCookie();

        api.Clock.Advance(TimeSpan.FromSeconds(11));
        using var replay = await _client.RefreshAsync(session.RefreshCookie);
        using var legitimate = await _client.RefreshAsync(current);

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, legitimate.StatusCode);
    }

    [Fact]
    public async Task Replaying_a_just_rotated_cookie_from_another_client_revokes_the_session()
    {
        var session = await _client.SignInAsync("alice");
        using var rotated = await _client.RefreshAsync(session.RefreshCookie);
        var current = rotated.RefreshCookie();

        using var replay = await _client.RefreshAsync(session.RefreshCookie, userAgent: "attacker/1.0");
        using var legitimate = await _client.RefreshAsync(current);

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, legitimate.StatusCode);
    }

    [Fact]
    public async Task Login_accepts_the_email_instead_of_the_username()
    {
        using var response = await _client.LoginAsync("alice@demo.local");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Register_rejects_a_username_that_looks_like_an_email()
    {
        // Otherwise it could shadow alice's email as a login name.
        using var response = await _client.RegisterAsync(
            new { username = "alice@demo.local", email = "mallory@demo.local", password = HttpExtensions.Password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.JsonAsync()).GetProperty("errors").EnumerateArray().ToList();
        Assert.Contains(errors, error => error.GetProperty("field").GetString() == "username");
    }

    [Fact]
    public async Task Register_gives_the_user_role_and_stores_blank_names_as_null()
    {
        var username = $"new-{Guid.NewGuid():N}"[..20];
        using var register = await _client.RegisterAsync(new
        {
            username,
            email = $"{username}@demo.local",
            password = HttpExtensions.Password,
            firstName = "",
            lastName = "  ",
        });
        register.EnsureSuccessStatusCode();

        var session = await _client.SignInAsync(username);
        var claims = HttpExtensions.JwtPayload(session.AccessToken);
        using var me = await _client.SendAsync(HttpMethod.Get, "/api/auth/me", session.AccessToken);
        var profile = await me.JsonAsync();

        Assert.Equal(["user"], claims.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        Assert.False(claims.TryGetProperty("given_name", out _));
        Assert.False(claims.TryGetProperty("family_name", out _));
        // Null fields are omitted from responses.
        Assert.False(profile.TryGetProperty("firstName", out _));
        Assert.False(profile.TryGetProperty("lastName", out _));
        // The test clock stands still, so wall-clock time would be off by far more than 1 ms.
        // (1 ms because Postgres keeps microseconds and the clock has 100 ns ticks.)
        Assert.Equal(
            api.Clock.GetUtcNow(),
            profile.GetProperty("attributes").GetProperty("memberSince").GetDateTimeOffset(),
            TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task Locking_an_account_with_wrong_passwords_does_not_end_its_sessions()
    {
        var username = $"lock-{Guid.NewGuid():N}"[..20];
        using var register = await _client.RegisterAsync(
            new { username, email = $"{username}@demo.local", password = HttpExtensions.Password });
        register.EnsureSuccessStatusCode();
        var session = await _client.SignInAsync(username);

        for (var i = 0; i < 5; i++)
        {
            using var wrong = await _client.LoginAsync(username, "wrong-password");
        }

        using var locked = await _client.LoginAsync(username);
        using var refresh = await _client.RefreshAsync(session.RefreshCookie);

        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_the_session_and_clears_the_cookie()
    {
        var session = await _client.SignInAsync("alice");

        using var logout = await _client.LogoutAsync(session.RefreshCookie);
        using var refresh = await _client.RefreshAsync(session.RefreshCookie);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Contains("expires=thu, 01 jan 1970", logout.RefreshSetCookie()?.ToLowerInvariant(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Profile_matches_the_adapter_default_mapProfile()
    {
        var session = await _client.SignInAsync("admin");

        using var response = await _client.SendAsync(HttpMethod.Get, "/api/auth/me", session.AccessToken);
        var profile = await response.JsonAsync();

        Assert.Equal("admin", profile.GetProperty("username").GetString());
        Assert.Equal("admin@demo.local", profile.GetProperty("email").GetString());
        Assert.True(profile.GetProperty("emailVerified").GetBoolean());
        Assert.Equal("Ada", profile.GetProperty("firstName").GetString());
        Assert.Equal(HttpExtensions.JwtPayload(session.AccessToken).GetProperty("sub").GetString(), profile.GetProperty("id").GetString());
    }

    [Fact]
    public async Task Register_reports_identity_errors_per_field()
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new { username = "bob", email = "bob@demo.local", password = "weak" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.JsonAsync()).GetProperty("errors").EnumerateArray().ToList();
        Assert.NotEmpty(errors);
        Assert.All(errors, error => Assert.Equal("password", error.GetProperty("field").GetString()));
    }
}
