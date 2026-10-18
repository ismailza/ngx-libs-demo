using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Demo.Api.Auth;
using Demo.Api.Data;
using Demo.Api.Data.Entities;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Demo.Api.Features.Notes;

public sealed record NoteResponse(Guid Id, string Text, DateTimeOffset CreatedAt);

public sealed record CreateNoteRequest([Required, StringLength(500, MinimumLength = 1)] string Text);

/// <summary>
/// Per-user data, keyed by the token's <c>sub</c>: works with any auth provider and proves the
/// bearer token reaches the API.
/// </summary>
public static class NotesEndpoints
{
    public static void MapNotesEndpoints(this IEndpointRouteBuilder app)
    {
        var notes = app.MapGroup("/api/v1/notes").WithTags("Notes").RequireAuthorization();

        notes.MapGet("/", ListAsync);
        notes.MapPost("/", CreateAsync);
        notes.MapDelete("/{id:guid}", DeleteAsync);
    }

    private static async Task<Ok<List<NoteResponse>>> ListAsync(ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var ownerId = user.GetUserId();
        return TypedResults.Ok(await db.Notes
            .Where(n => n.OwnerId == ownerId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NoteResponse(n.Id, n.Text, n.CreatedAt))
            .ToListAsync(ct));
    }

    private static async Task<Results<Created<NoteResponse>, ValidationProblem>> CreateAsync(
        CreateNoteRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        // The attribute checks the raw length; a note of only spaces would be stored empty.
        var text = request.Text.Trim();
        if (text.Length == 0)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["text"] = ["Enter some text."],
            });
        }

        var note = new Note { OwnerId = user.GetUserId(), Text = text, CreatedAt = clock.GetUtcNow() };
        db.Notes.Add(note);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/v1/notes/{note.Id}", new NoteResponse(note.Id, note.Text, note.CreatedAt));
    }

    private static async Task<Results<NoContent, NotFound>> DeleteAsync(
        Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var ownerId = user.GetUserId();
        var deleted = await db.Notes
            .Where(n => n.Id == id && n.OwnerId == ownerId)
            .ExecuteDeleteAsync(ct);
        return deleted == 0 ? TypedResults.NotFound() : TypedResults.NoContent();
    }
}
