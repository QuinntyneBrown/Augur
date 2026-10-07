using System.Security.Cryptography;
using System.Text;

namespace Augur.Core.Intake;

/// <summary>
/// SHA-256 over the specification text (UTF-8, line endings normalized to LF) followed by the bytes of each
/// image in command-line order, as lower-case hex.
/// </summary>
public static class InputHasher
{
    public static string Hash(string text, IReadOnlyList<ImageFile> images)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        hash.AppendData(Encoding.UTF8.GetBytes(normalized));
        foreach (var image in images)
        {
            hash.AppendData(image.Bytes.Span);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}
