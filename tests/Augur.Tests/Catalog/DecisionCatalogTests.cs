using Augur.Core;
using Augur.Core.Catalog;

namespace Augur.Tests.Catalog;

public sealed class DecisionCatalogTests
{
    private static readonly DecisionCatalog Catalog = DecisionCatalog.BuiltIn;

    [Fact]
    public void Catalog_version_2_contains_exactly_the_nine_decisions_in_order()
    {
        Assert.Equal(2, Catalog.Version);
        Assert.Equal(
            ["target", "authentication", "architecture", "persistence", "background-processing", "domain-complexity", "ui-library", "state-management", "server-side-rendering"],
            Catalog.Decisions.Select(d => d.Id));
    }

    [Theory]
    [InlineData("target", DecisionType.Choice, "fullstack,dotnet,angular,other", "fullstack", null, null)]
    [InlineData("authentication", DecisionType.Predicate, "true,false", "false", null, null)]
    [InlineData("architecture", DecisionType.Choice, "clean-architecture,vertical-slice,minimal-api,other", "clean-architecture", "target", "fullstack,dotnet")]
    [InlineData("persistence", DecisionType.Choice, "ef-core-sqlserver,ef-core-postgresql,ef-core-sqlite,none,other", "ef-core-sqlite", "target", "fullstack,dotnet")]
    [InlineData("background-processing", DecisionType.Predicate, "true,false", "false", "target", "fullstack,dotnet")]
    [InlineData("domain-complexity", DecisionType.Score, "true,false", "false", "architecture", "clean-architecture,vertical-slice")]
    [InlineData("ui-library", DecisionType.Choice, "angular-material,none,other", "angular-material", "target", "fullstack,angular")]
    [InlineData("state-management", DecisionType.Choice, "signals,ngrx-signal-store,other", "signals", "target", "fullstack,angular")]
    [InlineData("server-side-rendering", DecisionType.Predicate, "true,false", "false", "target", "fullstack,angular")]
    public void Each_decision_has_the_answers_default_and_dependency_from_the_spec(
        string id, DecisionType type, string answers, string @default, string? dependsOn, string? acceptedValues)
    {
        var decision = Catalog.Get(id);

        Assert.Equal(type, decision.Type);
        Assert.Equal(answers.Split(','), decision.Answers);
        Assert.Equal(@default, decision.Default);
        Assert.Equal(dependsOn, decision.When?.DependsOn);
        Assert.Equal(acceptedValues?.Split(','), decision.When?.AcceptedValues);
    }

    [Fact]
    public void Domain_complexity_scores_four_levels_and_enables_cqrs_from_business_rules_up()
    {
        var decision = Assert.IsType<ScoreDefinition>(Catalog.Get("domain-complexity"));

        Assert.Equal(["trivial", "crud", "business-rules", "complex-domain"], decision.Levels.Select(l => l.Label));
        Assert.Equal(2.0, decision.CutOff);
        Assert.Equal("cqrs", decision.ResolvedId);
    }

    [Fact]
    public void Every_choice_decision_offers_other()
    {
        foreach (var choice in Catalog.Decisions.OfType<ChoiceDefinition>())
        {
            Assert.Contains("other", choice.Answers);
        }
    }

    [Fact]
    public void Every_option_and_level_has_a_description_and_every_default_is_an_answer()
    {
        foreach (var decision in Catalog.Decisions)
        {
            Assert.False(string.IsNullOrWhiteSpace(decision.Instructions), decision.Id);
            Assert.Contains(decision.Default, decision.Answers);
            var descriptions = decision switch
            {
                ChoiceDefinition c => c.Options.Select(o => o.Description),
                ScoreDefinition s => s.Levels.Select(l => l.Description),
                _ => [],
            };
            Assert.All(descriptions, d => Assert.False(string.IsNullOrWhiteSpace(d), decision.Id));
        }
    }

    [Fact]
    public void The_shipped_thresholds_match_the_spec()
    {
        var authentication = Assert.IsType<PredicateDefinition>(Catalog.Get("authentication"));
        Assert.Equal(0.80, authentication.UpperThreshold);
        Assert.Equal(0.20, authentication.LowerThreshold);
        Assert.Equal(0.70, Assert.IsType<ChoiceDefinition>(Catalog.Get("architecture")).MinConfidence);
        Assert.Equal(0.70, Assert.IsType<ScoreDefinition>(Catalog.Get("domain-complexity")).MinConfidence);
    }

    [Fact]
    public void A_when_clause_that_references_an_undefined_decision_is_rejected_naming_the_decision()
    {
        var definitions = CatalogVersion2.Build()
            .Select(d => d.Id == "persistence" ? d with { When = new WhenClause("platform", ["dotnet"]) } : d)
            .ToList();

        var error = Assert.Throws<InvalidCatalogException>(() => DecisionCatalog.Create(2, definitions));

        Assert.Equal("persistence", error.DecisionId);
        Assert.Equal(ExitCode.InternalError, error.ExitCode);
        Assert.Contains("persistence", error.Message);
    }

    [Fact]
    public void A_cycle_in_when_clauses_is_rejected_naming_a_decision_in_the_cycle()
    {
        var definitions = CatalogVersion2.Build()
            .Select(d => d.Id == "target" ? d with { When = new WhenClause("architecture", ["minimal-api"]) } : d)
            .ToList();

        var error = Assert.Throws<InvalidCatalogException>(() => DecisionCatalog.Create(2, definitions));

        Assert.Contains(error.DecisionId, new[] { "target", "architecture" });
    }

    [Fact]
    public void A_default_outside_the_answer_set_is_rejected()
    {
        var definitions = CatalogVersion2.Build()
            .Select(d => d.Id == "persistence" ? d with { Default = "mongodb" } : d)
            .ToList();

        var error = Assert.Throws<InvalidCatalogException>(() => DecisionCatalog.Create(2, definitions));

        Assert.Equal("persistence", error.DecisionId);
    }

    [Fact]
    public void A_choice_without_other_is_rejected()
    {
        var definitions = CatalogVersion2.Build()
            .Select(d => d is ChoiceDefinition { Id: "ui-library" } c ? c with { Options = [.. c.Options.Where(o => o.Value != "other")] } : d)
            .ToList();

        var error = Assert.Throws<InvalidCatalogException>(() => DecisionCatalog.Create(2, definitions));

        Assert.Equal("ui-library", error.DecisionId);
    }

    [Fact]
    public void A_duplicate_id_or_an_empty_description_is_rejected()
    {
        var duplicate = CatalogVersion2.Build().Append(CatalogVersion2.Build()[0]).ToList();
        Assert.Equal("target", Assert.Throws<InvalidCatalogException>(() => DecisionCatalog.Create(2, duplicate)).DecisionId);

        var emptyDescription = CatalogVersion2.Build()
            .Select(d => d is ChoiceDefinition { Id: "state-management" } c
                ? c with { Options = [.. c.Options.Select(o => o.Value == "signals" ? o with { Description = " " } : o)] }
                : d)
            .ToList();
        Assert.Equal("state-management", Assert.Throws<InvalidCatalogException>(() => DecisionCatalog.Create(2, emptyDescription)).DecisionId);
    }
}
