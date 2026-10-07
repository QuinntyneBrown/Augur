using System.Diagnostics;
using System.Runtime.InteropServices;
using Augur.IntegrationTests.Support;
using Augur.Tests.Support;

namespace Augur.IntegrationTests;

/// <summary>Tests that run augur the way users do: packed, installed as a .NET tool, and started as its own process.</summary>
public sealed class InstalledToolTests
{
    private const string Spec = "Build an order tracking app.\n";

    [SlowFact]
    public async Task The_package_installs_runs_and_uninstalls_as_a_dotnet_tool()
    {
        using var tool = await InstalledTool.InstallAsync();

        var version = await tool.RunAsync(tool.Root, "--version");
        Assert.Equal(0, version.ExitCode);
        Assert.Matches(@"^augur \d+\.\d+\.\d+ \(catalog 2\)\r?\n$", version.Stdout);

        var uninstall = await EmittedBuild.RunAsync("dotnet", $"tool uninstall Augur.Cli --tool-path \"{tool.ToolPath}\"", tool.Root, TimeSpan.FromMinutes(2));
        Assert.Equal(0, uninstall.ExitCode);
        Assert.False(File.Exists(tool.Executable), "the augur command is still present after uninstalling");
    }

    [SlowFact]
    public async Task Emission_needs_no_node_or_npm_and_matches_the_output_of_a_full_environment()
    {
        using var tool = await InstalledTool.InstallAsync();
        var plan = Plans.Fullstack("clean-architecture", "ef-core-sqlite", authentication: true, worker: true, cqrs: true, "angular-material", "ngrx-signal-store", ssr: true);
        File.WriteAllText(Path.Combine(tool.Root, "plan.json"), Plans.Json("Contoso.Orders", plan));
        var dotnetOnly = Path.GetDirectoryName(Environment.ProcessPath)!;

        var isolated = await tool.RunAsync(tool.Root, "emit --plan plan.json --out isolated", new() { ["PATH"] = dotnetOnly });
        var normal = await tool.RunAsync(tool.Root, "emit --plan plan.json --out normal");

        Assert.True(isolated.ExitCode == 0, isolated.Output);
        Assert.Equal(0, normal.ExitCode);
        var a = Snapshot(Path.Combine(tool.Root, "isolated"));
        var b = Snapshot(Path.Combine(tool.Root, "normal"));
        Assert.Equal(b.Keys, a.Keys);
        Assert.All(a, file => Assert.Equal(b[file.Key], file.Value));
    }

    [SlowFact]
    public async Task Replayed_generate_and_emit_stay_within_their_time_budgets()
    {
        using var tool = await InstalledTool.InstallAsync();
        File.WriteAllText(Path.Combine(tool.Root, "spec.md"), Spec);
        File.WriteAllText(Path.Combine(tool.Root, "answers.json"), AnswerScript.FullstackCleanArchitecture().ToJson());
        var first = await tool.RunAsync(tool.Root, "generate --spec spec.md --name Contoso.Orders --oracle-script answers.json --out first");
        Assert.True(first.ExitCode == 0, first.Output);

        var generate = new List<TimeSpan>();
        var emit = new List<TimeSpan>();
        for (var run = 0; run < 10; run++)
        {
            var clock = Stopwatch.StartNew();
            var replayed = await tool.RunAsync(tool.Root, $"generate --spec spec.md --name Contoso.Orders --offline --out g{run}");
            generate.Add(clock.Elapsed);
            Assert.True(replayed.ExitCode == 0, replayed.Output);

            clock.Restart();
            var emitted = await tool.RunAsync(tool.Root, $"emit --plan first/augur.plan.json --out e{run}");
            emit.Add(clock.Elapsed);
            Assert.True(emitted.ExitCode == 0, emitted.Output);
        }

        Assert.True(P95(generate) <= TimeSpan.FromSeconds(3), $"generate --offline p95 {P95(generate)}");
        Assert.True(P95(emit) <= TimeSpan.FromSeconds(2), $"emit p95 {P95(emit)}");
    }

