using System.Text.Json.Nodes;
using Augur.Tests.Support;

namespace Augur.Tests.Acceptance;

public sealed class ScriptedPlanTests
{
    private const string Spec = "Build an order tracking API for a small shop.\n";

    [Fact]
    public async Task Scripted_answers_resolve_with_source_script()
    {
        using var cli = NewCli();
        var script = AnswerScript.DotNetMinimalApi().Choice("architecture", "vertical-slice").Score("domain-complexity", 1.0, 0.9).WriteTo(cli);

        var result = await Plan(cli, script);

        Assert.Equal(0, result.ExitCode);
        var architecture = Decisions(result)["architecture"]!;
        Assert.Equal("vertical-slice", architecture["value"]!.GetValue<string>());
        Assert.Equal("script", architecture["source"]!.GetValue<string>());
    }

    [Fact]
    public async Task Stdout_holds_only_the_plan_json()
    {
        using var cli = NewCli();
        var script = AnswerScript.DotNetMinimalApi().WriteTo(cli);

        var result = await Plan(cli, script);

        Assert.Equal(0, result.ExitCode);
        Assert.NotNull(JsonNode.Parse(result.Stdout));
    }

    [Fact]
    public async Task A_decision_whose_dependency_rules_it_out_is_skipped_and_never_asked()
    {
        using var cli = NewCli();
        var script = AnswerScript.DotNetMinimalApi().WriteTo(cli);

        var result = await Plan(cli, script);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(
            ["architecture", "authentication", "background-processing", "persistence", "target"],
            Decisions(result).Select(d => d.Key));
    }

