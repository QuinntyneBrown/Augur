using System.Net;
using System.Net.Sockets;
using Augur.IntegrationTests.Support;
using Augur.Tests.Support;

namespace Augur.IntegrationTests;

public sealed class FullstackBuildTests
{
    private static readonly string[] Architectures = ["clean-architecture", "vertical-slice", "minimal-api"];
    private static readonly string[] Persistence = ["ef-core-sqlserver", "ef-core-postgresql", "ef-core-sqlite", "none"];
    private static readonly bool[] Booleans = [false, true];

    /// <summary>Every valid fullstack decision set.</summary>
    public static IEnumerable<Dictionary<string, object>> AllFullstack() =>
        from authentication in Booleans
        from architecture in Architectures
        from cqrs in architecture == "minimal-api" ? new bool?[] { null } : [false, true]
        from persistence in Persistence
        from worker in Booleans
        from uiLibrary in new[] { "angular-material", "none" }
        from state in new[] { "signals", "ngrx-signal-store" }
        from ssr in Booleans
        select Plans.Fullstack(architecture, persistence, authentication, worker, cqrs, uiLibrary, state, ssr);

    [Fact]
    public void The_pairwise_fullstack_set_covers_every_pair_of_values()
    {
        var all = AllFullstack().ToList();
        var chosen = Pairwise.Cover(all);

        Assert.Equal(all.SelectMany(Pairwise.Pairs).ToHashSet(), chosen.SelectMany(Pairwise.Pairs).ToHashSet());
        Assert.True(chosen.Count <= 20, $"{chosen.Count} plans");
    }

    [SlowFact]
    public async Task The_pairwise_fullstack_plans_build_and_pass_their_tests_on_both_sides()
    {
        using var build = new EmittedBuild("fullstack");
        var plans = Pairwise.Cover([.. AllFullstack()]);
        var names = await build.EmitAsync("Full", plans);

        var solution = build.WriteAggregateSolution("Full");
        var compiled = await EmittedBuild.RunAsync("dotnet", $"build \"{solution}\" -warnaserror", build.Root, TimeSpan.FromMinutes(20));
        Assert.True(compiled.ExitCode == 0 && compiled.Problems.Length == 0, $"dotnet build failed in {build.Root}:\n{compiled.Problems}");
        var tested = await EmittedBuild.RunAsync("dotnet", $"test \"{solution}\" --no-build", build.Root, TimeSpan.FromMinutes(20));
        Assert.True(tested.ExitCode == 0, $"dotnet test failed in {build.Root}:\n{tested.Output}");

        var failures = new List<string>();
        for (var i = 0; i < names.Count; i++)
        {
            await Npm.VerifyWorkspaceAsync(Path.Combine(build.Root, names[i], "web"), $"{names[i]} ({Plans.Describe(plans[i])})", (bool)plans[i]["server-side-rendering"], failures);
        }

        Assert.True(failures.Count == 0, string.Join("\n\n", failures));
    }

    [SlowFact]
    public async Task The_published_api_serves_the_angular_app_and_keeps_api_404s()
    {
        using var build = new EmittedBuild("publish");
        var plan = Plans.Fullstack("minimal-api", "none", authentication: false, worker: false, cqrs: null, "none", "signals", ssr: false);
        var name = (await build.EmitAsync("Shop", [plan])).Single();
        var root = Path.Combine(build.Root, name);
        var publish = Path.Combine(build.Root, "publish");

        var published = await EmittedBuild.RunAsync("dotnet", $"publish \"{Path.Combine(root, $"{name}.Api", $"{name}.Api.csproj")}\" -c Release -o \"{publish}\"", root, TimeSpan.FromMinutes(15));
        Assert.True(published.ExitCode == 0, published.Output);
        Assert.True(File.Exists(Path.Combine(publish, "wwwroot", "index.html")), "the Angular build was not published to wwwroot");

        var port = FreePort();
        using var api = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("dotnet", $"\"{Path.Combine(publish, $"{name}.Api.dll")}\" --urls http://127.0.0.1:{port}")
        {
            WorkingDirectory = publish,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            Environment = { ["ASPNETCORE_ENVIRONMENT"] = "Development" },
        })!;
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
            await WaitUntilUpAsync(http);

            var home = await http.GetAsync(new Uri("/", UriKind.Relative));
            var clientRoute = await http.GetAsync(new Uri("/some/client/route", UriKind.Relative));
            var missingApi = await http.GetAsync(new Uri("/api/does-not-exist", UriKind.Relative));
            var health = await http.GetStringAsync(new Uri("/api/health", UriKind.Relative));

            Assert.Equal(HttpStatusCode.OK, home.StatusCode);
            Assert.Contains("<app-root>", await home.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.OK, clientRoute.StatusCode);
            Assert.Contains("<app-root>", await clientRoute.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.NotFound, missingApi.StatusCode);
            Assert.Equal("{\"status\":\"Healthy\"}", health);
        }
        finally
        {
            api.Kill(entireProcessTree: true);
        }
    }

    private static async Task WaitUntilUpAsync(HttpClient http)
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            try
            {
                if ((await http.GetAsync(new Uri("/api/health", UriKind.Relative))).IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }

            await Task.Delay(500);
        }

        throw new TimeoutException("the published API did not start");
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
