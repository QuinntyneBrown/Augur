namespace Contoso.Orders.Api.Notes;

/// <summary>The notes endpoints under <c>/api/notes</c>.</summary>
public static class NotesEndpoints
{
    public static IEndpointRouteBuilder MapNotesEndpoints(this IEndpointRouteBuilder app)
    {
        var notes = app.MapGroup("/api/notes");

        notes.MapGet("/", async (INoteStore store, CancellationToken cancellationToken) =>
            Results.Ok(await store.ListAsync(cancellationToken)));

        notes.MapPost("/", async (CreateNoteRequest request, INoteStore store, TimeProvider time, CancellationToken cancellationToken) =>
        {
            var problems = request.Problems();
            if (problems.Count > 0)
            {
                return Results.ValidationProblem(problems);
            }

            var note = await store.AddAsync(
                new Note { Id = Guid.NewGuid(), Title = request.Title!.Trim(), CreatedAt = time.GetUtcNow().UtcDateTime },
                cancellationToken);
            return Results.Created($"/api/notes/{note.Id}", note);
        });

        return app;
    }
}
