using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Augur.Core.Catalog;
using Augur.Core.Decisions;

namespace Augur.Core.Locking;

/// <summary>Renders how each recorded decision was reached, for <c>augur explain</c>.</summary>
public static class Explanation
{
    private const int LabelWidth = 12;
    private const string StaleNote = "the question has changed since this was recorded; it will be asked again";

    public static string RenderText(Lockfile lockfile, DecisionCatalog catalog)
    {
        var text = new StringBuilder();
        foreach (var entry in Ordered(lockfile))
        {
            var definition = catalog.Get(entry.Id);
            if (text.Length > 0)
            {
                text.Append('\n');
            }

            text.Append(CultureInfo.InvariantCulture, $"{entry.Id} = {entry.Value} ({entry.Source.ToWireName()})\n");
            Row(text, "result", ResultText(entry.Raw));
            var distribution = Distribution(entry.Raw);
            if (distribution.Count > 0)
            {
                text.Append("  distribution\n");
                var width = distribution.Max(d => d.Label.Length);
                foreach (var (label, probability) in distribution)
                {
                    text.Append(CultureInfo.InvariantCulture, $"    {label.PadRight(width)}  {probability:0.00}\n");
                }
            }

            Row(text, "thresholds", ThresholdText(entry.Thresholds ?? Thresholds.Of(definition, new ResolverOptions())));
            if (entry.Model is { } model)
            {
                Row(text, "model", model);
            }

            if (IsStale(entry, definition))
            {
                Row(text, "stale", StaleNote);
            }
        }

        return text.ToString();
    }

    public static string RenderJson(Lockfile lockfile, DecisionCatalog catalog) =>
        CanonicalJson.Serialize(new JsonArray([.. Ordered(lockfile).Select(entry =>
        {
            var definition = catalog.Get(entry.Id);
            return new JsonObject
            {
                ["id"] = entry.Id,
                ["value"] = DecisionValues.ToJson(definition, entry.Value),
                ["source"] = entry.Source.ToWireName(),
                ["result"] = ResultJson(entry.Raw),
                ["thresholds"] = (entry.Thresholds ?? Thresholds.Of(definition, new ResolverOptions())).ToJson(),
                ["resolvedAt"] = entry.ResolvedAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                ["model"] = entry.Model,
                ["stale"] = IsStale(entry, definition),
            };
        })]));

    private static IEnumerable<LockfileEntry> Ordered(Lockfile lockfile) => lockfile.Entries.OrderBy(e => e.Id, StringComparer.Ordinal);

    private static bool IsStale(LockfileEntry entry, DecisionDefinition definition) => entry.QuestionHash != definition.QuestionHash;

    private static string ResultText(DecisionAnswer raw) => raw switch
    {
        { Refused: true } => "refused to answer",
        { Predicate: { } p } => Format($"probability {p.Probability:0.00}"),
        { Choice: { } c } => Format($"choice {c.Choice}, confidence {c.Confidence:0.00}"),
        { Score: { } s } => Format($"score {s.Score:0.00}, confidence {s.Confidence:0.00}"),
        _ => "none",
    };

    private static JsonObject ResultJson(DecisionAnswer raw)
    {
        var json = new JsonObject();
        switch (raw)
        {
            case { Refused: true }:
                json["type"] = RawResultJson.Refusal;
                break;
            case { Predicate: { } p }:
                json["type"] = "predicate";
                json["probability"] = p.Probability;
                break;
            case { Choice: { } c }:
                json["type"] = "choice";
                json["choice"] = c.Choice;
                json["confidence"] = c.Confidence;
                break;
            case { Score: { } s }:
                json["type"] = "score";
                json["score"] = s.Score;
                json["confidence"] = s.Confidence;
                break;
        }

        var distribution = Distribution(raw);
        if (distribution.Count > 0)
        {
            json["distribution"] = new JsonArray([.. distribution.Select(d => new JsonObject { ["label"] = d.Label, ["probability"] = d.Probability })]);
        }

        return json;
    }

    /// <summary>The returned probabilities, most probable first; ties keep their recorded order.</summary>
    private static List<(string Label, double Probability)> Distribution(DecisionAnswer raw)
    {
        var probabilities = raw.Choice?.Probabilities ?? raw.Score?.Probabilities ?? [];
        return [.. probabilities.Select(p => (p.Label, p.Probability)).OrderByDescending(p => p.Probability)];
    }

    private static string ThresholdText(Thresholds thresholds)
    {
        var parts = new List<string>();
        if (thresholds is { Upper: { } upper, Lower: { } lower })
        {
            parts.Add(Format($"true at {upper:0.00} or more, false at {lower:0.00} or less"));
        }

        if (thresholds.MinConfidence is { } minConfidence)
        {
            parts.Add(Format($"minimum confidence {minConfidence:0.00}"));
        }

        if (thresholds.CutOff is { } cutOff)
        {
            parts.Add(Format($"true at a score of {cutOff} or more"));
        }

        return string.Join(", ", parts);
    }

    private static void Row(StringBuilder text, string label, string value) =>
        text.Append("  ").Append(label.PadRight(LabelWidth)).Append(value).Append('\n');

    private static string Format(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
