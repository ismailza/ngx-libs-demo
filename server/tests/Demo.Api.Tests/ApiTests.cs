using System.Net;
using System.Text.Json;
using Demo.Api.Tests.Infrastructure;

namespace Demo.Api.Tests;

/// <summary>What <c>ngx-api-client</c> relies on: problem+json errors, paging, versions, retry signals.</summary>
public sealed class ApiTests(ApiFactory api)
{
    private readonly HttpClient _client = api.CreateApiClient();

    [Fact]
    public async Task Protected_endpoints_answer_401_as_problem_json()
    {
        using var response = await _client.GetAsync(new Uri("/api/v1/notes", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.JsonAsync();
        Assert.Equal("UNAUTHENTICATED", problem.GetProperty("code").GetString());
        Assert.Equal(401, problem.GetProperty("status").GetInt32());
        Assert.True(problem.TryGetProperty("timestamp", out _));
        Assert.True(problem.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task Validation_errors_are_a_field_message_array()
    {
        var session = await _client.SignInAsync("alice");

        using var response = await _client.SendAsync(HttpMethod.Post, "/api/v1/notes", session.AccessToken, new { text = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.JsonAsync();
        Assert.Equal("VALIDATION_ERROR", problem.GetProperty("code").GetString());
        var error = Assert.Single(problem.GetProperty("errors").EnumerateArray());
        Assert.Equal("text", error.GetProperty("field").GetString());
    }

    [Fact]
    public async Task A_note_of_only_spaces_is_rejected()
    {
        var session = await _client.SignInAsync("alice");

        using var response = await _client.SendAsync(HttpMethod.Post, "/api/v1/notes", session.AccessToken, new { text = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = Assert.Single((await response.JsonAsync()).GetProperty("errors").EnumerateArray());
        Assert.Equal("text", error.GetProperty("field").GetString());
    }

    [Fact]
    public async Task Notes_are_scoped_to_the_caller()
    {
        var alice = await _client.SignInAsync("alice");
        var admin = await _client.SignInAsync("admin");

        using var created = await _client.SendAsync(HttpMethod.Post, "/api/v1/notes", alice.AccessToken, new { text = "alice's secret" });
        var id = (await created.JsonAsync()).GetProperty("id").GetString();
        using var adminList = await _client.SendAsync(HttpMethod.Get, "/api/v1/notes", admin.AccessToken);
        using var adminDelete = await _client.SendAsync(HttpMethod.Delete, $"/api/v1/notes/{id}", admin.AccessToken);
        using var aliceDelete = await _client.SendAsync(HttpMethod.Delete, $"/api/v1/notes/{id}", alice.AccessToken);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.DoesNotContain((await adminList.JsonAsync()).EnumerateArray(), n => n.GetProperty("id").GetString() == id);
        Assert.Equal(HttpStatusCode.NotFound, adminDelete.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, aliceDelete.StatusCode);
    }

    [Fact]
    public async Task Admin_endpoints_require_the_admin_role()
    {
        var alice = await _client.SignInAsync("alice");

        using var response = await _client.SendAsync(HttpMethod.Get, "/api/v1/admin/users", alice.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("ACCESS_DENIED", (await response.JsonAsync()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Users_are_paged_in_the_getPage_shape()
    {
        var admin = await _client.SignInAsync("admin");

        using var response = await _client.SendAsync(HttpMethod.Get, "/api/v1/admin/users?page=0&size=1", admin.AccessToken);
        var body = await response.JsonAsync();

        Assert.Single(body.GetProperty("content").EnumerateArray());
        var page = body.GetProperty("page");
        Assert.Equal(0, page.GetProperty("number").GetInt32());
        Assert.Equal(1, page.GetProperty("size").GetInt32());
        Assert.True(page.GetProperty("totalElements").GetInt32() >= 2);
        Assert.Equal(page.GetProperty("totalElements").GetInt32(), page.GetProperty("totalPages").GetInt32());
    }

    [Fact]
    public async Task Revoking_sessions_refuses_the_users_next_refresh()
    {
        var alice = await _client.SignInAsync("alice");
        var admin = await _client.SignInAsync("admin");
        var aliceId = HttpExtensions.JwtPayload(alice.AccessToken).GetProperty("sub").GetString();

        using var revoke = await _client.SendAsync(HttpMethod.Post, $"/api/v1/admin/users/{aliceId}/revoke-sessions", admin.AccessToken);
        using var refresh = await _client.RefreshAsync(alice.RefreshCookie);

        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Flaky_endpoint_fails_then_succeeds_and_sends_retry_after_on_429()
    {
        var session = await _client.SignInAsync("alice");
        var url = $"/api/v1/playground/flaky?key={Guid.NewGuid()}&failures=1&status=429";

        using var first = await _client.SendAsync(HttpMethod.Get, url, session.AccessToken);
        using var second = await _client.SendAsync(HttpMethod.Get, url, session.AccessToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, first.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(1), first.Headers.RetryAfter?.Delta);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(2, (await second.JsonAsync()).GetProperty("attempts").GetInt32());
    }

    [Theory]
    [InlineData("/api/v1/playground/version", 1)]
    [InlineData("/api/v2/playground/version", 2)]
    public async Task Versions_are_served_side_by_side(string url, int version)
    {
        var session = await _client.SignInAsync("alice");

        using var response = await _client.SendAsync(HttpMethod.Get, url, session.AccessToken);

        Assert.Equal(version, (await response.JsonAsync()).GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task Custom_error_codes_pass_through()
    {
        var session = await _client.SignInAsync("alice");

        using var response = await _client.SendAsync(HttpMethod.Get, "/api/v1/playground/status/409", session.AccessToken);
        var problem = await response.JsonAsync();

        Assert.Equal("STALE_VERSION", problem.GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.String, problem.GetProperty("detail").ValueKind);
    }
}
