namespace Augur.Core.Decisions;

/// <summary>How decisions that did not clear their thresholds are resolved.</summary>
public enum LowConfidencePolicy
{
    Default,
    Prompt,
    Fail,
}

/// <summary>A value chosen for a low-confidence decision by a policy.</summary>
public sealed record HandledDecision(string Id, string Value, DecisionSource Source);

/// <summary>Resolves all the low-confidence decisions of one dependency level at once.</summary>
public interface ILowConfidenceHandler
{
    /// <exception cref="UnresolvedDecisionsException">The policy leaves decisions unresolved.</exception>
    Task<IReadOnlyList<HandledDecision>> HandleAsync(IReadOnlyList<LowConfidence> decisions, CancellationToken cancellationToken);
}

public static class LowConfidencePolicySelector
{
    /// <summary>The explicit policy, or prompt when both stdin and stderr are terminals, or fail otherwise.</summary>
    public static LowConfidencePolicy Select(LowConfidencePolicy? option, bool stdinIsTerminal, bool stderrIsTerminal) =>
        option ?? (stdinIsTerminal && stderrIsTerminal ? LowConfidencePolicy.Prompt : LowConfidencePolicy.Fail);
}

/// <summary>Uses each decision's catalog default and warns that it did.</summary>
public sealed class DefaultFallbackHandler(IReporter reporter) : ILowConfidenceHandler
{
    public Task<IReadOnlyList<HandledDecision>> HandleAsync(IReadOnlyList<LowConfidence> decisions, CancellationToken cancellationToken)
    {
        foreach (var decision in decisions)
        {
            reporter.Warn($"{decision.Definition.Id} is low-confidence ({decision.Describe(withThresholds: false)}); using the default {decision.Definition.Default}");
        }

        return Task.FromResult<IReadOnlyList<HandledDecision>>(
            [.. decisions.Select(d => new HandledDecision(d.Definition.Id, d.Definition.Default, DecisionSource.Fallback))]);
    }
}

/// <summary>Lists every low-confidence decision of the level and stops the run.</summary>
public sealed class FailHandler(IReporter reporter) : ILowConfidenceHandler
{
    public Task<IReadOnlyList<HandledDecision>> HandleAsync(IReadOnlyList<LowConfidence> decisions, CancellationToken cancellationToken)
    {
        foreach (var decision in decisions)
        {
            reporter.Warn($"low confidence: {decision.Definition.Id}: {decision.Describe()}");
        }

        var ids = decisions.Select(d => d.Definition.Id).ToList();
        throw new UnresolvedDecisionsException($"low-confidence decisions: {string.Join(", ", ids)}", ids);
    }
}
