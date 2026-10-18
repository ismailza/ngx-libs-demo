using Microsoft.AspNetCore.Identity;

namespace Demo.Api.Data.Entities;

/// <summary>An account of the Local provider. With Keycloak, users live in the realm instead.</summary>
public class AppUser : IdentityUser
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    /// <summary>Set from the injected <see cref="TimeProvider"/>, so tests control it.</summary>
    public required DateTimeOffset CreatedAt { get; set; }
}
