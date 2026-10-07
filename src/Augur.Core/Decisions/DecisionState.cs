namespace Augur.Core.Decisions;

public enum DecisionStatus
{
    Pending,
    Skipped,
    Resolved,
}

/// <summary>The state of every decision during one evaluation of the tree.</summary>
public sealed class DecisionState
{
    private readonly Dictionary<string, ResolvedDecision> _resolved = new(StringComparer.Ordinal);
    private readonly HashSet<string> _skipped = new(StringComparer.Ordinal);

    public IReadOnlyCollection<ResolvedDecision> Resolved => _resolved.Values;

    public IReadOnlyCollection<string> Skipped => _skipped;

    public DecisionStatus StatusOf(string id) =>
        _resolved.ContainsKey(id) ? DecisionStatus.Resolved
        : _skipped.Contains(id) ? DecisionStatus.Skipped
        : DecisionStatus.Pending;

    public ResolvedDecision? Get(string id) => _resolved.GetValueOrDefault(id);

    public string? ValueOf(string id) => _resolved.GetValueOrDefault(id)?.Value;

    public void Store(ResolvedDecision decision) => _resolved[decision.Id] = decision;

    public void MarkSkipped(string id) => _skipped.Add(id);
}
