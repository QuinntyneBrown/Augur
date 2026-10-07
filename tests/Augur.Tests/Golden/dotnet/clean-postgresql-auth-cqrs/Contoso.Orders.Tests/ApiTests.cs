using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Contoso.Orders.Tests;

public sealed class ApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Health_reports_healthy()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var health = await response.Content.ReadFromJsonAsync<HealthBody>();
        Assert.Equal("Healthy", health?.Status);
    }

    [Fact]
    public async Task Notes_require_an_authenticated_user()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/notes", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void The_api_refuses_to_start_outside_development_without_an_authority()
    {
        using var production = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));

        Assert.ThrowsAny<Exception>(() => production.CreateClient());
    }

    private sealed record HealthBody(string Status);
}
