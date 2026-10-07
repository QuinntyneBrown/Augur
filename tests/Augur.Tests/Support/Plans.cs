using System.Text.Json.Nodes;

namespace Augur.Tests.Support;

/// <summary>Builds valid plan files directly, and enumerates every valid combination of decisions for a target.</summary>
public static class Plans
{
    public const string InputHash = "0000000000000000000000000000000000000000000000000000000000000000";

    private static readonly string[] Architectures = ["clean-architecture", "vertical-slice", "minimal-api"];
    private static readonly string[] Persistence = ["ef-core-sqlserver", "ef-core-postgresql", "ef-core-sqlite", "none"];
    private static readonly bool[] Booleans = [false, true];

    /// <summary>A plan for <paramref name="name"/> with the given values; booleans may be passed as bool or "true"/"false".</summary>
    public static string Json(string name, IReadOnlyDictionary<string, object> decisions) => new JsonObject
    {
        ["planVersion"] = 1,
        ["catalogVersion"] = 2,
        ["solutionName"] = name,
        ["inputHash"] = InputHash,
        ["decisions"] = new JsonObject(decisions.Select(d => KeyValuePair.Create(d.Key, (JsonNode?)new JsonObject
        {
            ["value"] = d.Value switch
            {
                bool b => JsonValue.Create(b),
                "true" => JsonValue.Create(true),
                "false" => JsonValue.Create(false),
                _ => JsonValue.Create((string)d.Value),
            },
            ["source"] = "override",
        }))),
    }.ToJsonString();

    /// <summary>Every valid decision set for <c>target=dotnet</c>: 80 plans.</summary>
    public static IEnumerable<Dictionary<string, object>> AllDotNet() =>
        from authentication in Booleans
        from architecture in Architectures
        from cqrs in architecture == "minimal-api" ? new bool?[] { null } : [false, true]
        from persistence in Persistence
        from worker in Booleans
        select DotNet(architecture, persistence, authentication, worker, cqrs);

    /// <summary>Every valid decision set for <c>target=angular</c>: 16 plans.</summary>
    public static IEnumerable<Dictionary<string, object>> AllAngular() =>
        from authentication in Booleans
        from uiLibrary in new[] { "angular-material", "none" }
        from state in new[] { "signals", "ngrx-signal-store" }
        from ssr in Booleans
        select Angular(uiLibrary, state, ssr, authentication);

    public static Dictionary<string, object> DotNet(string architecture, string persistence, bool authentication, bool worker, bool? cqrs)
    {
        var decisions = new Dictionary<string, object>
        {
            ["target"] = "dotnet",
            ["authentication"] = authentication,
            ["architecture"] = architecture,
            ["persistence"] = persistence,
            ["background-processing"] = worker,
        };
        if (cqrs is { } value)
        {
            decisions["domain-complexity"] = value;
        }

        return decisions;
    }

    public static Dictionary<string, object> Angular(string uiLibrary, string state, bool ssr, bool authentication) => new()
    {
        ["target"] = "angular",
        ["authentication"] = authentication,
        ["ui-library"] = uiLibrary,
        ["state-management"] = state,
        ["server-side-rendering"] = ssr,
    };

    public static Dictionary<string, object> Fullstack(
        string architecture, string persistence, bool authentication, bool worker, bool? cqrs, string uiLibrary, string state, bool ssr)
    {
        var decisions = DotNet(architecture, persistence, authentication, worker, cqrs);
        decisions["target"] = "fullstack";
        decisions["ui-library"] = uiLibrary;
        decisions["state-management"] = state;
        decisions["server-side-rendering"] = ssr;
        return decisions;
    }

    /// <summary>A short label for test output, such as <c>dotnet clean-architecture ef-core-sqlite auth cqrs</c>.</summary>
    public static string Describe(IReadOnlyDictionary<string, object> decisions) =>
        string.Join(' ', decisions.Select(d => d.Value switch
        {
            true => d.Key,
            false => $"no-{d.Key}",
            _ => d.Value.ToString(),
        }));
}
