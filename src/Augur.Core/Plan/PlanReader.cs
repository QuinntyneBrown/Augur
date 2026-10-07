using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Augur.Core.Catalog;
using Augur.Core.Decisions;

namespace Augur.Core.Plan;

/// <summary>One problem with a plan file, located by JSON path.</summary>
public sealed record PlanViolation(string JsonPath, string Message)
{
    public override string ToString() => $"{JsonPath}: {Message}";
}

/// <summary>A plan that breaks the schema or the catalog's rules (exit code 2). Every problem is listed.</summary>
public sealed class PlanValidationException(string displayPath, IReadOnlyList<PlanViolation> violations)
    : UsageException($"{displayPath} is not a valid plan ({violations.Count} {(violations.Count == 1 ? "problem" : "problems")})")
{
    public IReadOnlyList<PlanViolation> Violations { get; } = violations;

    public override IReadOnlyList<string> Details => [.. Violations.Select(v => v.ToString())];
}

/// <summary>Reads a plan file and checks it against the plan format and the installed catalog.</summary>
public static partial class PlanReader
{
    public const int MaxBytes = 1024 * 1024;

    /// <exception cref="UsageException">The file is missing, too large, or not JSON.</exception>
    /// <exception cref="PlanValidationException">The plan breaks the format or the catalog's rules.</exception>
    public static GenerationPlan Read(string displayPath, string fullPath, DecisionCatalog catalog)
    {
        if (!File.Exists(fullPath))
        {
            throw new UsageException($"plan file not found: {displayPath}");
        }

        if (new FileInfo(fullPath).Length > MaxBytes)
        {
            throw new UsageException($"{displayPath} exceeds 1 MiB");
        }

        JsonNode? root;
        try
        {
            root = CanonicalJson.Parse(File.ReadAllText(fullPath));
        }
        catch (JsonException ex)
        {
            throw new UsageException(JsonErrors.Describe(displayPath, ex), ex);
        }

        return Validate(root, displayPath, catalog);
    }

    /// <summary>Applies every rule and collects all violations before deciding.</summary>
    /// <exception cref="PlanValidationException">At least one rule is broken.</exception>
    public static GenerationPlan Validate(JsonNode? root, string displayPath, DecisionCatalog catalog)
    {
        var violations = new List<PlanViolation>();
        void Problem(string path, string message) => violations.Add(new PlanViolation(path, message));

        if (root is not JsonObject plan)
        {
            throw new PlanValidationException(displayPath, [new PlanViolation("$", "a plan must be a JSON object")]);
        }

        foreach (var property in plan.Select(p => p.Key).Where(k => k is not ("planVersion" or "catalogVersion" or "solutionName" or "inputHash" or "decisions")))
        {
            Problem($"$.{property}", "unknown property");
        }

        var planVersion = Integer(plan, "planVersion", Problem);
        if (planVersion is { } version && version != GenerationPlan.CurrentPlanVersion)
        {
            Problem("$.planVersion", $"planVersion {version} is not supported; expected {GenerationPlan.CurrentPlanVersion}");
        }

        var catalogVersion = Integer(plan, "catalogVersion", Problem);
        if (catalogVersion is { } cv && cv != catalog.Version)
        {
            Problem("$.catalogVersion", $"catalogVersion {cv} does not match the installed catalog version {catalog.Version}");
        }

        var nameText = Text(plan, "solutionName", Problem);
        SolutionName? name = null;
        if (nameText is not null && !SolutionName.TryParse(nameText, out name))
        {
            Problem("$.solutionName", "solutionName" + SolutionName.Rule["--name".Length..]);
        }

        var inputHash = Text(plan, "inputHash", Problem);
        if (inputHash is not null && !InputHashPattern().IsMatch(inputHash))
        {
            Problem("$.inputHash", "inputHash must be 64 lower-case hexadecimal characters");
        }

        var present = (plan["decisions"] as JsonObject)?.Select(d => d.Key).ToHashSet(StringComparer.Ordinal) ?? [];
        var decisions = ReadDecisions(plan, catalog, Problem);
        CheckApplicability(decisions, present, catalog, Problem);

        if (violations.Count > 0)
        {
            throw new PlanValidationException(displayPath, violations);
        }

        return new GenerationPlan(catalogVersion!.Value, name!, inputHash!, decisions);
    }

    private static Dictionary<string, PlanDecision> ReadDecisions(JsonObject plan, DecisionCatalog catalog, Action<string, string> problem)
    {
        var decisions = new Dictionary<string, PlanDecision>(StringComparer.Ordinal);
        if (plan["decisions"] is not JsonObject entries)
        {
            problem("$.decisions", plan.ContainsKey("decisions") ? "decisions must be an object" : "missing");
            return decisions;
        }

        foreach (var (id, node) in entries)
        {
            var path = $"$.decisions.{id}";
            if (!catalog.TryGet(id, out var definition))
            {
                problem(path, $"'{id}' is not a catalog decision");
                continue;
            }

            if (node is not JsonObject entry)
            {
                problem(path, "must be an object with value and source");
                continue;
            }

            foreach (var extra in entry.Select(p => p.Key).Where(k => k is not ("value" or "source")))
            {
                problem($"{path}.{extra}", "unknown property");
            }

            var value = DecisionValues.FromJson(definition, entry["value"]);
            if (value is null)
            {
                problem($"{path}.value", entry.ContainsKey("value")
                    ? $"must be a {(definition.IsBoolean ? "boolean" : "string")}"
                    : "missing");
            }
            else if (value == DecisionDefinition.Other)
            {
                problem($"{path}.value", $"'other' cannot be used as a value for {id}");
            }
            else if (!definition.AcceptedValues.Contains(value))
            {
                problem($"{path}.value", $"'{value}' is not an allowed value for {id}; allowed values: {string.Join(", ", definition.AcceptedValues)}");
            }

            var sourceText = entry["source"] is JsonValue s && s.GetValueKind() == JsonValueKind.String ? s.GetValue<string>() : null;
            if (!DecisionSourceExtensions.TryParse(sourceText, out var source))
            {
                problem($"{path}.source", $"must be one of {string.Join(", ", Enum.GetValues<DecisionSource>().Select(x => x.ToWireName()))}");
            }

            if (value is not null && definition.AcceptedValues.Contains(value))
            {
                decisions[id] = new PlanDecision(value, source);
            }
        }

        return decisions;
    }

