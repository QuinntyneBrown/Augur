using System.Text.RegularExpressions;
using Augur.IntegrationTests.Support;
using Augur.Tests.Support;

namespace Augur.IntegrationTests;

public sealed partial class AngularBuildTests
{
    /// <summary>Every pair of values, or all 16 workspaces with <c>AUGUR_FULL_MATRIX=1</c>.</summary>
    public static IReadOnlyList<Dictionary<string, object>> AngularPlans() =>
        Environment.GetEnvironmentVariable("AUGUR_FULL_MATRIX") == "1"
            ? [.. Plans.AllAngular()]
            : Pairwise.Cover([.. Plans.AllAngular()]);

    [Fact]
    public void The_pairwise_angular_set_covers_every_pair_of_values()
    {
        var all = Plans.AllAngular().ToList();

        Assert.Equal(all.SelectMany(Pairwise.Pairs).ToHashSet(), Pairwise.Cover(all).SelectMany(Pairwise.Pairs).ToHashSet());
    }

    [SlowFact]
    public async Task Emitted_angular_workspaces_install_build_without_warnings_and_pass_their_tests()
    {
        using var build = new EmittedBuild("angular");
        var plans = AngularPlans();
        var names = await build.EmitAsync("Web", plans);

        var failures = new List<string>();
        for (var i = 0; i < names.Count; i++)
        {
            var workspace = Path.Combine(build.Root, names[i]);
            var label = $"{names[i]} ({Plans.Describe(plans[i])})";
            var ssr = (bool)plans[i]["server-side-rendering"];
            await Npm.VerifyWorkspaceAsync(workspace, label, ssr, failures);
        }

        Assert.True(failures.Count == 0, $"workspaces in {build.Root} failed:\n{string.Join("\n\n", failures)}");
    }
}

/// <summary>Runs the commands L2-031 requires on an emitted Angular workspace.</summary>
internal static partial class Npm
{
    public static async Task VerifyWorkspaceAsync(string workspace, string label, bool ssr, List<string> failures)
    {
        var install = await RunAsync("ci --no-audit --no-fund", workspace, TimeSpan.FromMinutes(10));
        if (install.ExitCode != 0)
        {
            failures.Add($"{label}: npm ci failed\n{Tail(install.Output)}");
            return;
        }

        var built = await RunAsync("run build -- --configuration production", workspace, TimeSpan.FromMinutes(10));
        var output = AnsiCodes().Replace(built.Output, "");
        if (built.ExitCode != 0 || output.Contains("WARNING", StringComparison.OrdinalIgnoreCase) || output.Contains("budget", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add($"{label}: ng build failed or warned\n{Tail(output)}");
            return;
        }

        var name = Path.GetFileName(Directory.GetDirectories(Path.Combine(workspace, "dist")).Single());
        if (ssr && !File.Exists(Path.Combine(workspace, "dist", name, "server", "server.mjs")))
        {
            failures.Add($"{label}: the production build has no server bundle");
        }

        var tested = await RunAsync("test", workspace, TimeSpan.FromMinutes(10));
        if (tested.ExitCode != 0)
        {
            failures.Add($"{label}: ng test failed\n{Tail(AnsiCodes().Replace(tested.Output, ""))}");
        }
    }

    /// <summary>Installs and builds a workspace for production and returns the folder of browser files.</summary>
    public static async Task<string> InstallAndBuildAsync(string workspace)
    {
        var install = await RunAsync("ci --no-audit --no-fund", workspace, TimeSpan.FromMinutes(10));
        Assert.True(install.ExitCode == 0, install.Output);
        var built = await RunAsync("run build -- --configuration production", workspace, TimeSpan.FromMinutes(10));
        Assert.True(built.ExitCode == 0, built.Output);
        var dist = Directory.GetDirectories(Path.Combine(workspace, "dist")).Single();
        return Path.Combine(dist, "browser");
    }

    public static Task<ProcessResult> RunAsync(string arguments, string workingDirectory, TimeSpan timeout) =>
        OperatingSystem.IsWindows()
            ? EmittedBuild.RunAsync("cmd.exe", $"/c npm {arguments}", workingDirectory, timeout, Environment())
            : EmittedBuild.RunAsync("npm", arguments, workingDirectory, timeout, Environment());

    private static Dictionary<string, string?> Environment() => new()
    {
        ["CI"] = "1",
        ["NG_CLI_ANALYTICS"] = "false",
        ["NO_COLOR"] = "1",
    };

    private static string Tail(string output) => string.Join('\n', output.Split('\n').TakeLast(40));

    [GeneratedRegex(@"\x1b\[[0-9;]*m")]
    private static partial Regex AnsiCodes();
}
