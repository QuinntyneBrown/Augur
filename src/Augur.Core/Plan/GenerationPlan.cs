using System.Text.Json.Nodes;
using Augur.Core.Catalog;
using Augur.Core.Decisions;

namespace Augur.Core.Plan;

/// <summary>
/// The contract between deciding and emitting: every resolved decision, the catalog it was resolved against,
/// the solution name, and the hash of the input it came from.
/// </summary>
public sealed record GenerationPlan(
    int CatalogVersion,
    SolutionName SolutionName,
    string InputHash,
    IReadOnlyDictionary<string, PlanDecision> Decisions)
{
    public const int CurrentPlanVersion = 1;

    public int PlanVersion { get; init; } = CurrentPlanVersion;

    public string? ValueOf(string id) => Decisions.GetValueOrDefault(id)?.Value;

    /// <summary>Builds the plan from every resolved decision; skipped decisions take no value.</summary>
    public static GenerationPlan From(DecisionState state, SolutionName name, string inputHash, DecisionCatalog catalog) =>
        new(
            catalog.Version,
            name,
            inputHash,
            state.Resolved.ToDictionary(
                d => d.Id,
                d => new PlanDecision(d.Value, d.Raw?.Replayed?.OriginalSource ?? d.Source),
                StringComparer.Ordinal));

    public string ToJson(DecisionCatalog catalog) => CanonicalJson.Serialize(new JsonObject
    {
        ["planVersion"] = PlanVersion,
        ["catalogVersion"] = CatalogVersion,
        ["solutionName"] = SolutionName.Value,
        ["inputHash"] = InputHash,
        ["decisions"] = new JsonObject(Decisions.Select(d => KeyValuePair.Create(
            d.Key,
            (JsonNode?)new JsonObject
            {
                ["value"] = catalog.TryGet(d.Key, out var definition)
                    ? DecisionValues.ToJson(definition, d.Value.Value)
                    : JsonValue.Create(d.Value.Value),
                ["source"] = d.Value.Source.ToWireName(),
            }))),
    });
}

/// <summary>One decision's value in a plan, and where it came from.</summary>
public sealed record PlanDecision(string Value, DecisionSource Source);
