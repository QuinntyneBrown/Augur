using System.Text.Json.Nodes;
using Augur.Tests.Support;

namespace Augur.Tests.Acceptance;

public sealed class LockfileReplayTests
{
    private const string Spec = "Build an order tracking API for a small shop.\n";

    [Fact]
    public async Task A_rerun_replays_the_lockfile_without_asking_and_writes_the_same_plan()
    {
        using var cli = NewCli();
        var first = await Plan(cli, AnswerScript.DotNetMinimalApi().WriteTo(cli));
        var lockfile = cli.ReadFile("decisions.json");

        var second = await Plan(cli, EmptyScript(cli));

        Assert.Equal(0, second.ExitCode);
        Assert.Equal(first.Stdout, second.Stdout);
        Assert.Equal(lockfile, cli.ReadFile("decisions.json"));
        Assert.Matches(@"resolved 5 \(api 0, lockfile 5, override 0, fallback 0, user 0, script 0\)", second.Stderr);
    }

    [Fact]
    public async Task Changing_the_specification_asks_every_decision_again()
    {
        using var cli = NewCli();
        await Plan(cli, AnswerScript.DotNetMinimalApi().WriteTo(cli));
        cli.WriteFile("spec.md", Spec.Replace("small", "large", StringComparison.Ordinal));

        var result = await Plan(cli, EmptyScript(cli));

        Assert.Equal(3, result.ExitCode);
        Assert.EndsWith("error: the answer script has no answer for: target, authentication\n", result.Stderr);
    }

    [Fact]
    public async Task An_entry_with_a_stale_question_hash_is_the_only_one_asked_again()
    {
        using var cli = NewCli();
        await Plan(cli, AnswerScript.DotNetMinimalApi().WriteTo(cli));
        var lockfile = JsonNode.Parse(cli.ReadFile("decisions.json"))!;
        Entry(lockfile, "persistence")["questionHash"] = new string('0', 64);
        cli.WriteFile("decisions.json", lockfile.ToJsonString());

        var result = await Plan(cli, new AnswerScript().Choice("persistence", "ef-core-postgresql").WriteTo(cli, "persistence.json"));

        Assert.Equal(0, result.ExitCode);
        var decisions = JsonNode.Parse(result.Stdout)!["decisions"]!;
        Assert.Equal("ef-core-postgresql", decisions["persistence"]!["value"]!.GetValue<string>());
        Assert.Matches(@"resolved 5 \(api 0, lockfile 4, override 0, fallback 0, user 0, script 1\)", result.Stderr);
    }

    [Fact]
    public async Task Replayed_entries_keep_their_original_resolution_time()
    {
        using var cli = NewCli();
        await Plan(cli, AnswerScript.DotNetMinimalApi().WriteTo(cli));
        var lockfile = JsonNode.Parse(cli.ReadFile("decisions.json"))!;
        Entry(lockfile, "target")["resolvedAt"] = "2020-01-02T03:04:05Z";
        Entry(lockfile, "persistence")["questionHash"] = new string('0', 64);
        cli.WriteFile("decisions.json", lockfile.ToJsonString());

        await Plan(cli, new AnswerScript().Choice("persistence", "ef-core-postgresql").WriteTo(cli, "persistence.json"));

        var rewritten = JsonNode.Parse(cli.ReadFile("decisions.json"))!;
        Assert.Equal("2020-01-02T03:04:05Z", Entry(rewritten, "target")["resolvedAt"]!.GetValue<string>());
        Assert.Equal("ef-core-postgresql", Entry(rewritten, "persistence")["value"]!.GetValue<string>());
    }

    [Fact]
    public async Task Refresh_asks_every_decision_again()
    {
        using var cli = NewCli();
        await Plan(cli, AnswerScript.DotNetMinimalApi().WriteTo(cli));

        var result = await Plan(cli, AnswerScript.DotNetMinimalApi().Choice("persistence", "none").WriteTo(cli, "second.json"), "--refresh");

        Assert.Equal(0, result.ExitCode);
        Assert.Matches(@"resolved 5 \(api 0, lockfile 0, override 0, fallback 0, user 0, script 5\)", result.Stderr);
        Assert.Equal("none", JsonNode.Parse(result.Stdout)!["decisions"]!["persistence"]!["value"]!.GetValue<string>());
    }

    [Fact]
    public async Task Refresh_with_an_id_asks_only_that_decision_again()
    {
        using var cli = NewCli();
        await Plan(cli, AnswerScript.DotNetMinimalApi().WriteTo(cli));

        var result = await Plan(cli, new AnswerScript().Choice("persistence", "none").WriteTo(cli, "second.json"), "--refresh", "persistence");

        Assert.Equal(0, result.ExitCode);
        Assert.Matches(@"resolved 5 \(api 0, lockfile 4, override 0, fallback 0, user 0, script 1\)", result.Stderr);
    }

    [Fact]
    public async Task Refresh_with_an_unknown_id_is_rejected()
    {
        using var cli = NewCli();

        var result = await Plan(cli, EmptyScript(cli), "--refresh", "colour");

        Assert.Equal(2, result.ExitCode);
        Assert.StartsWith("error: 'colour' is not a catalog decision; valid ids: target,", result.Stderr);
    }

