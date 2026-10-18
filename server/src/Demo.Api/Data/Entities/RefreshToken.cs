namespace Demo.Api.Data.Entities;

/// <summary>
/// One refresh token of the Local provider. Tokens issued from the same sign-in share a
/// <see cref="FamilyId"/>: rotating a token adds a member to the family, and reusing a rotated
/// token revokes the whole family.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string UserId { get; set; }
    public AppUser? User { get; set; }
    public Guid FamilyId { get; set; }

    /// <summary>SHA-256 of the token. The token itself is only ever in the cookie.</summary>
    public required string TokenHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? ReplacedById { get; set; }
    public string? CreatedByIp { get; set; }
    public string? UserAgent { get; set; }
}
