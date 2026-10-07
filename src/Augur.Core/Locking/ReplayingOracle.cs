using Augur.Core.Catalog;
using Augur.Core.Decisions;
using Augur.Core.Intake;

namespace Augur.Core.Locking;

/// <summary>Decision ids whose recorded answers <c>--refresh</c> tells augur to ignore.</summary>
public sealed class RefreshSet
{
    private RefreshSet(bool all, IReadOnlySet<string> ids)
    {
        All = all;
        Ids = ids;
    }

    public static RefreshSet None { get; } = new(false, new HashSet<string>());

    public bool All { get; }

    public IReadOnlySet<string> Ids { get; }

    public bool Matches(string id) => All || Ids.Contains(id);

    /// <summary><c>--refresh</c> alone refreshes everything; <c>--refresh id</c> refreshes only the named decisions.</summary>
    /// <exception cref="UsageException">An id is not a catalog decision.</exception>
    public static RefreshSet Parse(bool given, IReadOnlyList<string> ids, DecisionCatalog catalog)
    {
        if (!given)
        {
            return None;
        }

        foreach (var id in ids)
        {
            if (!catalog.TryGet(id, out _))
            {
                throw new UsageException($"'{id}' is not a catalog decision; valid ids: {string.Join(", ", catalog.Ids)}");
            }
        }

        return new RefreshSet(ids.Count == 0, ids.ToHashSet(StringComparer.Ordinal));
    }
}

/// <summary>
/// Serves answers recorded in the lockfile when both the input and the question are unchanged, and forwards
/// every other question to the inner oracle in one batch.
/// </summary>
public sealed class ReplayingOracle(Lockfile? previous, RefreshSet refresh, IDecisionOracle inner) : IDecisionOracle, IApiRequestCounter
{
    public int ApiRequests => (inner as IApiRequestCounter)?.ApiRequests ?? 0;

    public async Task<IReadOnlyList<DecisionAnswer>> AnswerAsync(
        SpecificationInput input,
        IReadOnlyList<DecisionRequest> requests,
        CancellationToken cancellationToken)
    {
        var answers = new Dictionary<string, DecisionAnswer>(StringComparer.Ordinal);
        var forward = new List<DecisionRequest>();
        foreach (var request in requests)
        {
            if (Reusable(request, input) is { } entry)
            {
                answers[request.Definition.Id] = entry.Raw with
                {
                    Source = DecisionSource.Lockfile,
                    Replayed = new ReplayedResolution(entry.Value, entry.Source, entry.ResolvedAt),
                };
            }
            else
            {
                forward.Add(request);
            }
        }

        if (forward.Count > 0)
        {
            foreach (var answer in await inner.AnswerAsync(input, forward, cancellationToken))
            {
                answers[answer.DecisionId] = answer;
            }
        }

        return [.. requests.Select(r => answers[r.Definition.Id])];
    }

    private LockfileEntry? Reusable(DecisionRequest request, SpecificationInput input)
    {
        if (previous is null || previous.InputHash != input.InputHash || refresh.Matches(request.Definition.Id))
        {
            return null;
        }

        return previous.Find(request.Definition.Id) is { } entry && entry.QuestionHash == request.Definition.QuestionHash
            ? entry
            : null;
    }
}

/// <summary>The inner oracle under <c>--offline</c>: anything not recorded in the lockfile stays unresolved.</summary>
public sealed class OfflineOracle : IDecisionOracle
{
    public Task<IReadOnlyList<DecisionAnswer>> AnswerAsync(
        SpecificationInput input,
        IReadOnlyList<DecisionRequest> requests,
        CancellationToken cancellationToken)
    {
        var ids = requests.Select(r => r.Definition.Id).ToList();
        throw new UnresolvedDecisionsException($"offline: no recorded decision for: {string.Join(", ", ids)}", ids);
    }
}
