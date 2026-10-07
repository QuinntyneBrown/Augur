using System.Text;

namespace Augur.Core.Intake;

/// <summary>Reads and checks the specification text and images named on the command line.</summary>
public sealed class SpecificationReader(PathResolver paths, Stream stdin)
{
    public const int MaxSpecBytes = 256 * 1024;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Reads <paramref name="specPath"/> (or stdin for <c>-</c>) and each image, in order.</summary>
    /// <exception cref="UsageException">An input is missing, empty, too large, or malformed.</exception>
    public SpecificationInput Read(string specPath, IReadOnlyList<string> imagePaths)
    {
        var text = ReadText(specPath);
        var images = imagePaths.Select(p => ImageFile.Load(p, paths.Resolve(p))).ToList();
        return SpecificationInput.Create(text, images, specPath == "-" ? "stdin" : specPath);
    }

    private string ReadText(string specPath)
    {
        var bytes = specPath == "-" ? ReadCapped(stdin) : ReadFile(specPath);
        if (bytes.Length > MaxSpecBytes)
        {
            throw new UsageException("the specification exceeds 256 KiB");
        }

        string text;
        try
        {
            var span = bytes.AsSpan();
            if (span.StartsWith(StrictUtf8.Preamble))
            {
                span = span[StrictUtf8.Preamble.Length..];
            }

            text = StrictUtf8.GetString(span);
        }
        catch (DecoderFallbackException)
        {
            throw new UsageException("the specification is not valid UTF-8");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new UsageException("the specification is empty");
        }

        return text;
    }

    private byte[] ReadFile(string specPath)
    {
        var fullPath = paths.Resolve(specPath);
        if (!File.Exists(fullPath))
        {
            throw new UsageException($"specification file not found: {specPath}");
        }

        using var file = File.OpenRead(fullPath);
        return ReadCapped(file);
    }

    /// <summary>Reads at most one byte past the limit, so an oversized input is detected without buffering all of it.</summary>
    private static byte[] ReadCapped(Stream stream)
    {
        var buffer = new byte[MaxSpecBytes + 1];
        var total = 0;
        int read;
        while (total < buffer.Length && (read = stream.Read(buffer, total, buffer.Length - total)) > 0)
        {
            total += read;
        }

        return buffer[..total];
    }
}
