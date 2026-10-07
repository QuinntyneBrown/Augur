using System.Globalization;
using System.Text;
using Augur.Core;
using Augur.Core.Catalog;
using Augur.Core.Decisions;

namespace Augur.Cli;

/// <summary>Asks the user to settle each low-confidence decision on the console.</summary>
internal sealed class InteractivePromptHandler(TextReader input, TextWriter stderr) : ILowConfidenceHandler
{
    public const int MaxAttempts = 3;

    public async Task<IReadOnlyList<HandledDecision>> HandleAsync(IReadOnlyList<LowConfidence> decisions, CancellationToken cancellationToken)
    {
        var handled = new List<HandledDecision>();
        foreach (var decision in decisions)
        {
            handled.Add(new HandledDecision(decision.Definition.Id, await AskAsync(decision, cancellationToken), DecisionSource.User));
        }

        return handled;
    }

    private async Task<string> AskAsync(LowConfidence decision, CancellationToken cancellationToken)
    {
        var definition = decision.Definition;
        var rows = PromptView.Rows(decision);
        stderr.Write(PromptView.Render(decision, rows));
        for (var attempt = 1; ; attempt++)
        {
            stderr.Write($"Enter a number or a value [default: {definition.Default}]: ");
            var line = await input.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                stderr.WriteLine();
                var ids = new[] { definition.Id };
                throw new UnresolvedDecisionsException($"no answer for {definition.Id}: stdin is closed", ids);
            }

            var entry = line.Trim();
            if (entry.Length == 0)
            {
                return definition.Default;
            }

            if (int.TryParse(entry, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number >= 1 && number <= rows.Count)
            {
                return rows[number - 1].Value;
            }

            if (rows.Any(r => r.Value == entry))
            {
                return entry;
            }

            stderr.WriteLine($"error: '{entry}' is not one of the listed answers");
            if (attempt == MaxAttempts)
            {
                throw new UnresolvedDecisionsException($"no valid answer for {definition.Id} after {MaxAttempts} attempts", [definition.Id]);
            }
        }
    }
}

/// <summary>Lays out the answers of a low-confidence decision, most probable first, without <c>other</c>.</summary>
internal static class PromptView
{
    public static IReadOnlyList<PromptRow> Rows(LowConfidence decision)
    {
        var definition = decision.Definition;
        var answer = decision.Answer;
        var probabilities = (definition, answer) switch
        {
            (PredicateDefinition, { Predicate: { } p }) => new Dictionary<string, double>
            {
                [DecisionDefinition.True] = p.Probability,
                [DecisionDefinition.False] = 1 - p.Probability,
            },
            (ChoiceDefinition, { Choice: { } c }) => c.Probabilities.ToDictionary(p => p.Label, p => p.Probability),
            (ScoreDefinition score, { Score: { } s }) => ScoreProbabilities(score, s),
            _ => [],
        };

        return [.. definition.AcceptedValues
            .Select((value, order) => (Value: value, Order: order, Probability: probabilities.GetValueOrDefault(value)))
            .OrderByDescending(r => r.Probability)
            .ThenBy(r => r.Order)
            .Select(r => new PromptRow(r.Value, r.Probability))];
    }

    public static string Render(LowConfidence decision, IReadOnlyList<PromptRow> rows)
    {
        var width = rows.Max(r => r.Value.Length);
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"{decision.Definition.Id} is low-confidence: {decision.Describe()}\n");
        text.Append(decision.Definition.Instructions).Append('\n');
        for (var i = 0; i < rows.Count; i++)
        {
            text.Append(CultureInfo.InvariantCulture, $"  {i + 1}) {rows[i].Value.PadRight(width)}  {rows[i].Probability:0.00}\n");
        }

        return text.ToString();
    }

    /// <summary>A score resolves to true at or above the cut-off, so true's probability is that of the levels from the cut-off up.</summary>
    private static Dictionary<string, double> ScoreProbabilities(ScoreDefinition definition, ScoreResult result)
    {
        var levels = definition.Levels.Select(l => l.Label).ToList();
        var atOrAboveCutOff = result.Probabilities.Where(p => levels.IndexOf(p.Label) >= definition.CutOff).Sum(p => p.Probability);
        var below = result.Probabilities.Where(p => levels.IndexOf(p.Label) is var i && i >= 0 && i < definition.CutOff).Sum(p => p.Probability);
        return new() { [DecisionDefinition.True] = atOrAboveCutOff, [DecisionDefinition.False] = below };
    }
}

internal sealed record PromptRow(string Value, double Probability);
