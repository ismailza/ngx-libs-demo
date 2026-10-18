namespace Demo.Api.Data.Entities;

public class Note
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// The owner's <c>sub</c>. Deliberately not a foreign key to <see cref="AppUser"/>: with an
    /// external provider (Keycloak) the user has no row in this database.
    /// </summary>
    public required string OwnerId { get; set; }

    public required string Text { get; set; }
    /// <summary>Set from the injected <see cref="TimeProvider"/>, so tests control it.</summary>
    public required DateTimeOffset CreatedAt { get; set; }
}
