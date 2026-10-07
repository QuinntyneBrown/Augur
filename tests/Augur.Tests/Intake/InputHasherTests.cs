using System.Text;
using Augur.Core.Intake;

namespace Augur.Tests.Intake;

public sealed class InputHasherTests
{
    private static readonly ImageFile First = new("a.png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 1 }, "image/png");
    private static readonly ImageFile Second = new("b.png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 2 }, "image/png");

    [Fact]
    public void The_same_images_in_a_different_order_give_a_different_hash()
    {
        Assert.NotEqual(
            InputHasher.Hash("Build an API.", [First, Second]),
            InputHasher.Hash("Build an API.", [Second, First]));
    }

    [Fact]
    public void Line_endings_do_not_change_the_hash()
    {
        Assert.Equal(
            InputHasher.Hash("line one\nline two\n", []),
            InputHasher.Hash("line one\r\nline two\r", []));
    }

    [Fact]
    public void The_hash_is_sha256_of_the_normalized_text_then_the_image_bytes()
    {
        var expected = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
            [.. Encoding.UTF8.GetBytes("a\nb"), .. First.Bytes.ToArray()]));

        Assert.Equal(expected, InputHasher.Hash("a\r\nb", [First]));
    }
}
