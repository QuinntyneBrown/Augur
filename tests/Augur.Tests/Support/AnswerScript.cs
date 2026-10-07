using System.Text.Json.Nodes;

namespace Augur.Tests.Support;

/// <summary>Builds an <c>--oracle-script</c> answer file in the Decisions API result format.</summary>
public sealed class AnswerScript
{
    private readonly JsonObject _answers = [];

    /// <summary>Confident answers for a .NET minimal-API backend: no score decision is asked.</summary>
    public static AnswerScript DotNetMinimalApi() => new AnswerScript()
        .Choice("target", "dotnet")
        .Predicate("authentication", 0.1)
        .Choice("architecture", "minimal-api")
        .Choice("persistence", "ef-core-sqlite")
        .Predicate("background-processing", 0.05);

    /// <summary>Confident answers for every decision a fullstack clean-architecture plan asks.</summary>
    public static AnswerScript FullstackCleanArchitecture() => new AnswerScript()
        .Choice("target", "fullstack")
        .Predicate("authentication", 0.95)
        .Choice("architecture", "clean-architecture")
        .Choice("persistence", "ef-core-postgresql")
        .Predicate("background-processing", 0.1)
        .Score("domain-complexity", 2.4, 0.85)
        .Choice("ui-library", "angular-material")
        .Choice("state-management", "signals")
        .Predicate("server-side-rendering", 0.1);

    public AnswerScript Predicate(string id, double probability)
    {
        _answers[id] = new JsonObject { ["type"] = "predicate", ["probability"] = probability };
        return this;
    }

    public AnswerScript Choice(string id, string choice, double confidence = 0.9)
    {
        _answers[id] = new JsonObject
        {
            ["type"] = "choice",
            ["choice"] = choice,
            ["confidence"] = confidence,
            ["probabilities"] = new JsonArray(new JsonObject { ["value"] = choice, ["probability"] = confidence }),
        };
        return this;
    }

    public AnswerScript Score(string id, double score, double confidence)
    {
        _answers[id] = new JsonObject
        {
            ["type"] = "score",
            ["score"] = score,
            ["confidence"] = confidence,
            ["probabilities"] = new JsonArray(
                new JsonObject { ["value"] = 0, ["label"] = "trivial", ["probability"] = 0.05 },
                new JsonObject { ["value"] = 1, ["label"] = "crud", ["probability"] = 0.15 },
                new JsonObject { ["value"] = 2, ["label"] = "business-rules", ["probability"] = 0.6 },
                new JsonObject { ["value"] = 3, ["label"] = "complex-domain", ["probability"] = 0.2 }),
        };
        return this;
    }

    public AnswerScript Refusal(string id)
    {
        _answers[id] = new JsonObject { ["type"] = "refusal" };
        return this;
    }

    public AnswerScript Without(string id)
    {
        _answers.Remove(id);
        return this;
    }

    public AnswerScript Raw(string id, JsonObject raw)
    {
        _answers[id] = raw;
        return this;
    }

    public string ToJson() => new JsonObject { ["answers"] = _answers.DeepClone() }.ToJsonString();

    /// <summary>Writes the script into the runner's working directory and returns its relative path.</summary>
    public string WriteTo(CliRunner cli, string relativePath = "answers.json")
    {
        cli.WriteFile(relativePath, ToJson());
        return relativePath;
    }
}
