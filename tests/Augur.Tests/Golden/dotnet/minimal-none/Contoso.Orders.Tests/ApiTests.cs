using System.Net;
using System.Net.Http.Json;
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
    public async Task A_created_note_is_listed()
    {
        using var app = WithEmptyStore();
        using var client = app.CreateClient();

        var created = await client.PostAsJsonAsync(new Uri("/api/notes", UriKind.Relative), new { title = "Ship order 42" });
        var notes = await client.GetFromJsonAsync<List<NoteBody>>(new Uri("/api/notes", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Contains(notes!, note => note.Title == "Ship order 42");
    }

    [Fact]
    public async Task A_note_without_a_title_is_rejected()
    {
        using var app = WithEmptyStore();
        using var client = app.CreateClient();

        var response = await client.PostAsJsonAsync(new Uri("/api/notes", UriKind.Relative), new { title = " " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private WebApplicationFactory<Program> WithEmptyStore()
    {
        return factory.WithWebHostBuilder(_ => { });
    }

    private sealed record NoteBody(Guid Id, string Title);

    private sealed record HealthBody(string Status);
}