    [Fact]
    public async Task Replayed_fallback_and_user_decisions_are_not_asked_again()
    {
        using var cli = NewCli();
        await Plan(cli, AnswerScript.DotNetMinimalApi().Choice("persistence", "ef-core-postgresql", 0.4).WriteTo(cli), "--on-low-confidence", "default");

        var result = await Plan(cli, EmptyScript(cli), "--on-low-confidence", "fail");

        Assert.Equal(0, result.ExitCode);
        var persistence = JsonNode.Parse(result.Stdout)!["decisions"]!["persistence"]!;
        Assert.Equal("ef-core-sqlite", persistence["value"]!.GetValue<string>());
        Assert.Equal("fallback", persistence["source"]!.GetValue<string>());
    }

    [Fact]
    public async Task Offline_resolves_from_a_complete_lockfile()
    {
        using var cli = NewCli();
        var first = await Plan(cli, AnswerScript.DotNetMinimalApi().WriteTo(cli));

        var result = await cli.RunAsync("plan", "--spec", "spec.md", "--name", "Contoso.Orders", "--offline");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(first.Stdout, result.Stdout);
    }

    [Fact]
    public async Task Offline_names_decisions_missing_from_the_lockfile_and_exits_with_3()
    {
        using var cli = NewCli();
        await Plan(cli, AnswerScript.DotNetMinimalApi().WriteTo(cli));
        var lockfile = JsonNode.Parse(cli.ReadFile("decisions.json"))!;
        lockfile["entries"]!.AsArray().Remove(Entry(lockfile, "persistence"));
        cli.WriteFile("decisions.json", lockfile.ToJsonString());

        var result = await cli.RunAsync("plan", "--spec", "spec.md", "--name", "Contoso.Orders", "--offline");

        Assert.Equal(3, result.ExitCode);
        Assert.Equal("error: offline: no recorded decision for: persistence\n", result.Stderr);
    }

    [Theory]
    [InlineData("--refresh")]
    [InlineData("--oracle-script", "answers.json")]
    public async Task Offline_conflicts_with_options_that_need_another_source(params string[] other)
    {
        using var cli = NewCli();

        var result = await cli.RunAsync(["plan", "--spec", "spec.md", "--name", "Contoso.Orders", "--offline", .. other]);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal($"error: --offline and {other[0]} conflict: --offline resolves decisions only from overrides and the lockfile\n", result.Stderr);
    }

    [Fact]
    public async Task A_lockfile_that_is_not_valid_json_is_named_and_left_unchanged()
    {
        using var cli = NewCli();
        cli.WriteFile("decisions.json", "{ \"lockfileVersion\": 1,\n  \"entries\": [ oops ] }");

        var result = await Plan(cli, AnswerScript.DotNetMinimalApi().WriteTo(cli));

        Assert.Equal(2, result.ExitCode);
        Assert.Matches(@"^error: decisions\.json is not valid JSON \(line 2, position \d+\): .+\n$", result.Stderr);
        Assert.Equal("{ \"lockfileVersion\": 1,\n  \"entries\": [ oops ] }", cli.ReadFile("decisions.json"));
    }

    [Fact]
    public async Task A_lockfile_with_an_unsupported_version_is_rejected()
    {
        using var cli = NewCli();
        cli.WriteFile("decisions.json", "{ \"lockfileVersion\": 99, \"catalogVersion\": 2, \"inputHash\": \"x\", \"entries\": [] }");

        var result = await Plan(cli, AnswerScript.DotNetMinimalApi().WriteTo(cli));

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("error: decisions.json has unsupported lockfileVersion 99; this version of augur reads version 1\n", result.Stderr);
    }

    [Fact]
    public async Task A_recorded_value_outside_the_answer_set_is_rejected()
    {
        using var cli = NewCli();
        await Plan(cli, AnswerScript.DotNetMinimalApi().WriteTo(cli));
        var lockfile = JsonNode.Parse(cli.ReadFile("decisions.json"))!;
        Entry(lockfile, "architecture")["value"] = "serverless";
        cli.WriteFile("decisions.json", lockfile.ToJsonString());

        var result = await Plan(cli, EmptyScript(cli));

        Assert.Equal(2, result.ExitCode);
        Assert.StartsWith("error: invalid answer for architecture from the lockfile: 'serverless' is not one of", result.Stderr);
    }

    private static JsonObject Entry(JsonNode lockfile, string id) =>
        lockfile["entries"]!.AsArray().Single(e => e!["id"]!.GetValue<string>() == id)!.AsObject();

    private static string EmptyScript(CliRunner cli) => new AnswerScript().WriteTo(cli, "empty.json");

    private static CliRunner NewCli()
    {
        var cli = new CliRunner();
        cli.WriteFile("spec.md", Spec);
        return cli;
    }

    private static Task<CliResult> Plan(CliRunner cli, string script, params string[] extra) =>
        cli.RunAsync(["plan", "--spec", "spec.md", "--name", "Contoso.Orders", "--oracle-script", script, .. extra]);
}
