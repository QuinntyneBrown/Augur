using Contoso.Orders.Domain.Notes;

namespace Contoso.Orders.Application.Notes.Queries;

/// <summary>Every note, oldest first.</summary>
public sealed record ListNotesQuery;

public sealed class ListNotesHandler(INoteRepository repository)
{
    public Task<IReadOnlyList<Note>> HandleAsync(ListNotesQuery query, CancellationToken cancellationToken) =>
        repository.ListAsync(cancellationToken);
}
