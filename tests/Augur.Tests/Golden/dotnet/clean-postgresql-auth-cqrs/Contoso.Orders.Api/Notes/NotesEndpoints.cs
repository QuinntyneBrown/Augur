using Contoso.Orders.Application.Notes;
using Contoso.Orders.Application.Notes.Commands;
using Contoso.Orders.Application.Notes.Queries;

namespace Contoso.Orders.Api.Notes;

/// <summary>The body of <c>POST /api/notes</c>.</summary>
public sealed record CreateNoteRequest(string? Title);

/// <summary>The notes endpoints under <c>/api/notes</c>.</summary>
public static class NotesEndpoints
{
    public static IEndpointRouteBuilder MapNotesEndpoints(this IEndpointRouteBuilder app)
    {
        var notes = app.MapGroup("/api/notes");

        notes.MapGet("/", async (ListNotesHandler handler, CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(new ListNotesQuery(), cancellationToken)));

        notes.MapPost("/", async (CreateNoteRequest request, CreateNoteHandler handler, CancellationToken cancellationToken) =>
        {
            var problems = NoteValidation.Problems(request.Title);
            if (problems.Count > 0)
            {
                return Results.ValidationProblem(problems);
            }

            var note = await handler.HandleAsync(new CreateNoteCommand(request.Title!), cancellationToken);
            return Results.Created($"/api/notes/{note.Id}", note);
        });

        return app;
    }
}
