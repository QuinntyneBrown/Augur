namespace Augur.IntegrationTests.Support;

/// <summary>
/// Picks a small set of plans in which every pair of values from two different decisions appears together at least
/// once. Greedy and deterministic: it repeatedly takes the candidate that covers the most uncovered pairs.
/// </summary>
public static class Pairwise
{
    public static IReadOnlyList<Dictionary<string, object>> Cover(IReadOnlyList<Dictionary<string, object>> candidates)
    {
        var uncovered = candidates.SelectMany(Pairs).ToHashSet();
        var chosen = new List<Dictionary<string, object>>();
        while (uncovered.Count > 0)
        {
            var best = candidates.MaxBy(c => Pairs(c).Count(uncovered.Contains))!;
            chosen.Add(best);
            uncovered.ExceptWith(Pairs(best));
        }

        return chosen;
    }

    /// <summary>Every pair of (decision, value) settings in a plan, in a canonical order.</summary>
    public static IEnumerable<string> Pairs(Dictionary<string, object> plan)
    {
        var settings = plan.Select(d => $"{d.Key}={d.Value}").Order(StringComparer.Ordinal).ToList();
        for (var i = 0; i < settings.Count; i++)
        {
            for (var j = i + 1; j < settings.Count; j++)
            {
                yield return $"{settings[i]}|{settings[j]}";
            }
        }
    }
}
