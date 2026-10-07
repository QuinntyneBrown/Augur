using Augur.Core.Catalog;
using Augur.Core.Intake;

namespace Augur.Core.Decisions;

/// <summary>
/// Walks the catalog one dependency level at a time. Overrides resolve first; decisions whose when clause is
/// false are skipped; the remaining decisions of a level go to the oracle in one batch.
/// </summary>
public sealed class TreeEvaluator(
    DecisionCatalog catalog,
    IDecisionOracle oracle,
    DecisionResolver resolver,
    ILowConfidenceHandler lowConfidence,
    TimeProvider time)
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
                await ResolveLevelAsync(pending, answers, state, cancellationToken);
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

    private async Task ResolveLevelAsync(
        IReadOnlyList<DecisionDefinition> pending,
        IReadOnlyList<DecisionAnswer> answers,
        DecisionState state,
        CancellationToken cancellationToken)
    {
        var byId = answers.ToDictionary(a => a.DecisionId, StringComparer.Ordinal);
        var lowConfidenceDecisions = new List<LowConfidence>();
        foreach (var definition in pending)
        {
            if (!byId.TryGetValue(definition.Id, out var answer))
            {
                throw new InvalidOperationException($"the oracle returned no answer for {definition.Id}");
            }

            switch (resolver.Resolve(definition, answer))
            {
                case Resolved resolved:
                    state.Store(new ResolvedDecision(
                        definition.Id,
                        resolved.Value,
                        answer.Source,
                        answer,
                        definition.QuestionHash,
                        answer.Replayed?.ResolvedAt ?? time.GetUtcNow()));
                    break;
                case LowConfidence low:
                    lowConfidenceDecisions.Add(low);
                    break;
            }
        }

        if (lowConfidenceDecisions.Count == 0)
        {
            return;
        }

        var handled = await lowConfidence.HandleAsync(lowConfidenceDecisions, cancellationToken);
        foreach (var low in lowConfidenceDecisions)
        {
            var decision = handled.Single(h => h.Id == low.Definition.Id);
            if (!low.Definition.AcceptedValues.Contains(decision.Value))
            {
                throw new InvalidOperationException($"the low-confidence policy chose '{decision.Value}', which is not an answer for {decision.Id}");
            }

            state.Store(new ResolvedDecision(
                decision.Id,
                decision.Value,
                decision.Source,
                low.Answer,
                low.Definition.QuestionHash,
                time.GetUtcNow()));
        }
    }
}
