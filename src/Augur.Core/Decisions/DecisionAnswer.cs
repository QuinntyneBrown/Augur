using Augur.Core.Catalog;

namespace Augur.Core.Decisions;

/// <summary>Where a decision's value came from.</summary>
public enum DecisionSource
{
    Api,
    Lockfile,
    Override,
    Fallback,
    User,
    Script,
}

public static class DecisionSourceExtensions
{
    public static string ToWireName(this DecisionSource source) => source switch
    {
        DecisionSource.Api => "api",
        DecisionSource.Lockfile => "lockfile",
        DecisionSource.Override => "override",
        DecisionSource.Fallback => "fallback",
        DecisionSource.User => "user",
        DecisionSource.Script => "script",
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, null),
    };

    public static bool TryParse(string? wireName, out DecisionSource source)
    {
        foreach (var candidate in Enum.GetValues<DecisionSource>())
        {
            if (candidate.ToWireName() == wireName)
            {
                source = candidate;
                return true;
            }
        }

        source = default;
        return false;
    }
}

/// <summary>One question for an oracle.</summary>
public sealed record DecisionRequest(DecisionDefinition Definition);

/// <summary>
/// The raw result an oracle returned for one decision. Exactly one of <see cref="Predicate"/>, <see cref="Choice"/>,
/// or <see cref="Score"/> is set. A replayed answer also carries the value and metadata recorded in the lockfile.
/// </summary>
public sealed record DecisionAnswer(
    string DecisionId,
    DecisionSource Source,
    PredicateResult? Predicate = null,
    ChoiceResult? Choice = null,
    ScoreResult? Score = null)
{
    /// <summary>The answer's model, when it came from the Decisions API.</summary>
    public string? Model { get; init; }

    /// <summary>For a lockfile replay: the value, source, and time recorded when the decision was first resolved.</summary>
    public ReplayedResolution? Replayed { get; init; }
}

public sealed record PredicateResult(double Probability);

public sealed record ChoiceResult(string Choice, IReadOnlyList<LabelProbability> Probabilities, double Confidence);

public sealed record ScoreResult(double Score, IReadOnlyList<LabelProbability> Probabilities, double Confidence);

public sealed record LabelProbability(string Label, double Probability);

public sealed record ReplayedResolution(string Value, DecisionSource OriginalSource, DateTimeOffset ResolvedAt);

/// <summary>A decision's final value and how it was reached.</summary>
public sealed record ResolvedDecision(
    string Id,
    string Value,
    DecisionSource Source,
    DecisionAnswer? Raw,
    string QuestionHash,
    DateTimeOffset ResolvedAt);

/// <summary>Answers a batch of independent questions about one specification.</summary>
public interface IDecisionOracle
{
    /// <summary>Returns exactly one answer per request.</summary>
    Task<IReadOnlyList<DecisionAnswer>> AnswerAsync(
        Intake.SpecificationInput input,
        IReadOnlyList<DecisionRequest> requests,
        CancellationToken cancellationToken);
}
