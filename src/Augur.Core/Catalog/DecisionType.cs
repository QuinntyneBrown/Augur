namespace Augur.Core.Catalog;

/// <summary>The three Decisions API question types.</summary>
public enum DecisionType
{
    Predicate,
    Choice,
    Score,
}

public static class DecisionTypeExtensions
{
    /// <summary>The lower-case name used on the wire, in the catalog listing, and in files.</summary>
    public static string ToWireName(this DecisionType type) => type switch
    {
        DecisionType.Predicate => "predicate",
        DecisionType.Choice => "choice",
        DecisionType.Score => "score",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };
}
