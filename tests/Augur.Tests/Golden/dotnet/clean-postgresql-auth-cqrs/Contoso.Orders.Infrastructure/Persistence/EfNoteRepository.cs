using Microsoft.EntityFrameworkCore;
using Contoso.Orders.Application.Notes;
using Contoso.Orders.Domain.Notes;

namespace Contoso.Orders.Infrastructure.Persistence;

/// <summary>Keeps notes in the database through <see cref="NotesDbContext"/>.</summary>
public sealed class EfNoteRepository(NotesDbContext db) : INoteRepository
{
    public async Task<IReadOnlyList<Note>> ListAsync(CancellationToken cancellationToken) =>
        await db.Notes.AsNoTracking().OrderBy(n => n.CreatedAt).ThenBy(n => n.Id).ToListAsync(cancellationToken);

    public async Task<Note> AddAsync(Note note, CancellationToken cancellationToken)
    {
        db.Notes.Add(note);
        await db.SaveChangesAsync(cancellationToken);
        return note;
    }
}
