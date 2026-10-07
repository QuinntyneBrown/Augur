using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Augur.Tests.Support;

namespace Augur.Tests.Acceptance;

public sealed class PlanFromOverridesTests
{
    private const string Spec = "Build an order tracking API for a small shop.\n";

    private static readonly string[] DotNetMinimalApi =
    [
        "--set", "target=dotnet",
        "--set", "authentication=false",
        "--set", "architecture=minimal-api",
        "--set", "persistence=none",
        "--set", "background-processing=false",
    ];

    private static readonly string[] Angular =
    [
        "--set", "target=angular",
        "--set", "authentication=true",
        "--set", "ui-library=angular-material",
        "--set", "state-management=signals",
        "--set", "server-side-rendering=false",
    ];

    [Fact]
    public async Task A_fully_overridden_plan_is_written_to_stdout_in_canonical_form()
    {
        using var cli = SpecIn(new CliRunner());

        var result = await cli.RunAsync(["plan", "--spec", "spec.md", "--name", "Contoso.Orders", .. DotNetMinimalApi]);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(
            $$"""
            {
              "catalogVersion": 2,
              "decisions": {
                "architecture": {
                  "source": "override",
                  "value": "minimal-api"
                },
                "authentication": {
                  "source": "override",
                  "value": false
                },
                "background-processing": {
                  "source": "override",
                  "value": false
                },
                "persistence": {
                  "source": "override",
                  "value": "none"
                },
                "target": {
                  "source": "override",
                  "value": "dotnet"
                }
              },
              "inputHash": "{{Sha256(Spec)}}",
              "planVersion": 1,
              "solutionName": "Contoso.Orders"
            }

            """.ReplaceLineEndings("\n"),
            result.Stdout);
    }

    [Fact]
    public async Task Decisions_that_do_not_apply_to_the_target_take_no_value()
    {
        using var cli = SpecIn(new CliRunner());

        var result = await cli.RunAsync(["plan", "--spec", "spec.md", "--name", "Contoso.Web", .. Angular]);

        Assert.Equal(0, result.ExitCode);
        var decisions = JsonNode.Parse(result.Stdout)!["decisions"]!.AsObject();
        Assert.Equal(
            ["authentication", "server-side-rendering", "state-management", "target", "ui-library"],
            decisions.Select(d => d.Key));
    }

    [Fact]
    public async Task The_plan_is_byte_identical_across_runs()
    {
        using var cli = SpecIn(new CliRunner());
        string[] args = ["plan", "--spec", "spec.md", "--name", "Contoso.Orders", .. DotNetMinimalApi];

        var first = await cli.RunAsync(args);
        var second = await cli.RunAsync(args);

        Assert.Equal(first.Stdout, second.Stdout);
    }

    [Fact]
    public async Task With_out_the_plan_is_written_to_the_file_and_stdout_is_empty()
    {
        using var cli = SpecIn(new CliRunner());

        var stdoutRun = await cli.RunAsync(["plan", "--spec", "spec.md", "--name", "Contoso.Orders", .. DotNetMinimalApi]);
        var fileRun = await cli.RunAsync(["plan", "--spec", "spec.md", "--name", "Contoso.Orders", "--out", "plans/plan.json", .. DotNetMinimalApi]);

        Assert.Equal(0, fileRun.ExitCode);
        Assert.Empty(fileRun.Stdout);
        Assert.Equal(stdoutRun.Stdout, cli.ReadFile("plans/plan.json"));
    }

    [Fact]
    public async Task A_plan_from_stdin_is_identical_to_one_from_a_file()
    {
        using var cli = SpecIn(new CliRunner());

        var fromFile = await cli.RunAsync(["plan", "--spec", "spec.md", "--name", "Contoso.Orders", .. DotNetMinimalApi]);
        var fromStdin = await cli.RunWithStdinAsync(Spec, ["plan", "--spec", "-", "--name", "Contoso.Orders", .. DotNetMinimalApi]);

        Assert.Equal(0, fromStdin.ExitCode);
        Assert.Equal(fromFile.Stdout, fromStdin.Stdout);
    }

    [Fact]
    public async Task A_fully_overridden_plan_needs_no_api_key()
    {
        using var cli = SpecIn(new CliRunner()).WithEnv("OPENAI_API_KEY", null);

        var result = await cli.RunAsync(["plan", "--spec", "spec.md", "--name", "Contoso.Orders", .. DotNetMinimalApi]);

        Assert.Equal(0, result.ExitCode);
    }

