using Augur.Core.Catalog;

namespace Augur.Core.Decisions;

/// <summary>
/// Groups decisions into levels: level 0 has no dependency, and a decision sits one level below the decision
/// its when clause names. Each level can be asked in one request.
/// </summary>
public static class DependencyLeveller
{
    public static IReadOnlyList<IReadOnlyList<DecisionDefinition>> Levels(DecisionCatalog catalog)
    {
        var depth = new Dictionary<string, int>(StringComparer.Ordinal);

        int DepthOf(DecisionDefinition decision)
        {
            if (!depth.TryGetValue(decision.Id, out var value))
            {
                value = decision.When is { } when ? DepthOf(catalog.Get(when.DependsOn)) + 1 : 0;
                depth[decision.Id] = value;
            }

            return value;
        }

        return [.. catalog.Decisions
            .GroupBy(DepthOf)
            .OrderBy(level => level.Key)
            .Select(level => (IReadOnlyList<DecisionDefinition>)[.. level])];
    }
}
