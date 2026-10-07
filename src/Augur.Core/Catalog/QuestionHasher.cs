using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Augur.Core.Catalog;

/// <summary>
/// SHA-256 over the canonical JSON of the parts of a decision that the model sees or that decide its answer:
/// type, instructions, options or levels with their descriptions, and thresholds. Id, default, and when are excluded.
/// </summary>
public static class QuestionHasher
{
    public static string Hash(DecisionDefinition definition)
    {
        var json = new JsonObject
        {
            ["type"] = definition.Type.ToWireName(),
            ["instructions"] = definition.Instructions,
        };
        switch (definition)
        {
            case PredicateDefinition predicate:
                json["thresholds"] = new JsonObject { ["upper"] = predicate.UpperThreshold, ["lower"] = predicate.LowerThreshold };
                break;
            case ChoiceDefinition choice:
                json["options"] = new JsonArray([.. choice.Options.Select(o => new JsonObject { ["value"] = o.Value, ["description"] = o.Description })]);
                json["thresholds"] = new JsonObject { ["minConfidence"] = choice.MinConfidence };
                break;
            case ScoreDefinition score:
                json["levels"] = new JsonArray([.. score.Levels.Select(l => new JsonObject { ["label"] = l.Label, ["description"] = l.Description })]);
                json["thresholds"] = new JsonObject { ["minConfidence"] = score.MinConfidence, ["cutOff"] = score.CutOff };
                break;
        }

        var bytes = Encoding.UTF8.GetBytes(CanonicalJson.SerializeCompact(json));
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}
