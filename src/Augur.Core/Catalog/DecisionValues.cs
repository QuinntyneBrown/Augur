using System.Text.Json;
using System.Text.Json.Nodes;

namespace Augur.Core.Catalog;

/// <summary>
/// Converts decision values between their in-memory form (always a string) and their file form:
/// booleans for predicate and score decisions, strings for choice decisions.
/// </summary>
public static class DecisionValues
{
    public static JsonNode ToJson(DecisionDefinition decision, string value) =>
        decision.IsBoolean ? JsonValue.Create(value == DecisionDefinition.True) : JsonValue.Create(value);

    /// <summary>Reads a value from a file; returns <c>null</c> when its JSON type does not fit the decision.</summary>
    public static string? FromJson(DecisionDefinition decision, JsonNode? node)
    {
        if (node is not JsonValue value)
        {
            return null;
        }

        return (decision.IsBoolean, value.GetValueKind()) switch
        {
            (true, JsonValueKind.True) => DecisionDefinition.True,
            (true, JsonValueKind.False) => DecisionDefinition.False,
            (false, JsonValueKind.String) => value.GetValue<string>(),
            _ => null,
        };
    }
}
