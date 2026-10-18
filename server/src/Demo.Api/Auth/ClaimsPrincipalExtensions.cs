using System.Security.Claims;

namespace Demo.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The caller's id: the token's <c>sub</c>. Data is keyed by it rather than by an Identity
    /// user row, so it stays valid whichever provider issued the token.
    /// </summary>
    public static string GetUserId(this ClaimsPrincipal user) =>
        user.FindFirstValue("sub") ?? throw new InvalidOperationException("The access token has no 'sub' claim.");
}
