using Augur.Core.Intake;

namespace Augur.Core.Decisions;

/// <summary>An oracle with no source of answers: every question it receives is unresolved.</summary>
public sealed class NoOracle : IDecisionOracle
{
    public Task<IReadOnlyList<DecisionAnswer>> AnswerAsync(
        SpecificationInput input,
        IReadOnlyList<DecisionRequest> requests,
        CancellationToken cancellationToken)
    {
        var ids = requests.Select(r => r.Definition.Id).ToList();
        throw new UnresolvedDecisionsException($"unresolved decisions: {string.Join(", ", ids)}", ids);
    }
}
