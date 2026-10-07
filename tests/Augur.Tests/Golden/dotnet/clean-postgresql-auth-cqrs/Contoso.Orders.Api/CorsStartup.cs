namespace Contoso.Orders.Api;

/// <summary>
/// Enables CORS only for the origins listed in <c>Cors:AllowedOrigins</c>. With no origins configured, cross-origin
/// requests are refused. Any-origin access is never allowed.
/// </summary>
public static class CorsStartup
{
    private const string PolicyName = "ConfiguredOrigins";

    public static void Configure(WebApplicationBuilder builder)
    {
        var origins = AllowedOrigins(builder.Configuration);
        if (origins.Length > 0)
        {
            builder.Services.AddCors(options => options.AddPolicy(
                PolicyName,
                policy => policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));
        }
    }

    public static void Use(WebApplication app)
    {
        if (AllowedOrigins(app.Configuration).Length > 0)
        {
            app.UseCors(PolicyName);
        }
    }

    private static string[] AllowedOrigins(IConfiguration configuration) =>
        configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
}
