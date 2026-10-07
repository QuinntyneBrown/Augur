using System.Text;
using Augur.Tests.Support;

namespace Augur.Tests.Acceptance;

public sealed class SpecificationInputTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13];

    [Fact]
    public async Task A_missing_specification_file_is_named()
    {
        using var cli = new CliRunner();

        var result = await cli.RunAsync("plan", "--spec", "missing.md", "--name", "Contoso.Orders");

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("error: specification file not found: missing.md\n", result.Stderr);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  \n\t \r\n")]
    public async Task An_empty_or_whitespace_specification_is_rejected(string content)
    {
        using var cli = new CliRunner();
        cli.WriteFile("spec.md", content);

        var result = await cli.RunAsync("plan", "--spec", "spec.md", "--name", "Contoso.Orders");

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("error: the specification is empty\n", result.Stderr);
    }

    [Fact]
    public async Task A_specification_over_256_KiB_is_rejected()
    {
        using var cli = new CliRunner();
        cli.WriteFile("spec.md", new string('a', 262_145));

        var result = await cli.RunAsync("plan", "--spec", "spec.md", "--name", "Contoso.Orders");

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("error: the specification exceeds 256 KiB\n", result.Stderr);
    }

    [Fact]
    public async Task A_specification_over_256_KiB_on_stdin_is_rejected()
    {
        using var cli = new CliRunner();

        var result = await cli.RunWithStdinAsync(new string('a', 262_145), "plan", "--spec", "-", "--name", "Contoso.Orders");

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("error: the specification exceeds 256 KiB\n", result.Stderr);
    }

    [Fact]
    public async Task A_specification_with_invalid_UTF8_is_rejected()
    {
        using var cli = new CliRunner();
        cli.WriteBytes("spec.md", [.. Encoding.UTF8.GetBytes("Build an app "), 0xC3, 0x28]);

        var result = await cli.RunAsync("plan", "--spec", "spec.md", "--name", "Contoso.Orders");

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("error: the specification is not valid UTF-8\n", result.Stderr);
    }

    [Fact]
    public async Task A_text_file_named_like_an_image_is_rejected()
    {
        using var cli = new CliRunner();
        cli.WriteFile("spec.md", "Build an order tracking API.");
        cli.WriteFile("sketch.png", "this is not an image");

        var result = await cli.RunAsync("plan", "--spec", "spec.md", "--name", "Contoso.Orders", "--image", "sketch.png");

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("error: sketch.png is not a supported image (PNG, JPEG, or WEBP)\n", result.Stderr);
    }

    [Fact]
    public async Task An_image_over_10_MiB_is_rejected()
    {
        using var cli = new CliRunner();
        cli.WriteFile("spec.md", "Build an order tracking API.");
        var image = new byte[10_485_761];
        Png.CopyTo(image, 0);
        cli.WriteBytes("big.png", image);

        var result = await cli.RunAsync("plan", "--spec", "spec.md", "--name", "Contoso.Orders", "--image", "big.png");

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("error: big.png exceeds 10 MiB\n", result.Stderr);
    }

    [Fact]
    public async Task A_missing_image_file_is_named()
    {
        using var cli = new CliRunner();
        cli.WriteFile("spec.md", "Build an order tracking API.");

        var result = await cli.RunAsync("plan", "--spec", "spec.md", "--name", "Contoso.Orders", "--image", "nowhere.png");

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("error: image file not found: nowhere.png\n", result.Stderr);
    }
}
