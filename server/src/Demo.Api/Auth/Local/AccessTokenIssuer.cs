using System.Text;
using Demo.Api.Data.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Demo.Api.Auth.Local;

public sealed record AccessToken(string Token, int ExpiresIn);

/// <summary>Issues the JWT the Angular adapter decodes for <c>claims()</c> and <c>roles()</c>.</summary>
public sealed class AccessTokenIssuer(IOptions<JwtOptions> options, TimeProvider clock)
{
    private static readonly JsonWebTokenHandler Handler = new();
    private readonly JwtOptions _options = options.Value;

    public static SymmetricSecurityKey KeyFrom(JwtOptions options) =>
        new(Encoding.UTF8.GetBytes(options.SigningKey));

    public AccessToken Issue(AppUser user, IReadOnlyCollection<string> roles)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var lifetime = TimeSpan.FromMinutes(_options.AccessTokenMinutes);

        var claims = new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Sub] = user.Id,
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
            ["preferred_username"] = user.UserName ?? "",
            [JwtRegisteredClaimNames.Email] = user.Email ?? "",
            // Always an array, even with a single role, so every consumer can read it as a list.
            ["roles"] = roles.ToArray(),
        };
        if (user.FirstName is not null)
        {
            claims[JwtRegisteredClaimNames.GivenName] = user.FirstName;
        }

        if (user.LastName is not null)
        {
            claims[JwtRegisteredClaimNames.FamilyName] = user.LastName;
        }

        var token = Handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now + lifetime,
            Claims = claims,
            SigningCredentials = new SigningCredentials(KeyFrom(_options), SecurityAlgorithms.HmacSha256),
        });

        return new AccessToken(token, (int)lifetime.TotalSeconds);
    }
}
