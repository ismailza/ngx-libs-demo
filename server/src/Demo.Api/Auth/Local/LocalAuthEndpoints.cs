using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Demo.Api.Data;
using Demo.Api.Data.Entities;
using Demo.Api.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace Demo.Api.Auth.Local;

public sealed record LoginRequest([Required] string Username, [Required] string Password);

public sealed record RegisterRequest(
    [Required, StringLength(50, MinimumLength = 3)] string Username,
    [Required, EmailAddress] string Email,
    [Required] string Password,
    [StringLength(100)] string? FirstName,
    [StringLength(100)] string? LastName);

/// <summary>The body <c>withHttpAuth()</c> reads by default.</summary>
public sealed record TokenResponse(string AccessToken, int ExpiresIn);

/// <summary>Field names match the adapter's default <c>mapProfile</c>.</summary>
public sealed record ProfileResponse(
    string Id,
    string? Username,
    string? Email,
    bool EmailVerified,
    string? FirstName,
    string? LastName,
    Dictionary<string, object> Attributes);

/// <summary>The backend half of <c>withHttpAuth()</c>: see docs/backend-contract.md.</summary>
public static class LocalAuthEndpoints
{
    /// <summary>Also published to the client through <c>/api/config</c>.</summary>
    public static readonly LocalEndpoints Paths = new(
        SignIn: "/api/auth/login",
        Refresh: "/api/auth/refresh",
        Logout: "/api/auth/logout",
        Profile: "/api/auth/me",
        Register: "/api/auth/register");

    public static void MapLocalAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Auth");

        auth.MapPost("/login", LoginAsync).RequireRateLimiting(RateLimitingSetup.AuthPolicy);
        auth.MapPost("/register", RegisterAsync).RequireRateLimiting(RateLimitingSetup.AuthPolicy);
        auth.MapPost("/refresh", RefreshAsync);
        auth.MapPost("/logout", LogoutAsync);
        auth.MapGet("/me", MeAsync).RequireAuthorization();
    }

    private static async Task<Results<Ok<TokenResponse>, ProblemHttpResult>> LoginAsync(
        LoginRequest request,
        UserManager<AppUser> users,
        SignInManager<AppUser> signIn,
        AccessTokenIssuer accessTokens,
        RefreshTokenStore refreshTokens,
        RefreshCookie cookie,
        HttpContext http,
        CancellationToken ct)
    {
        // Usernames cannot contain '@' (see LocalAuthSetup), so the input is one or the other.
        var user = request.Username.Contains('@', StringComparison.Ordinal)
            ? await users.FindByEmailAsync(request.Username)
            : await users.FindByNameAsync(request.Username);

        // One answer for "unknown user", "wrong password" and "locked out", so the endpoint
        // cannot be used to discover which accounts exist.
        if (user is null)
        {
            return InvalidCredentials();
        }

        var result = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            return InvalidCredentials();
        }

        var roles = await users.GetRolesAsync(user);
        cookie.Write(http.Response, await refreshTokens.IssueAsync(user, http, ct));

        var access = accessTokens.Issue(user, [.. roles]);
        return TypedResults.Ok(new TokenResponse(access.Token, access.ExpiresIn));
    }

    private static async Task<Results<Created, ValidationProblem>> RegisterAsync(
        RegisterRequest request,
        UserManager<AppUser> users,
        AppDbContext db,
        TimeProvider clock,
        CancellationToken ct)
    {
        var user = new AppUser
        {
            UserName = request.Username,
            Email = request.Email,
            FirstName = OptionalName(request.FirstName),
            LastName = OptionalName(request.LastName),
            CreatedAt = clock.GetUtcNow(),
        };

        // One transaction for the user and its role: an account without the `user` role would
        // sign in but fail every role check.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return TypedResults.ValidationProblem(result.Errors
                .GroupBy(e => FieldOf(e.Code))
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));
        }

        var role = await users.AddToRoleAsync(user, Roles.User);
        if (!role.Succeeded)
        {
            throw new InvalidOperationException(
                $"Adding the '{Roles.User}' role failed: {string.Join(", ", role.Errors.Select(e => e.Description))}");
        }

        await transaction.CommitAsync(ct);
        return TypedResults.Created(Paths.Profile);
    }

    /// <summary>Blank means "not given": stored as null, so the token omits the claim.</summary>
    private static string? OptionalName(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static async Task<Results<Ok<TokenResponse>, ProblemHttpResult>> RefreshAsync(
        UserManager<AppUser> users,
        AccessTokenIssuer accessTokens,
        RefreshTokenStore refreshTokens,
        RefreshCookie cookie,
        HttpContext http,
        CancellationToken ct)
    {
        if (cookie.Read(http.Request) is not { } value)
        {
            return NoSession();
        }

        var rotation = await refreshTokens.RotateAsync(value, http, ct);
        if (rotation is null)
        {
            cookie.Clear(http.Response);
            return NoSession();
        }

        cookie.Write(http.Response, rotation.Token);

        // Roles are re-read on every refresh, so a role change reaches the client within one
        // access-token lifetime without a new sign-in.
        var roles = await users.GetRolesAsync(rotation.User);
        var access = accessTokens.Issue(rotation.User, [.. roles]);
        return TypedResults.Ok(new TokenResponse(access.Token, access.ExpiresIn));
    }

    private static async Task<NoContent> LogoutAsync(
        RefreshTokenStore refreshTokens,
        RefreshCookie cookie,
        HttpContext http,
        CancellationToken ct)
    {
        // Driven by the cookie, not the bearer token: logging out must still work when the
        // access token has already expired.
        if (cookie.Read(http.Request) is { } value)
        {
            await refreshTokens.RevokeAsync(value, ct);
        }

        cookie.Clear(http.Response);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<ProfileResponse>, NotFound>> MeAsync(
        ClaimsPrincipal principal,
        UserManager<AppUser> users)
    {
        var user = await users.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        var roles = await users.GetRolesAsync(user);
        return TypedResults.Ok(new ProfileResponse(
            user.Id,
            user.UserName,
            user.Email,
            user.EmailConfirmed,
            user.FirstName,
            user.LastName,
            new Dictionary<string, object>
            {
                ["roles"] = roles,
                ["memberSince"] = user.CreatedAt,
            }));
    }

    private static string FieldOf(string identityErrorCode) => identityErrorCode switch
    {
        _ when identityErrorCode.StartsWith("Password", StringComparison.Ordinal) => "password",
        _ when identityErrorCode.Contains("Email", StringComparison.Ordinal) => "email",
        _ => "username",
    };

    private static ProblemHttpResult InvalidCredentials() =>
        TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid username or password.");

    private static ProblemHttpResult NoSession() =>
        TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "No active session.");
}
