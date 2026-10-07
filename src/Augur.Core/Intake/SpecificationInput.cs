namespace Augur.Core.Intake;

/// <summary>
/// The specification as augur received it: the text (BOM removed, line endings as written) and the images in
/// command-line order, with the input hash that identifies them.
/// </summary>
public sealed record SpecificationInput(string Text, IReadOnlyList<ImageFile> Images, string InputHash, string SpecDisplayName)
{
    public static SpecificationInput Create(string text, IReadOnlyList<ImageFile> images, string specDisplayName) =>
        new(text, images, InputHasher.Hash(text, images), specDisplayName);

    /// <summary>The UTF-8 byte length of the text, as reported in the data disclosure notice.</summary>
    public long TextByteCount => System.Text.Encoding.UTF8.GetByteCount(Text);
}

/// <summary>An image passed with <c>--image</c>, identified by its file signature.</summary>
public sealed record ImageFile(string DisplayName, ReadOnlyMemory<byte> Bytes, string MediaType)
{
    public const int MaxBytes = 10 * 1024 * 1024;

    /// <exception cref="UsageException">The file is missing, too large, or not a PNG, JPEG, or WEBP image.</exception>
    public static ImageFile Load(string displayName, string fullPath)
    {
        if (!File.Exists(fullPath))
        {
            throw new UsageException($"image file not found: {displayName}");
        }

        if (new FileInfo(fullPath).Length > MaxBytes)
        {
            throw new UsageException($"{displayName} exceeds 10 MiB");
        }

        var bytes = File.ReadAllBytes(fullPath);
        var mediaType = ImageSignature.Detect(bytes)
            ?? throw new UsageException($"{displayName} is not a supported image (PNG, JPEG, or WEBP)");
        return new ImageFile(displayName, bytes, mediaType);
    }

    public string ToDataUrl() => $"data:{MediaType};base64,{Convert.ToBase64String(Bytes.Span)}";
}

/// <summary>Identifies an image format from its leading bytes; the file extension is ignored.</summary>
public static class ImageSignature
{
    private static ReadOnlySpan<byte> Png => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static ReadOnlySpan<byte> Jpeg => [0xFF, 0xD8, 0xFF];

    private static ReadOnlySpan<byte> Riff => "RIFF"u8;

    private static ReadOnlySpan<byte> Webp => "WEBP"u8;

    public static string? Detect(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(Png))
        {
            return "image/png";
        }

        if (bytes.StartsWith(Jpeg))
        {
            return "image/jpeg";
        }

        if (bytes.Length >= 12 && bytes.StartsWith(Riff) && bytes[8..12].SequenceEqual(Webp))
        {
            return "image/webp";
        }

        return null;
    }
}
