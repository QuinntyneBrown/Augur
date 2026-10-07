using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Augur.Tests.Support;

namespace Augur.Tests.Acceptance;

public sealed class ExplainCommandTests
{
    private static readonly string[] AllIds =
    [
        "architecture", "authentication", "background-processing", "domain-complexity", "persistence",
        "server-side-rendering", "state-management", "target", "ui-library",
    ];

    [Fact]
    public async Task Explain_shows_one_section_per_entry_in_id_order()
    {
        using var cli = await PlannedCli();

        var result = await cli.RunAsync("explain");

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Stderr);
        var headers = Regex.Matches(result.Stdout, @"(?m)^(\S+) = .+ \(\w+\)$").Select(m => m.Groups[1].Value);
        Assert.Equal(AllIds, headers);
    }

    [Fact]
    public async Task Each_section_shows_the_value_source_raw_result_and_thresholds()
    {
        using var cli = await PlannedCli();

        var result = await cli.RunAsync("explain");

        Assert.Contains(
            "authentication = true (script)\n"
            + "  result      probability 0.95\n"
            + "  thresholds  true at 0.80 or more, false at 0.20 or less\n",
            result.Stdout);
        Assert.Contains(
            "domain-complexity = true (script)\n"
            + "  result      score 2.40, confidence 0.85\n"
            + "  distribution\n"
            + "    business-rules  0.60\n"
            + "    complex-domain  0.20\n"
            + "    crud            0.15\n"
            + "    trivial         0.05\n"
            + "  thresholds  minimum confidence 0.70, true at a score of 2 or more\n",
            result.Stdout);
        Assert.Contains(
            "architecture = clean-architecture (script)\n"
            + "  result      choice clean-architecture, confidence 0.90\n"
            + "  distribution\n"
            + "    clean-architecture  0.90\n"
            + "  thresholds  minimum confidence 0.70\n",
            result.Stdout);
    }

    [Fact]
    public async Task Explain_shows_the_thresholds_that_applied_when_min_confidence_was_used()
    {
        using var cli = await PlannedCli("--min-confidence", "0.8");

        var result = await cli.RunAsync("explain");

        Assert.Contains("architecture = clean-architecture (script)\n", result.Stdout);
        Assert.Matches(@"(?ms)^architecture = .*?  thresholds  minimum confidence 0\.80\n", result.Stdout);
    }

    [Fact]
    public async Task Explain_json_lists_every_entry_with_its_result_and_thresholds()
    {
        using var cli = await PlannedCli();

        var result = await cli.RunAsync("explain", "--json");

        Assert.Equal(0, result.ExitCode);
        var entries = JsonNode.Parse(result.Stdout)!.AsArray();
        Assert.Equal(AllIds, entries.Select(e => e!["id"]!.GetValue<string>()));
        var complexity = entries.Single(e => e!["id"]!.GetValue<string>() == "domain-complexity")!;
        Assert.True(complexity["value"]!.GetValue<bool>());
        Assert.Equal("script", complexity["source"]!.GetValue<string>());
        Assert.Equal(2.4, complexity["result"]!["score"]!.GetValue<double>());
        Assert.Equal(0.85, complexity["result"]!["confidence"]!.GetValue<double>());
        Assert.Equal(
            ["business-rules", "complex-domain", "crud", "trivial"],
            complexity["result"]!["distribution"]!.AsArray().Select(d => d!["label"]!.GetValue<string>()));
        Assert.Equal(0.7, complexity["thresholds"]!["minConfidence"]!.GetValue<double>());
        Assert.Equal(2.0, complexity["thresholds"]!["cutOff"]!.GetValue<double>());
        Assert.False(complexity["stale"]!.GetValue<bool>());
        Assert.NotNull(complexity["resolvedAt"]);
    }

    [Fact]
    public async Task Explain_marks_an_entry_whose_question_changed_as_stale()
    {
        using var cli = await PlannedCli();
        var lockfile = JsonNode.Parse(cli.ReadFile("decisions.json"))!;
        lockfile["entries"]!.AsArray().Single(e => e!["id"]!.GetValue<string>() == "persistence")!["questionHash"] = new string('0', 64);
        cli.WriteFile("decisions.json", lockfile.ToJsonString());

        var text = await cli.RunAsync("explain");
        var json = await cli.RunAsync("explain", "--json");

        Assert.Matches(@"(?ms)^persistence = .*?  stale       the question has changed since this was recorded; it will be asked again\n", text.Stdout);
        Assert.True(JsonNode.Parse(json.Stdout)!.AsArray().Single(e => e!["id"]!.GetValue<string>() == "persistence")!["stale"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Explain_reads_the_lockfile_named_by_lock()
    {
        using var cli = await PlannedCli("--lock", "locks/orders.json");

        var result = await cli.RunAsync("explain", "--lock", "locks/orders.json");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("target = fullstack (script)", result.Stdout);
    }

    [Fact]
    public async Task Explain_names_a_missing_lockfile_and_exits_with_2()
    {
        using var cli = new CliRunner();

        var result = await cli.RunAsync("explain", "--lock", "nowhere.json");

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("error: lockfile not found: nowhere.json\n", result.Stderr);
    }

    private static async Task<CliRunner> PlannedCli(params string[] extra)
    {
        var cli = new CliRunner();
        cli.WriteFile("spec.md", "Build an order tracking app with sign-in.\n");
        var script = AnswerScript.FullstackCleanArchitecture().WriteTo(cli);
        var result = await cli.RunAsync(["plan", "--spec", "spec.md", "--name", "Contoso.Orders", "--oracle-script", script, .. extra]);
        Assert.Equal(0, result.ExitCode);
        return cli;
    }
}