    [Theory]
    [InlineData("Contoso.Orders")]
    [InlineData("Acme")]
    [InlineData("Contoso2.Web3")]
    public async Task A_valid_solution_name_is_accepted(string name)
    {
        using var cli = SpecIn(new CliRunner());

        var result = await cli.RunAsync(["plan", "--spec", "spec.md", "--name", name, .. DotNetMinimalApi]);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(name, JsonNode.Parse(result.Stdout)!["solutionName"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("../evil")]
    [InlineData("contoso")]
    [InlineData("Contoso..Orders")]
    [InlineData("Contoso.Class")]
    [InlineData("Contoso.class")]
    [InlineData("Abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcde")]
    public async Task An_invalid_solution_name_is_rejected_with_the_naming_rule(string name)
    {
        using var cli = SpecIn(new CliRunner());

        var result = await cli.RunAsync(["plan", "--spec", "spec.md", "--name", name, .. DotNetMinimalApi]);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(
            "error: --name must be dot-separated segments that each start with an upper-case letter followed by letters or digits, "
            + "at most 64 characters, with no segment that is a C# keyword\n",
            result.Stderr);
    }

    [Fact]
    public async Task An_unknown_decision_id_is_rejected_with_the_valid_ids()
    {
        using var cli = SpecIn(new CliRunner());

        var result = await cli.RunAsync("plan", "--spec", "spec.md", "--name", "Contoso.Orders", "--set", "colour=blue");

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(
            "error: 'colour' is not a catalog decision; valid ids: target, authentication, architecture, persistence, "
            + "background-processing, domain-complexity, ui-library, state-management, server-side-rendering\n",
            result.Stderr);
    }

    [Fact]
    public async Task A_value_outside_the_answer_set_is_rejected_with_the_allowed_values()
    {
        using var cli = SpecIn(new CliRunner());

        var result = await cli.RunAsync("plan", "--spec", "spec.md", "--name", "Contoso.Orders", "--set", "architecture=hexagonal");

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(
            "error: 'hexagonal' is not an allowed value for architecture; allowed values: clean-architecture, vertical-slice, minimal-api\n",
            result.Stderr);
    }

    [Fact]
    public async Task Other_cannot_be_set_explicitly()
    {
        using var cli = SpecIn(new CliRunner());

        var result = await cli.RunAsync("plan", "--spec", "spec.md", "--name", "Contoso.Orders", "--set", "architecture=other");

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("error: 'other' cannot be set explicitly for architecture\n", result.Stderr);
    }

    [Fact]
    public async Task An_override_without_an_equals_sign_is_rejected()
    {
        using var cli = SpecIn(new CliRunner());

        var result = await cli.RunAsync("plan", "--spec", "spec.md", "--name", "Contoso.Orders", "--set", "architecture");

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("error: --set expects <id>=<value>, got 'architecture'\n", result.Stderr);
    }

    [Fact]
    public async Task An_override_for_a_decision_that_does_not_apply_is_rejected()
    {
        using var cli = SpecIn(new CliRunner());

        var result = await cli.RunAsync("plan", "--spec", "spec.md", "--name", "Contoso.Orders", "--set", "target=angular", "--set", "architecture=minimal-api");

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("error: architecture does not apply when target is angular\n", result.Stderr);
    }

    [Fact]
    public async Task An_override_for_a_decision_whose_dependency_does_not_apply_is_rejected()
    {
        using var cli = SpecIn(new CliRunner());

        var result = await cli.RunAsync("plan", "--spec", "spec.md", "--name", "Contoso.Orders", "--set", "target=angular", "--set", "domain-complexity=true");

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("error: domain-complexity does not apply because architecture does not apply when target is angular\n", result.Stderr);
    }

    [Fact]
    public async Task Decisions_with_no_source_of_answers_are_reported_as_unresolved()
    {
        using var cli = SpecIn(new CliRunner()).WithEnv("OPENAI_API_KEY", null);

        var result = await cli.RunAsync("plan", "--spec", "spec.md", "--name", "Contoso.Orders", "--set", "target=dotnet");

        Assert.Equal(3, result.ExitCode);
        Assert.Empty(result.Stdout);
        Assert.EndsWith("error: unresolved decisions: authentication\n", result.Stderr);
    }

    [Fact]
    public async Task A_score_decision_can_be_overridden_with_a_boolean()
    {
        using var cli = SpecIn(new CliRunner());

        var result = await cli.RunAsync(
            "plan", "--spec", "spec.md", "--name", "Contoso.Orders",
            "--set", "target=dotnet", "--set", "authentication=true", "--set", "architecture=clean-architecture",
            "--set", "persistence=ef-core-postgresql", "--set", "background-processing=false", "--set", "domain-complexity=true");

        Assert.Equal(0, result.ExitCode);
        var complexity = JsonNode.Parse(result.Stdout)!["decisions"]!["domain-complexity"]!;
        Assert.True(complexity["value"]!.GetValue<bool>());
        Assert.Equal("override", complexity["source"]!.GetValue<string>());
    }

    private static CliRunner SpecIn(CliRunner cli)
    {
        cli.WriteFile("spec.md", Spec);
        return cli;
    }

    private static string Sha256(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