    [SlowFact]
    public async Task Maximum_size_inputs_stay_under_256_MB_and_requests_add_little_overhead()
    {
        using var tool = await InstalledTool.InstallAsync();
        await using var stub = await StubDecisionsServer.StartAsync(AnswerScript.DotNetMinimalApi());
        var environment = new Dictionary<string, string?> { ["OPENAI_API_KEY"] = "sk-test-KEY456", ["AUGUR_OPENAI_BASE_URL"] = stub.BaseUrl };
        File.WriteAllText(Path.Combine(tool.Root, "spec.md"), new string('a', 262_144));
        var images = new List<string>();
        for (var i = 0; i < 4; i++)
        {
            var image = new byte[10 * 1024 * 1024];
            new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(image, 0);
            Random.Shared.NextBytes(image.AsSpan(8));
            File.WriteAllBytes(Path.Combine(tool.Root, $"image{i}.png"), image);
            images.Add($"--image image{i}.png");
        }

        var (big, peak) = await tool.RunMeasuredAsync(tool.Root, $"plan --spec spec.md --name Contoso.Orders --lock big.json {string.Join(' ', images)}", environment);
        Assert.True(big.ExitCode == 0, big.Output);
        Assert.True(peak <= 256L * 1024 * 1024, $"peak working set {peak / 1024 / 1024} MB");

        // L2-048 AC4: the fastest of three runs, so a single scheduling hiccup does not count as augur's overhead.
        File.WriteAllText(Path.Combine(tool.Root, "small.md"), Spec);
        var startup = TimeSpan.MaxValue;
        var planned = TimeSpan.MaxValue;
        for (var run = 0; run < 3; run++)
        {
            var clock = Stopwatch.StartNew();
            await tool.RunAsync(tool.Root, "--version");
            startup = TimeSpan.FromTicks(Math.Min(startup.Ticks, clock.Elapsed.Ticks));

            clock.Restart();
            var small = await tool.RunAsync(tool.Root, $"plan --spec small.md --name Contoso.Orders --lock small{run}.json", environment);
            planned = TimeSpan.FromTicks(Math.Min(planned.Ticks, clock.Elapsed.Ticks));
            Assert.True(small.ExitCode == 0, small.Output);
        }

        Assert.True(planned - startup <= TimeSpan.FromMilliseconds(200), $"plan with 2 instant requests took {(planned - startup).TotalMilliseconds:0} ms beyond process startup");
    }

    [SlowFact]
    public async Task Sigint_while_waiting_on_the_api_exits_with_130_and_changes_nothing()
    {
        if (OperatingSystem.IsWindows())
        {
            return; // Windows has no SIGINT for a child process; the in-process cancellation test covers L2-004 there.
        }

        using var tool = await InstalledTool.InstallAsync();
        await using var stub = await StubDecisionsServer.StartAsync((_, _) => Task.FromResult(StubResponse.Never));
        File.WriteAllText(Path.Combine(tool.Root, "spec.md"), Spec);
        var process = tool.Start(tool.Root, "plan --spec spec.md --name Contoso.Orders --out plan.json", new()
        {
            ["OPENAI_API_KEY"] = "sk-test-KEY456",
            ["AUGUR_OPENAI_BASE_URL"] = stub.BaseUrl,
        });
        while (stub.Requests.Count == 0)
        {
            await Task.Delay(50);
        }

        var clock = Stopwatch.StartNew();
        Assert.Equal(0, Kill(process.Id, 2));
        Assert.True(process.WaitForExit(2000), "augur did not exit within 2 seconds of SIGINT");
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(2));
        Assert.Equal(130, process.ExitCode);
        Assert.False(File.Exists(Path.Combine(tool.Root, "plan.json")));
        Assert.False(File.Exists(Path.Combine(tool.Root, "decisions.json")));
    }

    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int Kill(int pid, int signal);

    private static TimeSpan P95(List<TimeSpan> samples) => samples.Order().ElementAt((int)Math.Ceiling(samples.Count * 0.95) - 1);

    private static SortedDictionary<string, byte[]> Snapshot(string directory) =>
        new(Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .ToDictionary(f => Path.GetRelativePath(directory, f).Replace('\\', '/'), File.ReadAllBytes), StringComparer.Ordinal);
}
