using Demo.Api.Auth;
using Demo.Api.Auth.Local;
using Demo.Api.Data;
using Demo.Api.Data.Entities;
using Demo.Api.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Demo.Api.Features.Admin;

public sealed record UserSummary(
    string Id,
    string? Username,
    string? Email,
    IList<string> Roles,
    int ActiveSessions,
    DateTimeOffset CreatedAt);

/// <summary>
/// User and session management for the Local provider. Role-protected: the client checks
/// <c>data.auth</c> for UX, this policy is the security boundary. With Keycloak, users and
/// sessions are managed in Keycloak instead, so these endpoints are not mapped.
/// </summary>
public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        if (app.Configuration.GetAuthProvider() != AuthProvider.Local)
        {
            return;
        }

        var admin = app.MapGroup("/api/v1/admin").WithTags("Admin").RequireAuthorization(Policies.Admin);

        admin.MapGet("/users", ListUsersAsync);
        // Lets you watch a remote sign-out: the user's tab signs out on its next refresh.
        admin.MapPost("/users/{id}/revoke-sessions", RevokeSessionsAsync);
    }

    private static async Task<Ok<PagedResponse<UserSummary>>> ListUsersAsync(
        int? page, int? size,
        AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var request = PageRequest.From(page, size);
        var now = clock.GetUtcNow();

        var total = await db.Users.CountAsync(ct);
        var slice = await db.Users
            .OrderBy(u => u.UserName)
            .Skip(request.Skip)
            .Take(request.Size)
            .ToListAsync(ct);

        var ids = slice.Select(u => u.Id).ToList();
        var sessions = await db.RefreshTokens
            .Where(t => ids.Contains(t.UserId) && t.RevokedAt == null && t.ExpiresAt > now)
            .GroupBy(t => t.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Select(t => t.FamilyId).Distinct().Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, ct);

        // One query for the whole page, not one GetRolesAsync() per user.
        var roles = (await db.UserRoles
                .Where(ur => ids.Contains(ur.UserId))
                .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
                .OrderBy(x => x.Name)
                .ToListAsync(ct))
            .ToLookup(x => x.UserId, x => x.Name!);

        var content = slice
            .Select(user => new UserSummary(
                user.Id,
                user.UserName,
                user.Email,
                [.. roles[user.Id]],
                sessions.GetValueOrDefault(user.Id),
                user.CreatedAt))
            .ToList();

        return TypedResults.Ok(request.ToResponse<UserSummary>(content, total));
    }

    private static async Task<Results<Ok<int>, NotFound>> RevokeSessionsAsync(
        string id, UserManager<AppUser> users, RefreshTokenStore tokens, CancellationToken ct)
    {
        if (await users.FindByIdAsync(id) is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(await tokens.RevokeAllAsync(id, ct));
    }
}
