using Augur.Core.Catalog;
using Augur.Core.Intake;

namespace Augur.Core.Decisions;

/// <summary>
/// Walks the catalog one dependency level at a time. Overrides resolve first; decisions whose when clause is
/// false are skipped; the remaining decisions of a level go to the oracle in one batch.
/// </summary>
public sealed class TreeEvaluator(DecisionCatalog catalog, IDecisionOracle oracle, TimeProvider time)
{
    /// <exception cref="UsageException">An override names a decision that does not apply.</exception>
    /// <exception cref="UnresolvedDecisionsException">A decision could not be resolved.</exception>
    public async Task<DecisionState> EvaluateAsync(SpecificationInput input, OverrideSet overrides, CancellationToken cancellationToken)
    {
        CheckOverridesApply(overrides);

        var state = new DecisionState();
        foreach (var (id, value) in overrides.Values)
        {
            var definition = catalog.Get(id);
            state.Store(new ResolvedDecision(id, value, DecisionSource.Override, Raw: null, definition.QuestionHash, time.GetUtcNow()));
        }

        foreach (var level in DependencyLeveller.Levels(catalog))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var decision in level)
            {
                if (decision.When is { } when && !Applies(when, state))
                {
                    if (overrides.Contains(decision.Id))
                    {
                        throw new UsageException(NotApplicableReason(decision, state));
                    }

                    state.MarkSkipped(decision.Id);
                }
            }

            var pending = level.Where(d => state.StatusOf(d.Id) == DecisionStatus.Pending).ToList();
            if (pending.Count > 0)
            {
                var answers = await oracle.AnswerAsync(input, [.. pending.Select(d => new DecisionRequest(d))], cancellationToken);
                ResolveLevel(pending, answers, state);
            }
        }

        return state;
    }

    private static bool Applies(WhenClause when, DecisionState state) =>
        state.ValueOf(when.DependsOn) is { } value && when.AcceptedValues.Contains(value);

    private static string NotApplicableReason(DecisionDefinition decision, DecisionState state)
    {
        var when = decision.When!;
        return state.ValueOf(when.DependsOn) is { } value
            ? $"{decision.Id} does not apply when {when.DependsOn} is {value}"
            : $"{decision.Id} does not apply because {when.DependsOn} does not apply";
    }

    /// <summary>
    /// Rejects, before anything is asked, an override whose decision is already known not to apply because of
    /// other overrides.
    /// </summary>
    private void CheckOverridesApply(OverrideSet overrides)
    {
        var notApplicable = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var decision in DependencyLeveller.Levels(catalog).SelectMany(level => level))
        {
            if (decision.When is not { } when)
            {
                continue;
            }

            if (notApplicable.TryGetValue(when.DependsOn, out var dependencyReason))
            {
                notApplicable[decision.Id] = $"{decision.Id} does not apply because {dependencyReason}";
            }
            else if (overrides.Values.TryGetValue(when.DependsOn, out var value) && !when.AcceptedValues.Contains(value))
            {
                notApplicable[decision.Id] = $"{decision.Id} does not apply when {when.DependsOn} is {value}";
            }
        }

        foreach (var id in overrides.Values.Keys)
        {
            if (notApplicable.TryGetValue(id, out var reason))
            {
                throw new UsageException(reason);
            }
        }
    }

    private static void ResolveLevel(IReadOnlyList<DecisionDefinition> pending, IReadOnlyList<DecisionAnswer> answers, DecisionState state) =>
        throw new NotSupportedException("answers from an oracle are not resolved yet");
}
