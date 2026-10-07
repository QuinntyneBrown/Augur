using System.Text.Json;
using System.Text.Json.Nodes;
using Augur.Core.Catalog;

namespace Augur.Core.Decisions;

/// <summary>
/// Reads and writes a raw result in the Decisions API answer format, which answer scripts and lockfile entries
/// share: <c>probability</c>; or <c>choice</c>, <c>confidence</c>, <c>probabilities</c>; or <c>score</c>,
/// <c>confidence</c>, <c>probabilities</c>; or <c>type: refusal</c>.
/// </summary>
public static class RawResultJson
{
    public const string Refusal = "refusal";

    /// <summary>Reads <paramref name="raw"/> as an answer to <paramref name="definition"/>.</summary>
    /// <exception cref="FormatException">The fields do not form a result of the decision's type; the message completes "entry 'x' ...".</exception>
    public static DecisionAnswer Read(JsonNode? raw, DecisionDefinition definition, DecisionSource source)
    {
        if (raw is not JsonObject obj)
        {
            throw new FormatException("is not a JSON object");
        }

        var expected = definition.Type.ToWireName();
        var type = obj["type"] is { } typeNode ? String(obj, "type") : expected;
        if (type == Refusal)
        {
            return new DecisionAnswer(definition.Id, source) { Refused = true };
        }

        if (type != expected)
        {
            throw new FormatException($"is not a {expected} result: type is '{type}'");
        }

        try
        {
            return definition.Type switch
            {
                DecisionType.Predicate => new DecisionAnswer(definition.Id, source, Predicate: new PredicateResult(Number(obj, "probability"))),
                DecisionType.Choice => new DecisionAnswer(
                    definition.Id,
                    source,
                    Choice: new ChoiceResult(String(obj, "choice"), Probabilities(obj, definition), Number(obj, "confidence"))),
                DecisionType.Score => new DecisionAnswer(
                    definition.Id,
                    source,
                    Score: new ScoreResult(Number(obj, "score"), Probabilities(obj, definition), Number(obj, "confidence"))),
                _ => throw new FormatException($"has unknown type '{type}'"),
            };
        }
        catch (FormatException ex)
        {
            throw new FormatException($"is not a {expected} result: {ex.Message}", ex);
        }
    }

    /// <summary>Writes the raw result fields of <paramref name="answer"/> into <paramref name="target"/>.</summary>
    public static void Write(DecisionAnswer answer, DecisionDefinition? definition, JsonObject target)
    {
        if (answer.Refused)
        {
            target["type"] = Refusal;
            return;
        }

        if (answer.Predicate is { } predicate)
        {
            target["type"] = "predicate";
            target["probability"] = predicate.Probability;
        }
        else if (answer.Choice is { } choice)
        {
            target["type"] = "choice";
            target["choice"] = choice.Choice;
            target["confidence"] = choice.Confidence;
            target["probabilities"] = new JsonArray([.. choice.Probabilities.Select(p => new JsonObject
            {
                ["value"] = p.Label,
                ["probability"] = p.Probability,
            })]);
        }
        else if (answer.Score is { } score)
        {
            var levels = (definition as ScoreDefinition)?.Levels.Select(l => l.Label).ToList() ?? [];
            target["type"] = "score";
            target["score"] = score.Score;
            target["confidence"] = score.Confidence;
            target["probabilities"] = new JsonArray([.. score.Probabilities.Select(p => new JsonObject
            {
                ["value"] = levels.IndexOf(p.Label) is var index and >= 0 ? JsonValue.Create(index) : null,
                ["label"] = p.Label,
                ["probability"] = p.Probability,
            })]);
        }
    }

    private static List<LabelProbability> Probabilities(JsonObject obj, DecisionDefinition definition)
    {
        if (obj["probabilities"] is null)
        {
            return [];
        }

        if (obj["probabilities"] is not JsonArray array)
        {
            throw new FormatException("'probabilities' is not an array");
        }

        var levels = (definition as ScoreDefinition)?.Levels;
        return [.. array.Select(item =>
        {
            if (item is not JsonObject entry)
            {
                throw new FormatException("'probabilities' holds an entry that is not an object");
            }

            var label = levels is null ? String(entry, "value") : ScoreLabel(entry, levels);
            return new LabelProbability(label, Number(entry, "probability"));
        })];
    }

    private static string ScoreLabel(JsonObject entry, IReadOnlyList<ScoreLevel> levels)
    {
        if (entry["label"] is not null)
        {
            return String(entry, "label");
        }

        var index = (int)Number(entry, "value");
        return index >= 0 && index < levels.Count ? levels[index].Label : index.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static double Number(JsonObject obj, string name) => obj[name] switch
    {
        null => throw new FormatException($"'{name}' is missing"),
        JsonValue value when value.GetValueKind() == JsonValueKind.Number => value.GetValue<double>(),
        _ => throw new FormatException($"'{name}' is not a number"),
    };

    private static string String(JsonObject obj, string name) => obj[name] switch
    {
        null => throw new FormatException($"'{name}' is missing"),
        JsonValue value when value.GetValueKind() == JsonValueKind.String => value.GetValue<string>(),
        _ => throw new FormatException($"'{name}' is not a string"),
    };
}
