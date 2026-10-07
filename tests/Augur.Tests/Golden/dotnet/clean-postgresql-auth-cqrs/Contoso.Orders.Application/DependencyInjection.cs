using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Contoso.Orders.Application.Notes.Commands;
using Contoso.Orders.Application.Notes.Queries;

namespace Contoso.Orders.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<CreateNoteHandler>();
        services.AddScoped<ListNotesHandler>();
        return services;
    }
}
