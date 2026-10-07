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

public sealed class CancellationDuringRequestTests
{
    [Fact]
    public async Task Cancelling_while_waiting_on_the_api_exits_with_130_within_2_seconds_and_changes_nothing()
    {
        await using var stub = await StubDecisionsServer.StartAsync((_, _) => Task.FromResult(StubResponse.Never));
        using var cli = new CliRunner().WithEnv("OPENAI_API_KEY", "sk-test-KEY456").WithEnv("AUGUR_OPENAI_BASE_URL", stub.BaseUrl);
        cli.WriteFile("spec.md", "Build an order tracking API.\n");
        cli.WriteFile("decisions.json", "{ \"lockfileVersion\": 1, \"catalogVersion\": 2, \"inputHash\": \"previous\", \"entries\": [] }\n");
        using var cancellation = new CancellationTokenSource();

        var run = cli.RunAsync(["plan", "--spec", "spec.md", "--name", "Contoso.Orders", "--out", "plan.json"], stdin: null, cancellation.Token);
        while (stub.Requests.Count == 0 && !run.IsCompleted)
        {
            await Task.Delay(20);
        }

        var clock = System.Diagnostics.Stopwatch.StartNew();
        await cancellation.CancelAsync();
        var result = await run;

        Assert.Equal(130, result.ExitCode);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(2), $"took {clock.Elapsed}");
        Assert.Single(stub.Requests);
        Assert.EndsWith("error: cancelled\n", result.Stderr);
        Assert.Equal("{ \"lockfileVersion\": 1, \"catalogVersion\": 2, \"inputHash\": \"previous\", \"entries\": [] }\n", cli.ReadFile("decisions.json"));
        Assert.False(cli.Exists("plan.json"));
    }
}
