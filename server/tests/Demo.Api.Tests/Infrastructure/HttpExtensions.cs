using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Demo.Api.Tests.Infrastructure;

public sealed record Session(string AccessToken, string RefreshCookie);

public static class HttpExtensions
{
    public const string Password = "Passw0rd!";
    private const string CookieName = "refresh_token";

    public static Task<HttpResponseMessage> LoginAsync(this HttpClient client, string username, string password = Password) =>
        client.PostAsJsonAsync("/api/auth/login", new { username, password });

    /// <summary>Signs in and returns the access token and the refresh cookie.</summary>
    public static async Task<Session> SignInAsync(this HttpClient client, string username)
    {
        using var response = await client.LoginAsync(username);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new Session(body.GetProperty("accessToken").GetString()!, response.RefreshCookie()!);
    }

    public static Task<HttpResponseMessage> RefreshAsync(this HttpClient client, string? refreshCookie, string? userAgent = null)
    {
        var request = WithCookie(HttpMethod.Post, "/api/auth/refresh", refreshCookie);
        if (userAgent is not null)
        {
            request.Headers.UserAgent.ParseAdd(userAgent);
        }

        return client.SendAsync(request);
    }

    public static Task<HttpResponseMessage> RegisterAsync(this HttpClient client, object body) =>
        client.PostAsJsonAsync("/api/auth/register", body);

    public static Task<HttpResponseMessage> LogoutAsync(this HttpClient client, string refreshCookie) =>
        client.SendAsync(WithCookie(HttpMethod.Post, "/api/auth/logout", refreshCookie));

    public static Task<HttpResponseMessage> SendAsync(
        this HttpClient client, HttpMethod method, string url, string accessToken, object? body = null)
    {
        var request = new HttpRequestMessage(method, url)
        {
            Content = body is null ? null : JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    /// <summary>The raw <c>Set-Cookie</c> header for the refresh token, attributes included.</summary>
    public static string? RefreshSetCookie(this HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith($"{CookieName}=", StringComparison.Ordinal))
            : null;

    public static string? RefreshCookie(this HttpResponseMessage response)
    {
        var header = response.RefreshSetCookie();
        var value = header?[(CookieName.Length + 1)..]?.Split(';')[0];
        return string.IsNullOrEmpty(value) ? null : value;
    }

    public static async Task<JsonElement> JsonAsync(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    /// <summary>Decodes a JWT payload without verifying it — what the Angular adapter does.</summary>
    public static JsonElement JwtPayload(string token)
    {
        var payload = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');
        return JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload))).RootElement;
    }

    private static HttpRequestMessage WithCookie(HttpMethod method, string url, string? refreshCookie)
    {
        var request = new HttpRequestMessage(method, url);
        if (refreshCookie is not null)
        {
            request.Headers.Add("Cookie", $"{CookieName}={refreshCookie}");
        }

        return request;
    }
}
