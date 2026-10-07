using Augur.Core.Catalog;

namespace Augur.Core.Decisions;

/// <summary>Decisions the user fixed with <c>--set</c>, keyed by decision id, in the order given.</summary>
public sealed class OverrideSet
{
    private readonly Dictionary<string, string> _values;

    private OverrideSet(Dictionary<string, string> values) => _values = values;

    public static OverrideSet Empty { get; } = new(new Dictionary<string, string>(StringComparer.Ordinal));

    public IReadOnlyDictionary<string, string> Values => _values;

    public bool Contains(string id) => _values.ContainsKey(id);

    /// <summary>Parses <c>id=value</c> pairs and checks each against the catalog.</summary>
    /// <exception cref="UsageException">A pair is malformed, names an unknown decision, or uses a value outside its answer set.</exception>
    public static OverrideSet Parse(IEnumerable<string> pairs, DecisionCatalog catalog)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in pairs)
        {
            var separator = pair.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0)
            {
                throw new UsageException($"--set expects <id>=<value>, got '{pair}'");
            }

            var id = pair[..separator];
            var value = pair[(separator + 1)..];
            if (!catalog.TryGet(id, out var decision))
            {
                throw new UsageException($"'{id}' is not a catalog decision; valid ids: {string.Join(", ", catalog.Ids)}");
            }

            CheckValue(decision, value);
            if (!values.TryAdd(id, value))
            {
                throw new UsageException($"{id} is set more than once");
            }
        }

        return new OverrideSet(values);
    }

    /// <summary>Checks that <paramref name="value"/> is one the user may choose for <paramref name="decision"/>.</summary>
    /// <exception cref="UsageException">The value is <c>other</c> or outside the answer set.</exception>
    public static void CheckValue(DecisionDefinition decision, string value)
    {
        if (value == DecisionDefinition.Other && decision.Answers.Contains(DecisionDefinition.Other))
        {
            throw new UsageException($"'other' cannot be set explicitly for {decision.Id}");
        }

        if (!decision.AcceptedValues.Contains(value))
        {
            throw new UsageException(
                $"'{value}' is not an allowed value for {decision.Id}; allowed values: {string.Join(", ", decision.AcceptedValues)}");
        }
    }
}
