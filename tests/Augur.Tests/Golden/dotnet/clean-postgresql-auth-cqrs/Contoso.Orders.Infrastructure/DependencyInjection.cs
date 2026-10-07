using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Contoso.Orders.Application.Notes;
using Contoso.Orders.Infrastructure.Persistence;

namespace Contoso.Orders.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<NotesDbContext>(options => options.UseNpgsql(
            configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.")));
        services.AddScoped<INoteRepository, EfNoteRepository>();
        return services;
    }
}
