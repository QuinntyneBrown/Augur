using Augur.Tests.Support;

namespace Augur.Tests.Acceptance;

public sealed class CommandLineTests
{
    [Fact]
    public async Task Running_augur_with_no_arguments_writes_help_to_stderr_and_exits_with_2()
    {
        using var cli = new CliRunner();

        var result = await cli.RunAsync();

        Assert.Equal(2, result.ExitCode);
        Assert.Empty(result.Stdout);
        Assert.Contains("Usage:", result.Stderr);
    }
}
