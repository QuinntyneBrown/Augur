using Microsoft.EntityFrameworkCore;
using Contoso.Orders.Api.Data;

namespace Contoso.Orders.Api.Features.Notes;

public static class NotesServices
{
    public static IServiceCollection AddNotes(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddDbContext<NotesDbContext>(options => options.UseSqlite(
            configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.")));
        services.AddScoped<INoteStore, EfNoteStore>();
        services.AddScoped<CreateNote.Handler>();
        services.AddScoped<ListNotes.Handler>();
        return services;
    }
}