    [Fact]
    public async Task The_score_decision_is_asked_after_the_architecture_it_depends_on()
    {
        using var cli = NewCli();
        var script = AnswerScript.DotNetMinimalApi().Choice("architecture", "clean-architecture").Score("domain-complexity", 2.0, 0.8).WriteTo(cli);

        var result = await Plan(cli, script);

        Assert.Equal(0, result.ExitCode);
        Assert.True(Decisions(result)["domain-complexity"]!["value"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData(0.80, true)]
    [InlineData(0.20, false)]
    [InlineData(0.99, true)]
    [InlineData(0.01, false)]
    public async Task A_predicate_resolves_at_or_beyond_its_thresholds(double probability, bool expected)
    {
        using var cli = NewCli();
        var script = AnswerScript.DotNetMinimalApi().Predicate("authentication", probability).WriteTo(cli);

        var result = await Plan(cli, script);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(expected, Decisions(result)["authentication"]!["value"]!.GetValue<bool>());
    }

    [Fact]
    public async Task A_predicate_between_its_thresholds_is_low_confidence()
    {
        using var cli = NewCli();
        var script = AnswerScript.DotNetMinimalApi().Predicate("authentication", 0.50).WriteTo(cli);

        var result = await Plan(cli, script, "--on-low-confidence", "fail");

        Assert.Equal(3, result.ExitCode);
        Assert.Empty(result.Stdout);
        Assert.Contains("authentication: probability 0.50", result.Stderr);
    }

    [Theory]
    [InlineData("vertical-slice", 0.70, true)]
    [InlineData("vertical-slice", 0.69, false)]
    [InlineData("other", 0.95, false)]
    public async Task A_choice_resolves_only_at_or_above_its_minimum_confidence_and_never_to_other(string choice, double confidence, bool resolves)
    {
        using var cli = NewCli();
        var script = AnswerScript.DotNetMinimalApi().Choice("architecture", choice, confidence).Score("domain-complexity", 1, 0.9).WriteTo(cli);

        var result = await Plan(cli, script, "--on-low-confidence", "fail");

        if (resolves)
        {
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(choice, Decisions(result)["architecture"]!["value"]!.GetValue<string>());
        }
        else
        {
            Assert.Equal(3, result.ExitCode);
            Assert.Contains($"architecture: {choice} with confidence {confidence:0.00}", result.Stderr);
        }
    }

    [Theory]
    [InlineData(2.0, 0.80, "true")]
    [InlineData(1.99, 0.80, "false")]
    [InlineData(2.5, 0.50, null)]
    public async Task A_score_resolves_by_its_cut_off_when_confident(double score, double confidence, string? expected)
    {
        using var cli = NewCli();
        var script = AnswerScript.DotNetMinimalApi().Choice("architecture", "clean-architecture").Score("domain-complexity", score, confidence).WriteTo(cli);

        var result = await Plan(cli, script, "--on-low-confidence", "fail");

        if (expected is null)
        {
            Assert.Equal(3, result.ExitCode);
            Assert.Contains("domain-complexity: score 2.50 with confidence 0.50", result.Stderr);
        }
        else
        {
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(bool.Parse(expected), Decisions(result)["domain-complexity"]!["value"]!.GetValue<bool>());
        }
    }

    [Fact]
    public async Task A_refusal_is_low_confidence()
    {
        using var cli = NewCli();
        var script = AnswerScript.DotNetMinimalApi().Refusal("persistence").WriteTo(cli);

        var result = await Plan(cli, script, "--on-low-confidence", "default");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("fallback", Decisions(result)["persistence"]!["source"]!.GetValue<string>());
        Assert.Contains("warning: persistence is low-confidence (refused to answer); using the default ef-core-sqlite", result.Stderr);
    }

    [Fact]
    public async Task A_scripted_answer_outside_the_answer_set_names_the_entry_and_exits_with_2()
    {
        using var cli = NewCli();
        var script = AnswerScript.DotNetMinimalApi().Choice("architecture", "serverless").WriteTo(cli);

        var result = await Plan(cli, script);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(
            "error: invalid answer for architecture from the answer script: 'serverless' is not one of clean-architecture, vertical-slice, minimal-api, other\n",
            result.Stderr);
    }

    [Theory]
    [InlineData("authentication", "predicate", "probability", 1.3)]
    [InlineData("architecture", "choice", "confidence", -0.1)]
    public async Task A_scripted_probability_or_confidence_outside_0_to_1_is_rejected(string id, string type, string field, double value)
    {
        using var cli = NewCli();
        var raw = type == "predicate"
            ? new JsonObject { ["probability"] = value }
            : new JsonObject { ["choice"] = "minimal-api", ["confidence"] = value, ["probabilities"] = new JsonArray() };
        var script = AnswerScript.DotNetMinimalApi().Raw(id, raw).WriteTo(cli);

        var result = await Plan(cli, script);

        Assert.Equal(2, result.ExitCode);
        Assert.StartsWith($"error: invalid answer for {id} from the answer script: {field} ", result.Stderr);
    }

    [Fact]
    public async Task A_scripted_score_outside_the_level_range_is_rejected()
    {
        using var cli = NewCli();
        var script = AnswerScript.DotNetMinimalApi().Choice("architecture", "clean-architecture").Score("domain-complexity", 3.2, 0.9).WriteTo(cli);

        var result = await Plan(cli, script);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("error: invalid answer for domain-complexity from the answer script: score 3.2 is outside 0 to 3\n", result.Stderr);
    }

    [Fact]
    public async Task A_script_entry_with_the_wrong_fields_names_the_entry()
    {
        using var cli = NewCli();
        var script = AnswerScript.DotNetMinimalApi().Raw("architecture", new JsonObject { ["probability"] = 0.9 }).WriteTo(cli);

        var result = await Plan(cli, script);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("error: answer script entry 'architecture' is not a choice result: 'choice' is missing\n", result.Stderr);
    }

    [Fact]
    public async Task A_script_without_an_answer_for_a_pending_decision_names_it_and_exits_with_3()
    {
        using var cli = NewCli();
        var script = AnswerScript.DotNetMinimalApi().Without("persistence").Without("background-processing").WriteTo(cli);

        var result = await Plan(cli, script);

        Assert.Equal(3, result.ExitCode);
        Assert.Equal("error: the answer script has no answer for: persistence, background-processing\n", result.Stderr);
    }

    [Fact]
    public async Task A_script_that_is_not_valid_json_is_named_with_the_error_position()
    {
        using var cli = NewCli();
        cli.WriteFile("answers.json", "{ \"answers\": { \"target\": ,\n } }");

        var result = await Plan(cli, "answers.json");

        Assert.Equal(2, result.ExitCode);
        Assert.Matches(@"^error: answers\.json is not valid JSON \(line 1, position \d+\): .+\n$", result.Stderr);
    }

    [Fact]
    public async Task A_missing_script_file_is_named()
    {
        using var cli = NewCli();

        var result = await Plan(cli, "nowhere.json");

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("error: answer script not found: nowhere.json\n", result.Stderr);
    }

    [Fact]
    public async Task The_default_policy_uses_the_catalog_default_and_warns()
    {
        using var cli = NewCli();
        var script = AnswerScript.DotNetMinimalApi().Choice("architecture", "vertical-slice", 0.4).Score("domain-complexity", 1, 0.9).WriteTo(cli);

        var result = await Plan(cli, script, "--on-low-confidence", "default");

        Assert.Equal(0, result.ExitCode);
        var architecture = Decisions(result)["architecture"]!;
        Assert.Equal("clean-architecture", architecture["value"]!.GetValue<string>());
        Assert.Equal("fallback", architecture["source"]!.GetValue<string>());
        Assert.Contains("warning: architecture is low-confidence (vertical-slice with confidence 0.40); using the default clean-architecture", result.Stderr);
    }

    [Fact]
    public async Task The_fail_policy_lists_every_low_confidence_decision_in_the_level_and_writes_no_plan()
    {
        using var cli = NewCli();
        var script = AnswerScript.DotNetMinimalApi()
            .Choice("architecture", "vertical-slice", 0.5)
            .Choice("persistence", "ef-core-sqlite", 0.6)
            .WriteTo(cli);

        var result = await Plan(cli, script, "--on-low-confidence", "fail", "--out", "plan.json");

        Assert.Equal(3, result.ExitCode);
        Assert.False(cli.Exists("plan.json"));
        Assert.Equal(
            "warning: low confidence: architecture: vertical-slice with confidence 0.50 (minimum 0.70)\n"
            + "warning: low confidence: persistence: ef-core-sqlite with confidence 0.60 (minimum 0.70)\n"
            + "error: low-confidence decisions: architecture, persistence\n",
            result.Stderr);
    }

    [Fact]
    public async Task Without_a_policy_and_with_redirected_stdin_low_confidence_fails()
    {
        using var cli = NewCli();
        cli.StdinIsTerminal = false;
        cli.StderrIsTerminal = true;
        var script = AnswerScript.DotNetMinimalApi().Choice("architecture", "vertical-slice", 0.5).WriteTo(cli);

        var result = await Plan(cli, script);

        Assert.Equal(3, result.ExitCode);
        Assert.EndsWith("error: low-confidence decisions: architecture\n", result.Stderr);
    }

    [Fact]
    public async Task Min_confidence_raises_the_bar_for_choice_decisions()
    {
        using var cli = NewCli();
        var script = AnswerScript.DotNetMinimalApi().Choice("architecture", "vertical-slice", 0.85).WriteTo(cli);

        var result = await Plan(cli, script, "--min-confidence", "0.9", "--on-low-confidence", "fail");

        Assert.Equal(3, result.ExitCode);
        Assert.Contains("architecture: vertical-slice with confidence 0.85 (minimum 0.90)", result.Stderr);
    }

    [Fact]
    public async Task Min_confidence_does_not_change_predicate_thresholds()
    {
        using var cli = NewCli();
        var script = AnswerScript.DotNetMinimalApi().Predicate("authentication", 0.85).WriteTo(cli);

        var result = await Plan(cli, script, "--min-confidence", "0.9", "--on-low-confidence", "fail");

        Assert.Equal(0, result.ExitCode);
        Assert.True(Decisions(result)["authentication"]!["value"]!.GetValue<bool>());
    }

    [Fact]
    public async Task A_specification_of_exactly_256_KiB_is_accepted()
    {
        using var cli = new CliRunner();
        cli.WriteFile("spec.md", new string('a', 262_144));
        var script = AnswerScript.DotNetMinimalApi().WriteTo(cli);

        var result = await Plan(cli, script);

        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task Overrides_take_precedence_over_scripted_answers()
    {
        using var cli = NewCli();
        var script = AnswerScript.DotNetMinimalApi().WriteTo(cli);

        var result = await Plan(cli, script, "--set", "persistence=ef-core-sqlserver");

        Assert.Equal(0, result.ExitCode);
        var persistence = Decisions(result)["persistence"]!;
        Assert.Equal("ef-core-sqlserver", persistence["value"]!.GetValue<string>());
        Assert.Equal("override", persistence["source"]!.GetValue<string>());
    }

    private static CliRunner NewCli()
    {
        var cli = new CliRunner();
        cli.WriteFile("spec.md", Spec);
        return cli;
    }

    private static Task<CliResult> Plan(CliRunner cli, string script, params string[] extra) =>
        cli.RunAsync(["plan", "--spec", "spec.md", "--name", "Contoso.Orders", "--oracle-script", script, .. extra]);

    private static JsonObject Decisions(CliResult result) => JsonNode.Parse(result.Stdout)!["decisions"]!.AsObject();
}
