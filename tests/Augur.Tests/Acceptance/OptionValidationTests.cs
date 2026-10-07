using Augur.Tests.Support;

namespace Augur.Tests.Acceptance;

public sealed class OptionValidationTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("301")]
    public async Task A_timeout_outside_1_to_300_seconds_is_rejected_with_the_allowed_range(string timeout)
    {
        using var cli = new CliRunner();

        var result = await cli.RunAsync("plan", "--spec", "spec.md", "--name", "Contoso.Orders", "--timeout", timeout);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("error: --timeout must be between 1 and 300 seconds\n", result.Stderr);
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("-0.1")]
    public async Task A_minimum_confidence_outside_0_to_1_is_rejected(string value)
    {
        using var cli = new CliRunner();

        var result = await cli.RunAsync("plan", "--spec", "spec.md", "--name", "Contoso.Orders", "--min-confidence", value);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("error: --min-confidence must be between 0 and 1\n", result.Stderr);
    }

    [Fact]
    public async Task More_than_four_images_are_rejected()
    {
        using var cli = new CliRunner();

        var result = await cli.RunAsync(
            "plan", "--spec", "spec.md", "--name", "Contoso.Orders",
            "--image", "1.png", "--image", "2.png", "--image", "3.png", "--image", "4.png", "--image", "5.png");

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("error: at most 4 images are allowed\n", result.Stderr);
    }

    [Fact]
    public async Task A_missing_required_option_is_named()
    {
        using var cli = new CliRunner();

        var result = await cli.RunAsync("plan", "--name", "Contoso.Orders");

        Assert.Equal(2, result.ExitCode);
        Assert.Matches(@"^error: .*--spec.* is required\.?\n$", result.Stderr);
    }

    [Fact]
    public async Task An_unknown_verbosity_is_rejected()
    {
        using var cli = new CliRunner();

        var result = await cli.RunAsync("catalog", "--verbosity", "loud");

        Assert.Equal(2, result.ExitCode);
        Assert.StartsWith("error: ", result.Stderr);
        Assert.Contains("loud", result.Stderr);
    }
}
