using System.Text.Json;
using System.Text.Json.Nodes;
using Augur.Core;
using Augur.Core.Decisions;
using Augur.Core.Intake;

namespace Augur.Oracle.Scripted;

/// <summary>
/// Answers questions from a JSON file of the form <c>{ "answers": { "&lt;id&gt;": { ...raw result... } } }</c>,
/// where each raw result uses the Decisions API answer fields. It never touches the network.
/// </summary>
public sealed class ScriptedOracle : IDecisionOracle
{
    private readonly IReadOnlyDictionary<string, JsonNode?> _answers;

    private ScriptedOracle(IReadOnlyDictionary<string, JsonNode?> answers) => _answers = answers;

    /// <summary>Reads the answer script at <paramref name="fullPath"/>, naming it as <paramref name="displayPath"/> in errors.</summary>
    /// <exception cref="UsageException">The file is missing or is not a valid answer script.</exception>
    public static ScriptedOracle Load(string displayPath, string fullPath)
    {
        if (!File.Exists(fullPath))
        {
            throw new UsageException($"answer script not found: {displayPath}");
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

        if (root?["answers"] is not JsonObject answers)
        {
            throw new UsageException($"{displayPath} must be a JSON object with an 'answers' object");
        }

        return new ScriptedOracle(answers.ToDictionary(a => a.Key, a => a.Value, StringComparer.Ordinal));
    }

    public Task<IReadOnlyList<DecisionAnswer>> AnswerAsync(
        SpecificationInput input,
        IReadOnlyList<DecisionRequest> requests,
        CancellationToken cancellationToken)
    {
        var missing = requests.Where(r => !_answers.ContainsKey(r.Definition.Id)).Select(r => r.Definition.Id).ToList();
        if (missing.Count > 0)
        {
            throw new MissingScriptedAnswerException(missing);
        }

        var answers = new List<DecisionAnswer>(requests.Count);
        foreach (var request in requests)
        {
            try
            {
                answers.Add(RawResultJson.Read(_answers[request.Definition.Id], request.Definition, DecisionSource.Script));
            }
            catch (FormatException ex)
            {
                throw new UsageException($"answer script entry '{request.Definition.Id}' {ex.Message}", ex);
            }
        }

        return Task.FromResult<IReadOnlyList<DecisionAnswer>>(answers);
    }
}

/// <summary>The answer script has no entry for a decision that had to be asked (exit code 3).</summary>
public sealed class MissingScriptedAnswerException(IReadOnlyList<string> decisionIds)
    : UnresolvedDecisionsException($"the answer script has no answer for: {string.Join(", ", decisionIds)}", decisionIds);
