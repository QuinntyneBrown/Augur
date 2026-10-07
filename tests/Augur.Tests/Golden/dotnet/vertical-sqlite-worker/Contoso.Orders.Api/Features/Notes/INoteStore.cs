namespace Contoso.Orders.Api.Features.Notes;

/// <summary>Where notes are kept.</summary>
public interface INoteStore
{
    Task<IReadOnlyList<Note>> ListAsync(CancellationToken cancellationToken);

    Task<Note> AddAsync(Note note, CancellationToken cancellationToken);
}
