using System.Globalization;
using Augur.Core.Catalog;

namespace Augur.Core.Decisions;

/// <summary>An answer that is outside its decision's closed answer set or valid ranges. It is never used.</summary>
public sealed class InvalidAnswerException(string decisionId, DecisionSource source, string problem)
    : AugurException(
        $"invalid answer for {decisionId} from {Describe(source)}: {problem}",
        source == DecisionSource.Api ? ExitCode.ApiFailure : ExitCode.InvalidUsage)
{
    public string DecisionId { get; } = decisionId;

    public DecisionSource AnswerSource { get; } = source;

    public static string Describe(DecisionSource source) => source switch
    {
        DecisionSource.Api => "the Decisions API",
        DecisionSource.Script => "the answer script",
        DecisionSource.Lockfile => "the lockfile",
        DecisionSource.User => "the prompt",
        _ => source.ToWireName(),
    };
}

/// <summary>Per-run settings for resolving answers.</summary>
public sealed record ResolverOptions(double? MinConfidenceOverride = null);

/// <summary>The outcome of applying a decision's thresholds to an answer.</summary>
public abstract record Resolution;

public sealed record Resolved(string Value) : Resolution;

/// <summary>An answer that did not clear its decision's thresholds and must go to the low-confidence policy.</summary>
public sealed record LowConfidence(DecisionDefinition Definition, DecisionAnswer Answer, ResolverOptions Options) : Resolution
{
    /// <summary>What the model said, for messages: "vertical-slice with confidence 0.50 (minimum 0.70)".</summary>
    public string Describe(bool withThresholds = true)
    {
        var description = Answer switch
        {
            { Refused: true } => "refused to answer",
            { Predicate: { } p } => Format($"probability {p.Probability:0.00}"),
            { Choice: { } c } => Format($"{c.Choice} with confidence {c.Confidence:0.00}"),
            { Score: { } s } => Format($"score {s.Score:0.00} with confidence {s.Confidence:0.00}"),
            _ => "no answer",
        };
        if (!withThresholds || Answer.Refused)
        {
            return description;
        }

        return Definition switch
        {
            PredicateDefinition p => description + Format($" (between {p.LowerThreshold:0.00} and {p.UpperThreshold:0.00})"),
            ChoiceDefinition c => description + Format($" (minimum {Options.MinConfidenceOverride ?? c.MinConfidence:0.00})"),
            ScoreDefinition s => description + Format($" (minimum {Options.MinConfidenceOverride ?? s.MinConfidence:0.00})"),
            _ => description,
        };
    }

    private static string Format(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Checks an answer against its decision's closed set, then applies the decision's thresholds.</summary>
public sealed class DecisionResolver(ResolverOptions options)
{
    /// <exception cref="InvalidAnswerException">The answer is malformed or outside the decision's answer set.</exception>
    public Resolution Resolve(DecisionDefinition definition, DecisionAnswer answer)
    {
        if (answer.Replayed is { } replayed)
        {
            if (!definition.AcceptedValues.Contains(replayed.Value))
            {
                throw new InvalidAnswerException(definition.Id, answer.Source, $"'{replayed.Value}' is not one of {string.Join(", ", definition.AcceptedValues)}");
            }

            return new Resolved(replayed.Value);
        }

        Validate(definition, answer);
        if (answer.Refused)
        {
            return new LowConfidence(definition, answer, options);
        }

        return definition switch
        {
            PredicateDefinition p => answer.Predicate!.Probability >= p.UpperThreshold ? new Resolved(DecisionDefinition.True)
                : answer.Predicate.Probability <= p.LowerThreshold ? new Resolved(DecisionDefinition.False)
                : new LowConfidence(definition, answer, options),
            ChoiceDefinition c => answer.Choice!.Confidence >= (options.MinConfidenceOverride ?? c.MinConfidence) && answer.Choice.Choice != DecisionDefinition.Other
                ? new Resolved(answer.Choice.Choice)
                : new LowConfidence(definition, answer, options),
            ScoreDefinition s => answer.Score!.Confidence >= (options.MinConfidenceOverride ?? s.MinConfidence)
                ? new Resolved(answer.Score.Score >= s.CutOff ? DecisionDefinition.True : DecisionDefinition.False)
                : new LowConfidence(definition, answer, options),
            _ => throw new InvalidOperationException($"unknown decision type for {definition.Id}"),
        };
    }

    private static void Validate(DecisionDefinition definition, DecisionAnswer answer)
    {
        void Fail(string problem) => throw new InvalidAnswerException(definition.Id, answer.Source, problem);

        void RequireProbability(string name, double value)
        {
            if (value is < 0 or > 1 || double.IsNaN(value))
            {
                Fail(string.Create(CultureInfo.InvariantCulture, $"{name} {value} is outside 0 to 1"));
            }
        }

        if (answer.Refused)
        {
            return;
        }

        switch (definition)
        {
            case PredicateDefinition:
                if (answer.Predicate is not { } predicate)
                {
                    Fail("expected a predicate result");
                    return;
                }

                RequireProbability("probability", predicate.Probability);
                break;
            case ChoiceDefinition:
                if (answer.Choice is not { } choice)
                {
                    Fail("expected a choice result");
                    return;
                }

                RequireProbability("confidence", choice.Confidence);
                if (!definition.Answers.Contains(choice.Choice))
                {
                    Fail($"'{choice.Choice}' is not one of {string.Join(", ", definition.Answers)}");
                }

                foreach (var probability in choice.Probabilities)
                {
                    RequireProbability("probability", probability.Probability);
                    if (!definition.Answers.Contains(probability.Label))
                    {
                        Fail($"probabilities name '{probability.Label}', which is not one of {string.Join(", ", definition.Answers)}");
                    }
                }

                break;
            case ScoreDefinition scoreDefinition:
                if (answer.Score is not { } score)
                {
                    Fail("expected a score result");
                    return;
                }

                RequireProbability("confidence", score.Confidence);
                var max = scoreDefinition.Levels.Count - 1;
                if (score.Score < 0 || score.Score > max || double.IsNaN(score.Score))
                {
                    Fail(string.Create(CultureInfo.InvariantCulture, $"score {score.Score} is outside 0 to {max}"));
                }

                foreach (var probability in score.Probabilities)
                {
                    RequireProbability("probability", probability.Probability);
                    if (!scoreDefinition.Levels.Any(l => l.Label == probability.Label))
                    {
                        Fail($"probabilities name '{probability.Label}', which is not a level of {definition.Id}");
                    }
                }

                break;
        }
    }
}
