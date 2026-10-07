using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Augur.Core;

/// <summary>
/// The one JSON format augur writes: object keys sorted ordinally, non-ASCII characters written literally,
/// LF line endings, and (when indented) two-space indentation with a trailing newline.
/// </summary>
public static class CanonicalJson
{
    /// <summary>Strict reading: no comments, no trailing commas, no duplicate properties.</summary>
    public static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        AllowDuplicateProperties = false,
    };

    /// <summary>Two-space indented, LF, trailing newline: the format of plans, lockfiles, and <c>--json</c> output.</summary>
    public static string Serialize(JsonNode? node) => Write(node, indented: true) + "\n";

    /// <summary>No whitespace at all: the input to hashing.</summary>
    public static string SerializeCompact(JsonNode? node) => Write(node, indented: false);

    /// <summary>Parses strictly, so hand-edited files with comments or duplicate keys are rejected.</summary>
    public static JsonNode? Parse(string json) => JsonNode.Parse(json, documentOptions: DocumentOptions);

    private static string Write(JsonNode? node, bool indented)
    {
        var sorted = Sort(node);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = indented,
            IndentSize = 2,
            NewLine = "\n",
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }))
        {
            if (sorted is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                sorted.WriteTo(writer);
            }
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static JsonNode? Sort(JsonNode? node) => node switch
    {
        JsonObject obj => new JsonObject(obj
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => KeyValuePair.Create(p.Key, Sort(p.Value)))),
        JsonArray array => new JsonArray([.. array.Select(Sort)]),
        null => null,
        _ => node.DeepClone(),
    };
}
