namespace Augur.Core.Catalog;

/// <summary>Rejects a catalog that could ask an unanswerable question or accept an answer outside its closed set.</summary>
public static class CatalogValidator
{
    /// <exception cref="InvalidCatalogException">The first problem found, naming the decision.</exception>
    public static void Validate(IReadOnlyList<DecisionDefinition> decisions)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var decision in decisions)
        {
            if (!ids.Add(decision.Id))
            {
                throw new InvalidCatalogException(decision.Id, "is defined more than once");
            }
        }

        foreach (var decision in decisions)
        {
            ValidateDefinition(decision);
            if (decision.When is { } when && !ids.Contains(when.DependsOn))
            {
                throw new InvalidCatalogException(decision.Id, $"depends on undefined decision '{when.DependsOn}'");
            }
        }

        ValidateNoCycles(decisions);
    }

    private static void ValidateDefinition(DecisionDefinition decision)
    {
        if (string.IsNullOrWhiteSpace(decision.Instructions))
        {
            throw new InvalidCatalogException(decision.Id, "has no instructions");
        }

        if (!decision.AcceptedValues.Contains(decision.Default))
        {
            throw new InvalidCatalogException(decision.Id, $"has default '{decision.Default}', which is not one of its accepted answers");
        }

        switch (decision)
        {
            case ChoiceDefinition choice:
                if (!choice.Answers.Contains(DecisionDefinition.Other))
                {
                    throw new InvalidCatalogException(decision.Id, "is a choice without the 'other' option");
                }

                if (choice.Answers.Distinct(StringComparer.Ordinal).Count() != choice.Answers.Count)
                {
                    throw new InvalidCatalogException(decision.Id, "lists an option more than once");
                }

                RequireDescriptions(decision.Id, choice.Options.Select(o => (o.Value, o.Description)));
                RequireProbability(decision.Id, "minimum confidence", choice.MinConfidence);
                break;
            case ScoreDefinition score:
                if (score.Levels.Count < 2)
                {
                    throw new InvalidCatalogException(decision.Id, "needs at least two levels");
                }

                RequireDescriptions(decision.Id, score.Levels.Select(l => (l.Label, l.Description)));
                RequireProbability(decision.Id, "minimum confidence", score.MinConfidence);
                if (score.CutOff < 0 || score.CutOff > score.Levels.Count - 1)
                {
                    throw new InvalidCatalogException(decision.Id, "has a cut-off outside its level range");
                }

                break;
            case PredicateDefinition predicate:
                RequireProbability(decision.Id, "upper threshold", predicate.UpperThreshold);
                RequireProbability(decision.Id, "lower threshold", predicate.LowerThreshold);
                if (predicate.LowerThreshold >= predicate.UpperThreshold)
                {
                    throw new InvalidCatalogException(decision.Id, "has a lower threshold that is not below its upper threshold");
                }

                break;
        }
    }

    private static void RequireDescriptions(string id, IEnumerable<(string Name, string Description)> answers)
    {
        foreach (var (name, description) in answers)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                throw new InvalidCatalogException(id, $"has no description for '{name}'");
            }
        }
    }

    private static void RequireProbability(string id, string name, double value)
    {
        if (value is < 0 or > 1 || double.IsNaN(value))
        {
            throw new InvalidCatalogException(id, $"has a {name} outside 0 to 1");
        }
    }

    private static void ValidateNoCycles(IReadOnlyList<DecisionDefinition> decisions)
    {
        var byId = decisions.ToDictionary(d => d.Id, StringComparer.Ordinal);
        foreach (var start in decisions)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal) { start.Id };
            var current = start;
            while (current.When is { } when && byId.TryGetValue(when.DependsOn, out var next))
            {
                if (!seen.Add(next.Id))
                {
                    throw new InvalidCatalogException(start.Id, "is part of a cycle of when clauses");
                }

                current = next;
            }
        }
    }
}
