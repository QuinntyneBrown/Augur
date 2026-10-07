using Augur.Tests.Support;

namespace Augur.Tests.Acceptance;

public sealed class CancellationTests
{
    [Fact]
    public async Task A_cancelled_run_reports_cancelled_and_exits_with_130()
    {
        using var cli = new CliRunner();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await cli.RunAsync(["catalog"], stdin: null, cancellation.Token);

        Assert.Equal(130, result.ExitCode);
        Assert.Empty(result.Stdout);
        Assert.Equal("error: cancelled\n", result.Stderr);
    }
}
