using Augur.IntegrationTests.Support;
using Augur.Tests.Support;

namespace Augur.IntegrationTests;

/// <summary>Release checks that emitted code has no known vulnerable dependencies (L2-046 AC2, L2-054 AC2).</summary>
public sealed class AuditTests
{
    [SlowFact]
    public async Task Emitted_angular_workspaces_have_no_high_or_critical_production_vulnerabilities()
    {
        using var build = new EmittedBuild("audit-npm");
        var names = await build.EmitAsync("Audit", [.. Plans.AllAngular()]);

        var failures = new List<string>();
        foreach (var name in names)
        {
            var audit = await Npm.RunAsync("audit --omit=dev --audit-level=high", Path.Combine(build.Root, name), TimeSpan.FromMinutes(5));
            if (audit.ExitCode != 0)
            {
                failures.Add($"{name} ({File.ReadAllText(Path.Combine(build.Root, name, "plan.txt"))}):\n{audit.Output}");
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n\n", failures));
    }

    [SlowFact]
    public async Task Emitted_dotnet_solutions_have_no_vulnerable_packages()
    {
        using var build = new EmittedBuild("audit-nuget");
        await build.EmitAsync("Audit", DotNetBuildTests.DotNetPlans());
        var solution = build.WriteAggregateSolution("Audit");

        var restore = await EmittedBuild.RunAsync("dotnet", $"restore \"{solution}\"", build.Root, TimeSpan.FromMinutes(15));
        Assert.True(restore.ExitCode == 0, restore.Output);
        var listed = await EmittedBuild.RunAsync("dotnet", $"list \"{solution}\" package --vulnerable --include-transitive", build.Root, TimeSpan.FromMinutes(10));

        Assert.True(listed.ExitCode == 0, listed.Output);
        Assert.DoesNotContain("has the following vulnerable packages", listed.Output);
    }
}
