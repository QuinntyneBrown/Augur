using System.Text.Json;

namespace Augur.Core;

public static class JsonErrors
{
    /// <summary>"answers.json is not valid JSON (line 3, position 14): '}' is invalid after a property name."</summary>
    public static string Describe(string displayPath, JsonException ex)
    {
        var message = ex.Message;
        foreach (var marker in new[] { " LineNumber:", " Path:" })
        {
            var index = message.IndexOf(marker, StringComparison.Ordinal);
            if (index > 0)
            {
                message = message[..index];
            }
        }

        return ex.LineNumber is { } line
            ? $"{displayPath} is not valid JSON (line {line + 1}, position {(ex.BytePositionInLine ?? 0) + 1}): {message.Trim()}"
            : $"{displayPath} is not valid JSON: {message.Trim()}";
    }
}
