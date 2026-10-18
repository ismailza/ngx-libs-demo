using System.Security.Cryptography;
using System.Text;
using Demo.Api.Data;
using Demo.Api.Data.Entities;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Demo.Api.Auth.Local;

public sealed record IssuedRefreshToken(string Value, DateTimeOffset ExpiresAt);

public sealed record RotationResult(AppUser User, IssuedRefreshToken Token);

/// <summary>
/// Opaque refresh tokens with rotation and reuse detection. Only a SHA-256 hash is stored,
/// so a database leak does not leak live sessions.
/// </summary>
public sealed partial class RefreshTokenStore(
    AppDbContext db,
    IOptions<RefreshTokenOptions> options,
    TimeProvider clock,
    ILogger<RefreshTokenStore> logger)
{
    private readonly RefreshTokenOptions _options = options.Value;

    /// <summary>Starts a new session (a new token family).</summary>
    public Task<IssuedRefreshToken> IssueAsync(AppUser user, HttpContext http, CancellationToken ct) =>
        AddAsync(user.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), http, ct);

    /// <summary>
    /// Exchanges a refresh token for a new one in the same family. Returns <c>null</c> when the
    /// token is unknown, expired or revoked.
    /// </summary>
    /// <remarks>
    /// Lockout is not checked here on purpose: anyone can lock an account by typing wrong
    /// passwords, so honouring it would let them sign the owner out. Lockout only blocks
    /// password logins; to end someone's sessions, revoke them.
    /// </remarks>
    public async Task<RotationResult?> RotateAsync(string value, HttpContext http, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var hash = Hash(value);
        var token = await db.RefreshTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (token?.User is null || token.ExpiresAt <= now)
        {
            return null;
        }

        var nextId = Guid.CreateVersion7();

        if (token.RevokedAt is null)
        {
            // Claim the token atomically: of two concurrent refreshes with the same cookie,
            // exactly one wins the update; the other falls through to the grace check below.
            var claimed = await db.RefreshTokens
                .Where(t => t.Id == token.Id && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.RevokedAt, now)
                    .SetProperty(t => t.ReplacedById, nextId), ct);

            if (claimed == 1)
            {
                return new RotationResult(token.User, await AddAsync(token.UserId, token.FamilyId, nextId, http, ct));
            }

            await db.Entry(token).ReloadAsync(ct);
        }

        // The token was already rotated. Shortly after rotation, from the browser that holds
        // it, that is a race between tabs. Later, or from another client, it means someone is
        // replaying an old token, so the whole session is killed.
        var rotatedRecently = token.ReplacedById is not null
            && token.RevokedAt is { } revokedAt
            && now - revokedAt <= TimeSpan.FromSeconds(_options.ReuseGraceSeconds);

        if (rotatedRecently && IsSameClient(token, http) && await IsFamilyActiveAsync(token.FamilyId, now, ct))
        {
            return new RotationResult(token.User, await AddAsync(token.UserId, token.FamilyId, nextId, http, ct));
        }

        if (token.ReplacedById is not null)
        {
            LogReuse(token.UserId, token.FamilyId);
            await RevokeFamilyAsync(token.FamilyId, ct);
        }

        return null;
    }

    /// <summary>Ends the session the token belongs to. Unknown tokens are ignored.</summary>
    public async Task RevokeAsync(string value, CancellationToken ct)
    {
        var hash = Hash(value);
        var familyId = await db.RefreshTokens
            .Where(t => t.TokenHash == hash)
            .Select(t => (Guid?)t.FamilyId)
            .SingleOrDefaultAsync(ct);

        if (familyId is { } id)
        {
            await RevokeFamilyAsync(id, ct);
        }
    }

    /// <summary>Signs a user out everywhere. Their tabs sign out on their next refresh.</summary>
    public Task<int> RevokeAllAsync(string userId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        return db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }

    private Task<int> RevokeFamilyAsync(Guid familyId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        return db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }

    private Task<bool> IsFamilyActiveAsync(Guid familyId, DateTimeOffset now, CancellationToken ct) =>
        db.RefreshTokens.AnyAsync(t => t.FamilyId == familyId && t.RevokedAt == null && t.ExpiresAt > now, ct);

    private async Task<IssuedRefreshToken> AddAsync(
        string userId, Guid familyId, Guid id, HttpContext http, CancellationToken ct)
    {
        var value = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var now = clock.GetUtcNow();
        var expiresAt = now.AddDays(_options.Days);

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = id,
            UserId = userId,
            FamilyId = familyId,
            TokenHash = Hash(value),
            CreatedAt = now,
            ExpiresAt = expiresAt,
            CreatedByIp = IpOf(http),
            UserAgent = UserAgentOf(http),
        });
        await db.SaveChangesAsync(ct);

        return new IssuedRefreshToken(value, expiresAt);
    }

    /// <summary>
    /// Tabs of one browser share its IP and User-Agent; a stolen cookie replayed elsewhere
    /// usually doesn't. Both can be faked, so this narrows the grace window rather than closing it.
    /// </summary>
    private static bool IsSameClient(RefreshToken token, HttpContext http) =>
        string.Equals(token.CreatedByIp, IpOf(http), StringComparison.Ordinal)
        && string.Equals(token.UserAgent, UserAgentOf(http), StringComparison.Ordinal);

    private static string? IpOf(HttpContext http) => http.Connection.RemoteIpAddress?.ToString();

    private static string UserAgentOf(HttpContext http)
    {
        var userAgent = http.Request.Headers.UserAgent.ToString();
        return userAgent.Length > 512 ? userAgent[..512] : userAgent;
    }

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Refresh token reuse detected for user {UserId}; revoking session {FamilyId}")]
    private partial void LogReuse(string userId, Guid familyId);
}
