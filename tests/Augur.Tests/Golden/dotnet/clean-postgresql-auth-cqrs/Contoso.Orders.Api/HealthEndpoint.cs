namespace Contoso.Orders.Api;

/// <summary>An anonymous liveness check at <c>GET /api/health</c>.</summary>
public static class HealthEndpoint
{
    public static IEndpointRouteBuilder MapHealthEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", () => Results.Ok(new HealthStatus("Healthy"))).AllowAnonymous();
        return app;
    }
}

public sealed record HealthStatus(string Status);
