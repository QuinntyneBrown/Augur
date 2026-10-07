using Microsoft.EntityFrameworkCore;
using Contoso.Orders.Api.Features.Notes;

namespace Contoso.Orders.Api.Data;

/// <summary>
/// The application's database. Create the schema with EF Core migrations
/// (<c>dotnet ef migrations add Initial</c>); nothing connects to the database at startup.
/// </summary>
public sealed class NotesDbContext(DbContextOptions<NotesDbContext> options) : DbContext(options)
{
    public DbSet<Note> Notes => Set<Note>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Note>(note =>
        {
            note.HasKey(n => n.Id);
            note.Property(n => n.Title).HasMaxLength(Note.MaxTitleLength).IsRequired();
        });
    }
}
