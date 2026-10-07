using System.Diagnostics.CodeAnalysis;

namespace Augur.Core.Catalog;

/// <summary>The versioned, validated set of decisions augur can make.</summary>
public sealed class DecisionCatalog
{
    private readonly Dictionary<string, DecisionDefinition> _byId;

    private DecisionCatalog(int version, IReadOnlyList<DecisionDefinition> decisions)
    {
        Version = version;
        Decisions = decisions;
        _byId = decisions.ToDictionary(d => d.Id, StringComparer.Ordinal);
    }

    /// <summary>The catalog augur ships with.</summary>
    public static DecisionCatalog BuiltIn { get; } = Create(CatalogVersion2.Version, CatalogVersion2.Build());

    public int Version { get; }

    /// <summary>Every decision, in catalog order.</summary>
    public IReadOnlyList<DecisionDefinition> Decisions { get; }

    public IEnumerable<string> Ids => Decisions.Select(d => d.Id);

    /// <summary>Validates <paramref name="decisions"/> and builds a catalog from them.</summary>
    /// <exception cref="InvalidCatalogException">The definitions are malformed.</exception>
    public static DecisionCatalog Create(int version, IReadOnlyList<DecisionDefinition> decisions)
    {
        CatalogValidator.Validate(decisions);
        return new DecisionCatalog(version, decisions);
    }

    public DecisionDefinition Get(string id) =>
        _byId.TryGetValue(id, out var definition)
            ? definition
            : throw new KeyNotFoundException($"'{id}' is not a catalog decision");

    public bool TryGet(string id, [NotNullWhen(true)] out DecisionDefinition? definition) =>
        _byId.TryGetValue(id, out definition);
}
