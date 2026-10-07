using System.Text.Json.Nodes;
using Augur.Tests.Support;

namespace Augur.Tests.Acceptance;

public sealed class InteractivePromptTests
{
    private static readonly JsonObject LowConfidencePersistence = new()
    {
        ["choice"] = "ef-core-sqlite",
        ["confidence"] = 0.55,
        ["probabilities"] = new JsonArray(
            new JsonObject { ["value"] = "ef-core-sqlite", ["probability"] = 0.55 },
            new JsonObject { ["value"] = "ef-core-postgresql", ["probability"] = 0.30 },
            new JsonObject { ["value"] = "ef-core-sqlserver", ["probability"] = 0.10 },
            new JsonObject { ["value"] = "other", ["probability"] = 0.05 }),
    };

    [Fact]
    public async Task The_prompt_lists_the_answers_by_probability_without_other()
    {
        using var cli = NewCli();

        var result = await Plan(cli, "2\n");

        Assert.Contains(
            "persistence is low-confidence: ef-core-sqlite with confidence 0.55 (minimum 0.70)\n"
            + "Decide how the .NET backend should store its data.\n"
            + "  1) ef-core-sqlite      0.55\n"
            + "  2) ef-core-postgresql  0.30\n"
            + "  3) ef-core-sqlserver   0.10\n"
            + "  4) none                0.00\n"
            + "Enter a number or a value [default: ef-core-sqlite]: ",
            result.Stderr);
        Assert.DoesNotContain(") other", result.Stderr);
    }

    [Fact]
    public async Task Entering_a_number_uses_that_listed_answer_with_source_user()
    {
        using var cli = NewCli();

        var result = await Plan(cli, "2\n");

        Assert.Equal(0, result.ExitCode);
        AssertPersistence(result, "ef-core-postgresql");
    }

    [Fact]
    public async Task Entering_a_value_uses_it_with_source_user()
    {
        using var cli = NewCli();

        var result = await Plan(cli, "ef-core-postgresql\n");

        Assert.Equal(0, result.ExitCode);
        AssertPersistence(result, "ef-core-postgresql");
    }

    [Fact]
    public async Task An_empty_line_uses_the_default_with_source_user()
    {
        using var cli = NewCli();

        var result = await Plan(cli, "\n");

        Assert.Equal(0, result.ExitCode);
        AssertPersistence(result, "ef-core-sqlite");
    }

    [Fact]
    public async Task Three_invalid_entries_are_each_reported_and_then_the_run_exits_with_3()
    {
        using var cli = NewCli();

        var result = await Plan(cli, "9\nfoo\nother\n");

        Assert.Equal(3, result.ExitCode);
        Assert.Empty(result.Stdout);
        Assert.Contains("'9' is not one of the listed answers", result.Stderr);
        Assert.Contains("'foo' is not one of the listed answers", result.Stderr);
        Assert.Contains("'other' is not one of the listed answers", result.Stderr);
        Assert.EndsWith("error: no valid answer for persistence after 3 attempts\n", result.Stderr);
    }

    [Fact]
    public async Task A_closed_stdin_leaves_the_decision_unresolved()
    {
        using var cli = NewCli();

        var result = await Plan(cli, "");

        Assert.Equal(3, result.ExitCode);
        Assert.EndsWith("error: no answer for persistence: stdin is closed\n", result.Stderr);
    }

    [Fact]
    public async Task A_predicate_prompt_offers_true_and_false_with_complementary_probabilities()
    {
        using var cli = NewCli();
        var script = AnswerScript.DotNetMinimalApi().Predicate("authentication", 0.35).WriteTo(cli);

        var result = await cli.RunAsync(["plan", "--spec", "spec.md", "--name", "Contoso.Orders", "--oracle-script", script], "1\n"u8.ToArray(), CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("  1) false  0.65\n  2) true   0.35\n", result.Stderr);
        Assert.False(JsonNode.Parse(result.Stdout)!["decisions"]!["authentication"]!["value"]!.GetValue<bool>());
    }

    private static CliRunner NewCli()
    {
        var cli = new CliRunner { StdinIsTerminal = true, StderrIsTerminal = true };
        cli.WriteFile("spec.md", "Build an order tracking API.\n");
        AnswerScript.DotNetMinimalApi().Raw("persistence", LowConfidencePersistence).WriteTo(cli);
        return cli;
    }

    private static Task<CliResult> Plan(CliRunner cli, string stdin) =>
        cli.RunWithStdinAsync(stdin, "plan", "--spec", "spec.md", "--name", "Contoso.Orders", "--oracle-script", "answers.json");

    private static void AssertPersistence(CliResult result, string expected)
    {
        var persistence = JsonNode.Parse(result.Stdout)!["decisions"]!["persistence"]!;
        Assert.Equal(expected, persistence["value"]!.GetValue<string>());
        Assert.Equal("user", persistence["source"]!.GetValue<string>());
    }
}