    /// <summary>A decision must be present exactly when its when clause holds; one without a when clause is always required.</summary>
    private static void CheckApplicability(
        Dictionary<string, PlanDecision> decisions,
        IReadOnlySet<string> present,
        DecisionCatalog catalog,
        Action<string, string> problem)
    {
        var applies = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var definition in DependencyLeveller.Levels(catalog).SelectMany(level => level))
        {
            var path = $"$.decisions.{definition.Id}";
            if (definition.When is not { } when)
            {
                applies[definition.Id] = true;
                if (!present.Contains(definition.Id))
                {
                    problem(path, $"{definition.Id} is required");
                }

                continue;
            }

            var dependency = decisions.GetValueOrDefault(when.DependsOn)?.Value;
            var dependencyApplies = applies.GetValueOrDefault(when.DependsOn);
            var holds = dependencyApplies && dependency is not null && when.AcceptedValues.Contains(dependency);
            applies[definition.Id] = holds;
            var isPresent = present.Contains(definition.Id);
            if (holds && !isPresent)
            {
                problem(path, $"{definition.Id} is required when {when.DependsOn} is {dependency}");
            }
            else if (!holds && isPresent && dependency is not null)
            {
                problem(path, $"{definition.Id} must be absent when {when.DependsOn} is {dependency}");
            }
            else if (!holds && isPresent && present.Contains(when.DependsOn) == decisions.ContainsKey(when.DependsOn))
            {
                problem(path, $"{definition.Id} must be absent because {when.DependsOn} does not apply");
            }
        }
    }

    private static int? Integer(JsonObject plan, string property, Action<string, string> problem)
    {
        if (plan[property] is JsonValue value && value.GetValueKind() == JsonValueKind.Number && value.TryGetValue<int>(out var number))
        {
            return number;
        }

        problem($"$.{property}", plan.ContainsKey(property) ? "must be an integer" : "missing");
        return null;
    }

    private static string? Text(JsonObject plan, string property, Action<string, string> problem)
    {
        if (plan[property] is JsonValue value && value.GetValueKind() == JsonValueKind.String)
        {
            return value.GetValue<string>();
        }

        problem($"$.{property}", plan.ContainsKey(property) ? "must be a string" : "missing");
        return null;
    }

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex InputHashPattern();
}

/// <summary>Builds the JSON Schema for <c>GenerationPlan</c> from the installed catalog.</summary>
public static class PlanSchema
{
    public static string Generate(DecisionCatalog catalog)
    {
        var sources = new JsonArray([.. Enum.GetValues<DecisionSource>().Select(s => (JsonNode?)s.ToWireName())]);
        var decisions = new JsonObject();
        foreach (var definition in catalog.Decisions)
        {
            decisions[definition.Id] = new JsonObject
            {
                ["type"] = "object",
                ["description"] = definition.Instructions,
                ["properties"] = new JsonObject
                {
                    ["value"] = definition.IsBoolean
                        ? new JsonObject { ["type"] = "boolean" }
                        : new JsonObject
                        {
                            ["type"] = "string",
                            ["enum"] = new JsonArray([.. definition.AcceptedValues.Select(v => (JsonNode?)v)]),
                        },
                    ["source"] = new JsonObject { ["type"] = "string", ["enum"] = sources.DeepClone() },
                },
                ["required"] = new JsonArray("value", "source"),
                ["additionalProperties"] = false,
            };
        }

        return CanonicalJson.Serialize(new JsonObject
        {
            ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
            ["$id"] = $"https://github.com/QuinntyneBrown/Augur/schemas/plan-v{GenerationPlan.CurrentPlanVersion}-catalog-v{catalog.Version}.json",
            ["title"] = "GenerationPlan",
            ["description"] = "An Augur generation plan. Which decisions must be present depends on the when clauses shown by `augur catalog`; this schema does not encode them.",
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["planVersion"] = new JsonObject { ["const"] = GenerationPlan.CurrentPlanVersion },
                ["catalogVersion"] = new JsonObject { ["const"] = catalog.Version },
                ["solutionName"] = new JsonObject
                {
                    ["type"] = "string",
                    ["pattern"] = "^[A-Z][A-Za-z0-9]*(\\.[A-Z][A-Za-z0-9]*)*$",
                    ["maxLength"] = SolutionName.MaxLength,
                },
                ["inputHash"] = new JsonObject { ["type"] = "string", ["pattern"] = "^[0-9a-f]{64}$" },
                ["decisions"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = decisions,
                    ["required"] = new JsonArray([.. catalog.Decisions.Where(d => d.When is null).Select(d => (JsonNode?)d.Id)]),
                    ["additionalProperties"] = false,
                },
            },
            ["required"] = new JsonArray("planVersion", "catalogVersion", "solutionName", "inputHash", "decisions"),
            ["additionalProperties"] = false,
        });
    }
}
