namespace Contoso.Orders.Api.Notes;

public static class NotesServices
{
    public static IServiceCollection AddNotes(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<INoteStore, InMemoryNoteStore>();
        return services;
    }
}
