using System.Text.Json;
using System.Text.Json.Nodes;
using Augur.Tests.Support;
using Json.Schema;

namespace Augur.Tests.Acceptance;

public sealed class PlanValidationTests
{
    [Fact]
    public async Task Schema_plan_prints_a_draft_2020_12_json_schema()
    {
        using var cli = new CliRunner();

        var result = await cli.RunAsync("schema", "plan");

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Stderr);
        var schema = JsonNode.Parse(result.Stdout)!;
        Assert.Equal("https://json-schema.org/draft/2020-12/schema", schema["$schema"]!.GetValue<string>());
        LoadSchema(result.Stdout);
    }

    [Theory]
    [InlineData("dotnet")]
    [InlineData("angular")]
    [InlineData("fullstack")]
    public async Task Every_plan_augur_writes_passes_the_schema(string target)
    {
        using var cli = new CliRunner();
        var schema = LoadSchema((await cli.RunAsync("schema", "plan")).Stdout);
        var plan = await WritePlan(cli, AnswerScript.FullstackCleanArchitecture().Choice("target", target));

        var evaluation = schema.Evaluate(JsonDocument.Parse(plan).RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });

        Assert.True(evaluation.IsValid, JsonSerializer.Serialize(evaluation));
    }

    [Fact]
    public async Task A_value_outside_the_answer_set_fails_the_schema()
    {
        using var cli = new CliRunner();
        var schema = LoadSchema((await cli.RunAsync("schema", "plan")).Stdout);
        var plan = JsonNode.Parse(await WritePlan(cli, AnswerScript.FullstackCleanArchitecture()))!;
        plan["decisions"]!["architecture"]!["value"] = "hexagonal";

        Assert.False(schema.Evaluate(JsonDocument.Parse(plan.ToJsonString()).RootElement).IsValid);
    }

    [Fact]
    public async Task Emit_rejects_a_value_outside_the_answer_set_with_its_json_path_and_allowed_values()
    {
        using var cli = new CliRunner();
        await WriteEditedPlan(cli, plan => plan["decisions"]!["architecture"]!["value"] = "hexagonal");

        var result = await cli.RunAsync("emit", "--plan", "plan.json", "--out", "out");

        Assert.Equal(2, result.ExitCode);
        Assert.False(cli.Exists("out"));
        Assert.Equal(
            "  $.decisions.architecture.value: 'hexagonal' is not an allowed value for architecture; allowed values: clean-architecture, vertical-slice, minimal-api\n"
            + "error: plan.json is not a valid plan (1 problem)\n",
            result.Stderr);
    }

    [Fact]
    public async Task Emit_rejects_other_as_a_value()
    {
        using var cli = new CliRunner();
        await WriteEditedPlan(cli, plan => plan["decisions"]!["persistence"]!["value"] = "other");

        var result = await cli.RunAsync("emit", "--plan", "plan.json", "--out", "out");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("  $.decisions.persistence.value: 'other' cannot be used as a value for persistence\n", result.Stderr);
    }

    [Fact]
    public async Task Emit_rejects_a_value_for_a_decision_that_does_not_apply()
    {
        using var cli = new CliRunner();
        await WriteEditedPlan(cli, plan =>
        {
            plan["decisions"]!["architecture"]!["value"] = "minimal-api";
        });

        var result = await cli.RunAsync("emit", "--plan", "plan.json", "--out", "out");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("  $.decisions.domain-complexity: domain-complexity must be absent when architecture is minimal-api\n", result.Stderr);
    }

    [Fact]
    public async Task Emit_rejects_a_missing_decision_that_applies()
    {
        using var cli = new CliRunner();
        await WriteEditedPlan(cli, plan =>
        {
            plan["decisions"]!.AsObject().Remove("persistence");
            plan["decisions"]!.AsObject().Remove("target");
        });

        var result = await cli.RunAsync("emit", "--plan", "plan.json", "--out", "out");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("  $.decisions.target: target is required\n", result.Stderr);
        Assert.Contains("error: plan.json is not a valid plan (", result.Stderr);
    }

    [Fact]
    public async Task Emit_rejects_a_catalog_version_mismatch()
    {
        using var cli = new CliRunner();
        await WriteEditedPlan(cli, plan => plan["catalogVersion"] = 0);

        var result = await cli.RunAsync("emit", "--plan", "plan.json", "--out", "out");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("  $.catalogVersion: catalogVersion 0 does not match the installed catalog version 2\n", result.Stderr);
    }

    [Theory]
    [InlineData("planVersion", "2", "  $.planVersion: planVersion 2 is not supported; expected 1\n")]
    [InlineData("solutionName", "\"contoso\"", "  $.solutionName: solutionName must be dot-separated segments")]
    [InlineData("inputHash", "\"abc\"", "  $.inputHash: inputHash must be 64 lower-case hexadecimal characters\n")]
    [InlineData("extra", "1", "  $.extra: unknown property\n")]
    public async Task Emit_rejects_malformed_top_level_properties(string property, string json, string expected)
    {
        using var cli = new CliRunner();
        await WriteEditedPlan(cli, plan => plan[property] = JsonNode.Parse(json));

        var result = await cli.RunAsync("emit", "--plan", "plan.json", "--out", "out");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains(expected, result.Stderr);
    }

    [Fact]
    public async Task Emit_reports_every_problem_at_once()
    {
        using var cli = new CliRunner();
        await WriteEditedPlan(cli, plan =>
        {
            plan["catalogVersion"] = 1;
            plan["decisions"]!["ui-library"]!["value"] = "bootstrap";
            plan["decisions"]!["colour"] = new JsonObject { ["value"] = "blue", ["source"] = "override" };
        });

        var result = await cli.RunAsync("emit", "--plan", "plan.json", "--out", "out");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("$.catalogVersion:", result.Stderr);
        Assert.Contains("$.decisions.colour: 'colour' is not a catalog decision", result.Stderr);
        Assert.Contains("$.decisions.ui-library.value:", result.Stderr);
        Assert.EndsWith("error: plan.json is not a valid plan (3 problems)\n", result.Stderr);
    }

    [Fact]
    public async Task Emit_rejects_a_plan_over_1_MiB_before_parsing_it()
    {
        using var cli = new CliRunner();
        cli.WriteFile("plan.json", "{\"pad\": \"" + new string('x', 1_048_577) + "\"}");

        var result = await cli.RunAsync("emit", "--plan", "plan.json", "--out", "out");

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("error: plan.json exceeds 1 MiB\n", result.Stderr);
    }

    [Fact]
    public async Task Emit_names_a_plan_that_is_not_valid_json()
    {
        using var cli = new CliRunner();
        cli.WriteFile("plan.json", "{ \"planVersion\": 1, }");

        var result = await cli.RunAsync("emit", "--plan", "plan.json", "--out", "out");

        Assert.Equal(2, result.ExitCode);
        Assert.Matches(@"^error: plan\.json is not valid JSON \(line 1, position \d+\): .+\n$", result.Stderr);
    }

    [Fact]
    public async Task Emit_names_a_missing_plan_file()
    {
        using var cli = new CliRunner();

        var result = await cli.RunAsync("emit", "--plan", "nowhere.json", "--out", "out");

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("error: plan file not found: nowhere.json\n", result.Stderr);
    }

    /// <summary>Each schema gets its own registry: the library's global one refuses to register the same $id twice.</summary>
    private static JsonSchema LoadSchema(string text) => JsonSchema.FromText(text, new BuildOptions { SchemaRegistry = new SchemaRegistry() });

    private static async Task<string> WritePlan(CliRunner cli, AnswerScript answers)
    {
        cli.WriteFile("spec.md", "Build an order tracking app.\n");
        var script = answers.WriteTo(cli);
        var result = await cli.RunAsync("plan", "--spec", "spec.md", "--name", "Contoso.Orders", "--oracle-script", script, "--lock", $"{Guid.NewGuid():N}.json");
        Assert.Equal(0, result.ExitCode);
        return result.Stdout;
    }

    private static async Task WriteEditedPlan(CliRunner cli, Action<JsonNode> edit)
    {
        var plan = JsonNode.Parse(await WritePlan(cli, AnswerScript.FullstackCleanArchitecture()))!;
        edit(plan);
        cli.WriteFile("plan.json", plan.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }
}
