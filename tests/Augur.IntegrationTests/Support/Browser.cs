using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Playwright;

namespace Augur.IntegrationTests.Support;

/// <summary>A headless Chromium that records every uncaught page error.</summary>
public sealed class Browser : IAsyncDisposable
{
    private readonly IPlaywright _playwright;
    private readonly IBrowser _browser;
    private readonly List<string> _errors = [];

    private Browser(IPlaywright playwright, IBrowser browser)
    {
        _playwright = playwright;
        _browser = browser;
    }

    public IReadOnlyList<string> AllErrors => _errors;

    /// <summary>Installs Chromium for this Playwright version if needed, then launches it.</summary>
    public static async Task<Browser> LaunchAsync()
    {
        // Never delete browsers that other projects on this machine installed for other Playwright versions.
        Environment.SetEnvironmentVariable("PLAYWRIGHT_SKIP_BROWSER_GC", "1");
        var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"playwright install chromium exited with {exitCode}");
        }

        var playwright = await Playwright.CreateAsync();
        return new Browser(playwright, await playwright.Chromium.LaunchAsync());
    }

    /// <summary>Opens <paramref name="url"/> in a fresh context and waits for the page heading.</summary>
    public async Task<OpenedPage> OpenAsync(string url, int width, bool reduceMotion = false)
    {
        var page = await OpenBlankAsync(width, reduceMotion);
        await page.Page.GotoAsync(url);
        await page.Page.Locator("main h1").WaitForAsync();
        return page;
    }

    public async Task<OpenedPage> OpenBlankAsync(int width, bool reduceMotion = false)
    {
        var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = width, Height = 900 },
            ReducedMotion = reduceMotion ? ReducedMotion.Reduce : ReducedMotion.NoPreference,
        });
        var page = await context.NewPageAsync();
        var opened = new OpenedPage(page);
        page.PageError += (_, error) =>
        {
            opened.Errors.Add(error);
            _errors.Add(error);
        };
        return opened;
    }

    public async ValueTask DisposeAsync()
    {
        await _browser.DisposeAsync();
        _playwright.Dispose();
    }
}

public sealed record OpenedPage(IPage Page)
{
    public List<string> Errors { get; } = [];
}

/// <summary>Runs a published API until disposed.</summary>
public sealed class PublishedApi : IAsyncDisposable
{
    private readonly Process _process;

    private PublishedApi(Process process, string url)
    {
        _process = process;
        Url = url;
    }

    public string Url { get; }

    public static async Task<PublishedApi> StartAsync(string directory, string dll)
    {
        var port = FreePort();
        var url = $"http://127.0.0.1:{port}";
        var process = Process.Start(new ProcessStartInfo("dotnet", $"\"{Path.Combine(directory, dll)}\" --urls {url}")
        {
            WorkingDirectory = directory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            Environment = { ["ASPNETCORE_ENVIRONMENT"] = "Development" },
        })!;
        var api = new PublishedApi(process, url);
        using var http = new HttpClient { BaseAddress = new Uri(url) };
        for (var attempt = 0; attempt < 60; attempt++)
        {
            try
            {
                if ((await http.GetAsync(new Uri("/api/health", UriKind.Relative))).IsSuccessStatusCode)
                {
                    return api;
                }
            }
            catch (HttpRequestException)
            {
            }

            await Task.Delay(500);
        }

        await api.DisposeAsync();
        throw new TimeoutException("the published API did not start");
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            _process.Kill(entireProcessTree: true);
            _process.WaitForExit(5000);
        }
        catch (InvalidOperationException)
        {
        }

        _process.Dispose();
        return ValueTask.CompletedTask;
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
