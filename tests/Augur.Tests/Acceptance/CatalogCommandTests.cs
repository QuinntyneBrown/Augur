using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Augur.Tests.Support;

namespace Augur.Tests.Acceptance;

public sealed class CatalogCommandTests
{
    public static TheoryData<string, string, string, string, string?> Decisions => new()
    {
        { "target", "choice", "fullstack, dotnet, angular, other", "fullstack", null },
        { "authentication", "predicate", "true, false", "false", null },
        { "architecture", "choice", "clean-architecture, vertical-slice, minimal-api, other", "clean-architecture", "target is fullstack or dotnet" },
        { "persistence", "choice", "ef-core-sqlserver, ef-core-postgresql, ef-core-sqlite, none, other", "ef-core-sqlite", "target is fullstack or dotnet" },
        { "background-processing", "predicate", "true, false", "false", "target is fullstack or dotnet" },
        { "domain-complexity", "score", "true, false", "false", "architecture is clean-architecture or vertical-slice" },
        { "ui-library", "choice", "angular-material, none, other", "angular-material", "target is fullstack or angular" },
        { "state-management", "choice", "signals, ngrx-signal-store, other", "signals", "target is fullstack or angular" },
        { "server-side-rendering", "predicate", "true, false", "false", "target is fullstack or angular" },
    };

    [Theory]
    [MemberData(nameof(Decisions))]
    public async Task Catalog_lists_each_decision_with_its_type_answers_default_and_dependency(
        string id, string type, string answers, string @default, string? when)
    {
        using var cli = new CliRunner();

        var result = await cli.RunAsync("catalog");

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Stderr);
        var block = Block(result.Stdout, id);
        Assert.Matches($@"(?m)^  type\s+{type}$", block);
        Assert.Matches($@"(?m)^  answers\s+{Regex.Escape(answers)}$", block);
        Assert.Matches($@"(?m)^  default\s+{Regex.Escape(@default)}$", block);
        Assert.Matches($@"(?m)^  when\s+{Regex.Escape(when ?? "always")}$", block);
    }

    [Fact]
    public async Task Catalog_lists_the_levels_and_cut_off_of_the_score_decision()
    {
        using var cli = new CliRunner();

        var result = await cli.RunAsync("catalog");

        var block = Block(result.Stdout, "domain-complexity");
        Assert.Matches(@"(?m)^  levels\s+0 trivial, 1 crud, 2 business-rules, 3 complex-domain$", block);
        Assert.Matches(@"(?m)^  resolves\s+true when the score is 2 or more \(cqrs\)$", block);
    }

    [Fact]
    public async Task Catalog_json_lists_the_catalog_version_and_all_nine_decisions()
    {
        using var cli = new CliRunner();

        var result = await cli.RunAsync("catalog", "--json");

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Stderr);
        Assert.EndsWith("}\n", result.Stdout);
        var json = JsonNode.Parse(result.Stdout)!;
        Assert.Equal(2, json["catalogVersion"]!.GetValue<int>());
        var decisions = json["decisions"]!.AsArray();
        Assert.Equal(9, decisions.Count);

        var architecture = decisions.Single(d => d!["id"]!.GetValue<string>() == "architecture")!;
        Assert.Equal("choice", architecture["type"]!.GetValue<string>());
        Assert.Equal(["clean-architecture", "vertical-slice", "minimal-api", "other"], architecture["answers"]!.AsArray().Select(a => a!.GetValue<string>()));
        Assert.Equal("clean-architecture", architecture["default"]!.GetValue<string>());
        Assert.Equal("target", architecture["when"]!["dependsOn"]!.GetValue<string>());
        Assert.Equal(["fullstack", "dotnet"], architecture["when"]!["acceptedValues"]!.AsArray().Select(a => a!.GetValue<string>()));

        var authentication = decisions.Single(d => d!["id"]!.GetValue<string>() == "authentication")!;
        Assert.Equal([true, false], authentication["answers"]!.AsArray().Select(a => a!.GetValue<bool>()));
        Assert.False(authentication["default"]!.GetValue<bool>());
        Assert.Null(authentication["when"]);
    }

    [Fact]
    public async Task Catalog_json_is_identical_across_runs()
    {
        using var cli = new CliRunner();

        var first = await cli.RunAsync("catalog", "--json");
        var second = await cli.RunAsync("catalog", "--json");

        Assert.Equal(first.Stdout, second.Stdout);
        Assert.DoesNotContain("\r", first.Stdout);
    }

    private static string Block(string listing, string id)
    {
        var match = Regex.Match(listing, $@"(?ms)^{Regex.Escape(id)}\n(.*?)(?=^\S|\z)");
        Assert.True(match.Success, $"no block for {id} in:\n{listing}");
        return match.Groups[1].Value;
    }
}
