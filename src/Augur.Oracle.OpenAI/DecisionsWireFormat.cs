using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Augur.Core;
using Augur.Core.Catalog;
using Augur.Core.Decisions;
using Augur.Core.Intake;

namespace Augur.Oracle.OpenAI;

/// <summary>Builds the <c>POST /v1/decisions</c> body: model, one user message with the specification and images, and the questions.</summary>
public static class DecisionsRequestBuilder
{
    /// <summary>Writes the body as UTF-8 JSON. Each image is encoded straight into the buffer, one at a time, to bound memory.</summary>
    public static byte[] Build(SpecificationInput input, IReadOnlyList<DecisionRequest> requests, string model)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            json.WriteStartObject();
            json.WriteString("model", model);

            json.WriteStartArray("input");
            json.WriteStartObject();
            json.WriteString("role", "user");
            json.WriteStartArray("content");
            json.WriteStartObject();
            json.WriteString("type", "input_text");
            json.WriteString("text", input.Text);
            json.WriteEndObject();
            foreach (var image in input.Images)
            {
                json.WriteStartObject();
                json.WriteString("type", "input_image");
                json.WriteString("image_url", image.ToDataUrl());
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteEndObject();
            json.WriteEndArray();

            json.WriteStartArray("questions");
            foreach (var request in requests)
            {
                WriteQuestion(json, request.Definition);
            }

            json.WriteEndArray();
            json.WriteEndObject();
        }

        return buffer.ToArray();
    }

    private static void WriteQuestion(Utf8JsonWriter json, DecisionDefinition definition)
    {
        json.WriteStartObject();
        json.WriteString("type", definition.Type.ToWireName());
        json.WriteString("name", definition.Id);
        json.WriteString("instructions", definition.Instructions);
        switch (definition)
        {
            case ChoiceDefinition choice:
                json.WriteStartArray("choices");
                foreach (var option in choice.Options)
                {
                    json.WriteStartObject();
                    json.WriteString("value", option.Value);
                    json.WriteString("description", option.Description);
                    json.WriteEndObject();
                }

                json.WriteEndArray();
                break;
            case ScoreDefinition score:
                json.WriteStartArray("levels");
                foreach (var level in score.Levels)
                {
                    json.WriteStartObject();
                    json.WriteString("label", level.Label);
                    json.WriteString("description", level.Description);
                    json.WriteEndObject();
                }

                json.WriteEndArray();
                break;
        }

        json.WriteEndObject();
    }
}

/// <summary>Matches the <c>answers</c> of a response to the questions asked, by name.</summary>
public static class DecisionsResponseParser
{
    /// <exception cref="DecisionsApiException">The response is not JSON, lacks an answer, or has an answer of the wrong shape.</exception>
    public static IReadOnlyList<DecisionAnswer> Parse(string body, IReadOnlyList<DecisionRequest> requests, string model, string? requestId)
    {
        var suffix = $" (request id {requestId ?? "unknown"})";
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(body);
        }
        catch (JsonException ex)
        {
            throw new DecisionsApiException($"the Decisions API returned a response that is not JSON{suffix}", ex);
        }

        if (root?["answers"] is not JsonArray answers)
        {
            throw new DecisionsApiException($"the Decisions API response has no answers array{suffix}");
        }

        var byName = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var answer in answers.OfType<JsonObject>())
        {
            if (answer["name"] is JsonValue name && name.GetValueKind() == JsonValueKind.String)
            {
                byName[name.GetValue<string>()] = answer;
            }
        }

        var unknown = byName.Keys.Where(n => requests.All(r => r.Definition.Id != n)).ToList();
        if (unknown.Count > 0)
        {
            throw new DecisionsApiException($"the Decisions API response answers a question that was not asked: {string.Join(", ", unknown)}{suffix}");
        }

        return [.. requests.Select(request =>
        {
            var id = request.Definition.Id;
            if (!byName.TryGetValue(id, out var raw))
            {
                throw new DecisionsApiException($"the Decisions API response has no answer for {id}{suffix}");
            }

            try
            {
                return RawResultJson.Read(raw, request.Definition, DecisionSource.Api) with { Model = model };
            }
            catch (FormatException ex)
            {
                throw new DecisionsApiException($"the Decisions API answer for {id} {ex.Message}{suffix}", ex);
            }
        })];
    }
}
