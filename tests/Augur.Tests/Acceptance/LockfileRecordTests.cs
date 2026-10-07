using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Augur.Tests.Support;

namespace Augur.Tests.Acceptance;

public sealed class LockfileRecordTests
{
    private const string Spec = "Build an order tracking API for a small shop. Customers place orders and staff ship them.\n";

    [Fact]
    public async Task A_plan_records_every_resolved_decision_in_decisions_json()
    {
        using var cli = NewCli();
        var script = AnswerScript.DotNetMinimalApi().WriteTo(cli);

        var result = await Plan(cli, script);

        Assert.Equal(0, result.ExitCode);
        var lockfile = JsonNode.Parse(cli.ReadFile("decisions.json"))!;
        Assert.Equal(1, lockfile["lockfileVersion"]!.GetValue<int>());
        Assert.Equal(2, lockfile["catalogVersion"]!.GetValue<int>());
        Assert.Matches("^[0-9a-f]{64}$", lockfile["inputHash"]!.GetValue<string>());
        var entries = lockfile["entries"]!.AsArray();
        Assert.Equal(
            ["architecture", "authentication", "background-processing", "persistence", "target"],
            entries.Select(e => e!["id"]!.GetValue<string>()));
        foreach (var entry in entries)
        {
            Assert.Matches("^[0-9a-f]{64}$", entry!["questionHash"]!.GetValue<string>());
            Assert.Equal("script", entry["source"]!.GetValue<string>());
            Assert.NotNull(entry["value"]);
            Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$", entry["resolvedAt"]!.GetValue<string>());
            Assert.Null(entry["model"]);
        }

        var architecture = entries.Single(e => e!["id"]!.GetValue<string>() == "architecture")!;
        Assert.Equal("minimal-api", architecture["value"]!.GetValue<string>());
        Assert.Equal("minimal-api", architecture["choice"]!.GetValue<string>());
        Assert.Equal(0.9, architecture["confidence"]!.GetValue<double>());
        Assert.NotNull(architecture["probabilities"]);
        var authentication = entries.Single(e => e!["id"]!.GetValue<string>() == "authentication")!;
        Assert.False(authentication["value"]!.GetValue<bool>());
        Assert.Equal(0.1, authentication["probability"]!.GetValue<double>());
    }

    [Fact]
    public async Task Overridden_and_skipped_decisions_are_not_recorded()
    {
        using var cli = NewCli();
        var script = AnswerScript.DotNetMinimalApi().WriteTo(cli);

        var result = await Plan(cli, script, "--set", "target=dotnet", "--set", "architecture=minimal-api");

        Assert.Equal(0, result.ExitCode);
        var ids = JsonNode.Parse(cli.ReadFile("decisions.json"))!["entries"]!.AsArray().Select(e => e!["id"]!.GetValue<string>());
        Assert.Equal(["authentication", "background-processing", "persistence"], ids);
    }

    [Fact]
    public async Task A_fallback_records_the_chosen_value_and_the_raw_answer()
    {
        using var cli = NewCli();
        var script = AnswerScript.DotNetMinimalApi().Choice("persistence", "ef-core-postgresql", 0.5).WriteTo(cli);

        var result = await Plan(cli, script, "--on-low-confidence", "default");

        Assert.Equal(0, result.ExitCode);
        var persistence = JsonNode.Parse(cli.ReadFile("decisions.json"))!["entries"]!.AsArray()
            .Single(e => e!["id"]!.GetValue<string>() == "persistence")!;
        Assert.Equal("fallback", persistence["source"]!.GetValue<string>());
        Assert.Equal("ef-core-sqlite", persistence["value"]!.GetValue<string>());
        Assert.Equal("ef-core-postgresql", persistence["choice"]!.GetValue<string>());
        Assert.Equal(0.5, persistence["confidence"]!.GetValue<double>());
    }

    [Fact]
    public async Task The_lock_option_moves_the_lockfile()
    {
        using var cli = NewCli();
        var script = AnswerScript.DotNetMinimalApi().WriteTo(cli);

        var result = await Plan(cli, script, "--lock", "locks/orders.json");

        Assert.Equal(0, result.ExitCode);
        Assert.True(cli.Exists("locks/orders.json"));
        Assert.False(cli.Exists("decisions.json"));
    }

    [Fact]
    public async Task The_lockfile_never_contains_the_specification_or_the_api_key()
    {
        using var cli = NewCli().WithEnv("OPENAI_API_KEY", "sk-test-SENTINEL123");
        var script = AnswerScript.DotNetMinimalApi().WriteTo(cli);

        await Plan(cli, script);

        var lockfile = cli.ReadFile("decisions.json");
        for (var start = 0; start + 32 <= Spec.Length; start++)
        {
            Assert.DoesNotContain(Spec.Substring(start, 32), lockfile);
        }

        Assert.DoesNotContain("SENTINEL123", lockfile);
    }

    [Fact]
    public async Task The_lockfile_is_canonical_and_identical_across_runs_apart_from_resolution_times()
    {
        using var first = NewCli();
        using var second = NewCli();

        await Plan(first, AnswerScript.DotNetMinimalApi().WriteTo(first));
        await Plan(second, AnswerScript.DotNetMinimalApi().WriteTo(second));

        var one = first.ReadFile("decisions.json");
        Assert.DoesNotContain("\r", one);
        Assert.EndsWith("}\n", one);
        Assert.Contains("\n  \"entries\": [\n    {\n", one);
        Assert.Equal(WithoutTimes(one), WithoutTimes(second.ReadFile("decisions.json")));
    }

    [Fact]
    public async Task No_lockfile_is_written_when_decisions_are_unresolved()
    {
        using var cli = NewCli();
        var script = AnswerScript.DotNetMinimalApi().Choice("architecture", "vertical-slice", 0.3).WriteTo(cli);

        var result = await Plan(cli, script, "--on-low-confidence", "fail");

        Assert.Equal(3, result.ExitCode);
        Assert.False(cli.Exists("decisions.json"));
    }

    private static string WithoutTimes(string lockfile) =>
        Regex.Replace(lockfile, "\"resolvedAt\": \"[^\"]+\"", "\"resolvedAt\": \"\"");

    private static CliRunner NewCli()
    {
        var cli = new CliRunner();
        cli.WriteFile("spec.md", Spec);
        return cli;
    }

    private static Task<CliResult> Plan(CliRunner cli, string script, params string[] extra) =>
        cli.RunAsync(["plan", "--spec", "spec.md", "--name", "Contoso.Orders", "--oracle-script", script, .. extra]);
}
