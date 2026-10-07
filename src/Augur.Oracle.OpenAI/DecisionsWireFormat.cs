using System.Buffers;
using System.Buffers.Text;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Augur.Core;
using Augur.Core.Catalog;
using Augur.Core.Decisions;
using Augur.Core.Intake;

namespace Augur.Oracle.OpenAI;

/// <summary>
/// Builds the <c>POST /v1/decisions</c> body: model, one user message with the specification and images, and the
/// questions. The message is the large part (up to four 10 MiB images as base64), so it is encoded once per run into one
/// exact-size buffer and shared by every request and retry; each request adds only its small model and question parts.
/// </summary>
public static class DecisionsRequestBuilder
{
    private static readonly JsonWriterOptions WriterOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>The <c>input</c> array as UTF-8 JSON: one user message with the text, then each image as a base64 data URL.</summary>
    public static byte[] Input(SpecificationInput input)
    {
        var text = new ArrayBufferWriter<byte>();
        using (var json = new Utf8JsonWriter(text, WriterOptions))
        {
            json.WriteStringValue(input.Text);
        }

        var head = "[{\"role\":\"user\",\"content\":[{\"type\":\"input_text\",\"text\":"u8;
        var length = head.Length + text.WrittenCount + 1;
        var prefixes = input.Images.Select(i => Encoding.UTF8.GetBytes($",{{\"type\":\"input_image\",\"image_url\":\"data:{i.MediaType};base64,")).ToList();
        for (var i = 0; i < input.Images.Count; i++)
        {
            length += prefixes[i].Length + Base64.GetMaxEncodedToUtf8Length(input.Images[i].Bytes.Length) + 2;
        }

        var tail = "]}]"u8;
        length += tail.Length;

        var buffer = new byte[length];
        var position = 0;
        void Append(ReadOnlySpan<byte> bytes)
        {
            bytes.CopyTo(buffer.AsSpan(position));
            position += bytes.Length;
        }

        Append(head);
        Append(text.WrittenSpan);
        Append("}"u8);
        for (var i = 0; i < input.Images.Count; i++)
        {
            Append(prefixes[i]);
            Base64.EncodeToUtf8(input.Images[i].Bytes.Span, buffer.AsSpan(position), out _, out var written);
            position += written;
            Append("\"}"u8);
        }

        Append(tail);
        return position == buffer.Length ? buffer : buffer[..position];
    }

    /// <summary>The whole body as segments: the shared <paramref name="input"/> between the per-request model and questions.</summary>
    public static IReadOnlyList<ReadOnlyMemory<byte>> Build(ReadOnlyMemory<byte> input, IReadOnlyList<DecisionRequest> requests, string model)
    {
        var head = new ArrayBufferWriter<byte>();
        using (var json = new Utf8JsonWriter(head, WriterOptions))
        {
            json.WriteStartObject();
            json.WriteString("model", model);
            json.WritePropertyName("input");
            json.Flush();
        }

        var questions = new ArrayBufferWriter<byte>();
        using (var json = new Utf8JsonWriter(questions, WriterOptions))
        {
            json.WriteStartArray();
            foreach (var request in requests)
            {
                WriteQuestion(json, request.Definition);
            }

            json.WriteEndArray();
        }

        // The writer emitted `{"model":"...","input":` without a value; the input array follows as raw bytes.
        return [head.WrittenMemory, input, Encoding.UTF8.GetBytes(",\"questions\":"), questions.WrittenMemory, "}"u8.ToArray()];
    }

    /// <summary>Convenience for tests and small inputs: the body as one array.</summary>
    public static byte[] Build(SpecificationInput input, IReadOnlyList<DecisionRequest> requests, string model)
    {
        var segments = Build(Input(input), requests, model);
        var body = new byte[segments.Sum(s => s.Length)];
        var position = 0;
        foreach (var segment in segments)
        {
            segment.Span.CopyTo(body.AsSpan(position));
            position += segment.Length;
        }

        return body;
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
