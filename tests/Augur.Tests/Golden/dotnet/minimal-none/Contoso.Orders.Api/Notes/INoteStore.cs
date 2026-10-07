namespace Contoso.Orders.Api.Notes;

/// <summary>Where notes are kept.</summary>
public interface INoteStore
{
    Task<IReadOnlyList<Note>> ListAsync(CancellationToken cancellationToken);

    Task<Note> AddAsync(Note note, CancellationToken cancellationToken);
}
