using Augur.Tests.Support;

namespace Augur.Tests.Acceptance;

public sealed class RunSummaryTests
{
    [Fact]
    public async Task A_successful_plan_ends_with_a_one_line_summary_by_source()
    {
        using var cli = NewCli();
        var script = AnswerScript.DotNetMinimalApi().Choice("architecture", "vertical-slice", 0.4).Score("domain-complexity", 1, 0.9).WriteTo(cli);

        var result = await cli.RunAsync(
            "plan", "--spec", "spec.md", "--name", "Contoso.Orders", "--oracle-script", script,
            "--set", "target=dotnet", "--on-low-confidence", "default");

        Assert.Equal(0, result.ExitCode);
        var lastLine = result.Stderr.TrimEnd('\n').Split('\n')[^1];
        Assert.Matches(
            @"^resolved 6 \(api 0, lockfile 0, override 1, fallback 1, user 0, script 4\), skipped 3, api requests 0, elapsed \d+\.\d\ds$",
            lastLine);
    }

    [Fact]
    public async Task Quiet_writes_nothing_to_stderr_on_success()
    {
        using var cli = NewCli();
        var script = AnswerScript.DotNetMinimalApi().Choice("architecture", "vertical-slice", 0.4).Score("domain-complexity", 1, 0.9).WriteTo(cli);

        var result = await cli.RunAsync(
            "plan", "--spec", "spec.md", "--name", "Contoso.Orders", "--oracle-script", script,
            "--on-low-confidence", "default", "--verbosity", "quiet");

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Stderr);
    }

    [Fact]
    public async Task Quiet_still_writes_the_error_line_on_failure()
    {
        using var cli = NewCli();

        var result = await cli.RunAsync("plan", "--spec", "missing.md", "--name", "Contoso.Orders", "--verbosity", "quiet");

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("error: specification file not found: missing.md\n", result.Stderr);
    }

    [Fact]
    public async Task Detailed_logs_each_decision_as_it_is_resolved_or_skipped()
    {
        using var cli = NewCli();
        var script = AnswerScript.DotNetMinimalApi().WriteTo(cli);

        var result = await cli.RunAsync(
            "plan", "--spec", "spec.md", "--name", "Contoso.Orders", "--oracle-script", script,
            "--set", "authentication=true", "--verbosity", "detailed");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("authentication = true (override)\n", result.Stderr);
        Assert.Contains("target = dotnet (script)\n", result.Stderr);
        Assert.Contains("ui-library skipped: target is dotnet\n", result.Stderr);
        Assert.Contains("domain-complexity skipped: architecture is minimal-api\n", result.Stderr);
        Assert.True(
            result.Stderr.IndexOf("target = dotnet", StringComparison.Ordinal) < result.Stderr.IndexOf("architecture = minimal-api", StringComparison.Ordinal),
            "decisions are logged in dependency order");
    }

    [Fact]
    public async Task Normal_verbosity_does_not_log_each_decision()
    {
        using var cli = NewCli();
        var script = AnswerScript.DotNetMinimalApi().WriteTo(cli);

        var result = await cli.RunAsync("plan", "--spec", "spec.md", "--name", "Contoso.Orders", "--oracle-script", script);

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("target = dotnet", result.Stderr);
        Assert.Single(result.Stderr.TrimEnd('\n').Split('\n'));
    }

    private static CliRunner NewCli()
    {
        var cli = new CliRunner();
        cli.WriteFile("spec.md", "Build an order tracking API.\n");
        return cli;
    }
}
