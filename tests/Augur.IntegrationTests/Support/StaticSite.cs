using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

namespace Augur.IntegrationTests.Support;

/// <summary>
/// Serves a built Angular app the way a static host would: files as-is, unknown non-/api paths fall back to the
/// index page, and /api returns 404 because no API is running.
/// </summary>
public sealed class StaticSite : IAsyncDisposable
{
    private readonly WebApplication _app;

    private StaticSite(WebApplication app, string url)
    {
        _app = app;
        Url = url;
    }

    public string Url { get; }

    public static async Task<StaticSite> StartAsync(string root, string indexFile = "index.html")
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { WebRootPath = root });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, 0));
        var app = builder.Build();
        var files = new PhysicalFileProvider(root);
        app.UseStaticFiles(new StaticFileOptions { FileProvider = files });
        app.MapGet("/api/{**rest}", () => Results.NotFound());
        app.MapFallback(async context =>
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.SendFileAsync(files.GetFileInfo(indexFile));
        });
        await app.StartAsync();
        return new StaticSite(app, app.Urls.Single());
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}

/// <summary>
/// A minimal OpenID Connect authority: a discovery document and an authorization page that only records that the
/// browser arrived. Enough to prove the app starts the Authorization Code flow with PKCE.
/// </summary>
public sealed class StubAuthority : IAsyncDisposable
{
    private readonly WebApplication _app;

    private StubAuthority(WebApplication app, string url)
    {
        _app = app;
        Url = url;
    }

    public string Url { get; }

    public static async Task<StubAuthority> StartAsync()
    {
        var port = FreePort();
        var url = $"http://localhost:{port}";
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.ListenLocalhost(port));
        builder.Services.AddCors();
        var app = builder.Build();
        app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
        app.MapGet("/.well-known/openid-configuration", () => Results.Json(new Dictionary<string, object>
        {
            ["issuer"] = url,
            ["authorization_endpoint"] = $"{url}/authorize",
            ["token_endpoint"] = $"{url}/token",
            ["end_session_endpoint"] = $"{url}/logout",
            ["jwks_uri"] = $"{url}/jwks",
            ["response_types_supported"] = new[] { "code" },
            ["subject_types_supported"] = new[] { "public" },
            ["id_token_signing_alg_values_supported"] = new[] { "RS256" },
            ["code_challenge_methods_supported"] = new[] { "S256" },
        }));
        app.MapGet("/jwks", () => Results.Json(new { keys = Array.Empty<object>() }));
        app.MapGet("/authorize", () => Results.Content("<!doctype html><title>Authority</title><h1>Authority sign-in</h1>", "text/html"));
        await app.StartAsync();
        return new StubAuthority(app, url);
    }

    private static int FreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
