using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace Augur.Core.Catalog;

/// <summary>Renders the catalog for <c>augur catalog</c>, as text or as JSON.</summary>
public static class CatalogListing
{
    private const int LabelWidth = 10;

    public static string RenderText(DecisionCatalog catalog)
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"catalog version {catalog.Version}\n");
        foreach (var decision in catalog.Decisions)
        {
            text.Append('\n').Append(decision.Id).Append('\n');
            Row(text, "type", decision.Type.ToWireName());
            if (decision is ScoreDefinition score)
            {
                Row(text, "levels", string.Join(", ", score.Levels.Select((l, i) => $"{i} {l.Label}")));
                Row(text, "resolves", string.Create(CultureInfo.InvariantCulture, $"true when the score is {score.CutOff} or more ({score.ResolvedId})"));
            }

            Row(text, "answers", string.Join(", ", decision.Answers));
            Row(text, "default", decision.Default);
            Row(text, "when", decision.When?.ToText() ?? "always");
        }

        return text.ToString();
    }

    public static string RenderJson(DecisionCatalog catalog) => CanonicalJson.Serialize(new JsonObject
    {
        ["catalogVersion"] = catalog.Version,
        ["decisions"] = new JsonArray([.. catalog.Decisions.Select(ToJson)]),
    });

    private static JsonObject ToJson(DecisionDefinition decision)
    {
        var json = new JsonObject
        {
            ["id"] = decision.Id,
            ["type"] = decision.Type.ToWireName(),
            ["answers"] = new JsonArray([.. decision.Answers.Select(a => DecisionValues.ToJson(decision, a))]),
            ["default"] = DecisionValues.ToJson(decision, decision.Default),
            ["when"] = decision.When is { } when
                ? new JsonObject
                {
                    ["dependsOn"] = when.DependsOn,
                    ["acceptedValues"] = new JsonArray([.. when.AcceptedValues.Select(v => (JsonNode?)v)]),
                }
                : null,
        };
        if (decision is ScoreDefinition score)
        {
            json["levels"] = new JsonArray([.. score.Levels.Select(l => (JsonNode?)l.Label)]);
            json["cutOff"] = score.CutOff;
        }

        return json;
    }

    private static void Row(StringBuilder text, string label, string value) =>
        text.Append("  ").Append(label.PadRight(LabelWidth)).Append(value).Append('\n');
}
