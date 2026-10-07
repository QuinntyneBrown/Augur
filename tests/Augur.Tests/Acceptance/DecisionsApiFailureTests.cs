using System.Diagnostics;
using Augur.Tests.Support;

namespace Augur.Tests.Acceptance;

/// <summary>Retry, rejection, and transport failures. These wait out real backoff delays, so each slow case has its own class to run in parallel.</summary>
public sealed class DecisionsApiFailureTests
{
    [Fact]
    public async Task Two_503s_then_success_takes_three_requests_and_succeeds()
    {
        await using var stub = await StubDecisionsServer.StartAsync((request, n) =>
            Task.FromResult(n <= 2 ? StubResponse.Error(503) : StubResponse.Answer(AnswerScript.DotNetMinimalApi(), request)));
        using var cli = ApiCli.For(stub);

        var result = await ApiCli.Plan(cli, "--set", "target=dotnet", "--set", "architecture=minimal-api",
            "--set", "persistence=none", "--set", "background-processing=false");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(3, stub.Requests.Count);
        Assert.Matches("api requests 1,", result.Stderr);
    }

    [Fact]
    public async Task A_rejected_key_is_not_retried_and_never_appears_in_any_output()
    {
        await using var stub = await StubDecisionsServer.StartAsync((_, _) => Task.FromResult(StubResponse.Error(401)));
        using var cli = ApiCli.For(stub).WithEnv("OPENAI_API_KEY", "sk-test-SENTINEL123");

        var result = await ApiCli.Plan(cli, "--verbosity", "diagnostic");

        Assert.Equal(4, result.ExitCode);
        Assert.Single(stub.Requests);
        Assert.EndsWith("error: the Decisions API rejected the API key (HTTP 401, request id req_1)\n", result.Stderr);
        Assert.DoesNotContain("SENTINEL123", result.Stdout + result.Stderr);
        foreach (var file in Directory.EnumerateFiles(cli.WorkingDirectory, "*", SearchOption.AllDirectories))
        {
            Assert.DoesNotContain("SENTINEL123", File.ReadAllText(file));
        }
    }

    [Theory]
    [InlineData(400)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(422)]
    public async Task Client_errors_are_not_retried(int status)
    {
        await using var stub = await StubDecisionsServer.StartAsync((_, _) => Task.FromResult(StubResponse.Error(status)));
        using var cli = ApiCli.For(stub);

        var result = await ApiCli.Plan(cli);

        Assert.Equal(4, result.ExitCode);
        Assert.Single(stub.Requests);
        Assert.Matches($@"error: the Decisions API returned HTTP {status} \w.* \(request id req_1\)\n$", result.Stderr);
    }

    [Fact]
    public async Task An_untrusted_certificate_fails_without_retrying()
    {
        await using var stub = await StubDecisionsServer.StartAsync(
            (request, _) => Task.FromResult(StubResponse.Answer(AnswerScript.DotNetMinimalApi(), request)),
            untrustedHttps: true);
        using var cli = ApiCli.For(stub);
        var clock = Stopwatch.StartNew();

        var result = await ApiCli.Plan(cli);

        Assert.Equal(4, result.ExitCode);
        Assert.Empty(stub.Requests);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(1), $"took {clock.Elapsed}; a retry would wait at least a second");
        Assert.Matches(@"error: could not make a trusted HTTPS connection to 127\.0\.0\.1:\d+: .+\n$", result.Stderr);
    }
}

public sealed class DecisionsApiPersistentFailureTests
{
    [Fact]
    public async Task A_persistent_503_is_tried_four_times_and_reports_the_last_status_and_request_id()
    {
        await using var stub = await StubDecisionsServer.StartAsync((_, n) => Task.FromResult(StubResponse.Error(503) with { RequestId = $"req_{n}" }));
        using var cli = ApiCli.For(stub);

        var result = await ApiCli.Plan(cli);

        Assert.Equal(4, result.ExitCode);
        Assert.Equal(4, stub.Requests.Count);
        Assert.EndsWith("error: the Decisions API returned HTTP 503 Service Unavailable after 4 attempts (request id req_4)\n", result.Stderr);
        var gaps = stub.Requests.Zip(stub.Requests.Skip(1), (a, b) => b.At - a.At).ToList();
        Assert.True(gaps[0] >= TimeSpan.FromSeconds(0.95) && gaps[0] < TimeSpan.FromSeconds(1.5), $"first backoff {gaps[0]}");
        Assert.True(gaps[1] >= TimeSpan.FromSeconds(1.95) && gaps[1] < TimeSpan.FromSeconds(2.8), $"second backoff {gaps[1]}");
        Assert.True(gaps[2] >= TimeSpan.FromSeconds(3.95) && gaps[2] < TimeSpan.FromSeconds(5.3), $"third backoff {gaps[2]}");
    }
}

public sealed class DecisionsApiRetryAfterTests
{
    [Fact]
    public async Task Retry_after_is_honoured()
    {
        await using var stub = await StubDecisionsServer.StartAsync((request, n) =>
            Task.FromResult(n == 1 ? StubResponse.Error(429, retryAfter: "2") : StubResponse.Answer(AnswerScript.DotNetMinimalApi(), request)));
        using var cli = ApiCli.For(stub);

        var result = await ApiCli.Plan(cli);

        Assert.Equal(0, result.ExitCode);
        Assert.True(stub.Requests[1].At - stub.Requests[0].At >= TimeSpan.FromSeconds(1.95));
    }
}

public sealed class DecisionsApiTimeoutTests
{
    [Fact]
    public async Task Each_attempt_is_abandoned_after_the_timeout_and_four_attempts_are_made()
    {
        await using var stub = await StubDecisionsServer.StartAsync((_, _) => Task.FromResult(StubResponse.Never));
        using var cli = ApiCli.For(stub);

        var result = await ApiCli.Plan(cli, "--timeout", "1");

        Assert.Equal(4, result.ExitCode);
        Assert.Equal(4, stub.Requests.Count);
        Assert.EndsWith("error: the Decisions API did not respond within 1 s after 4 attempts\n", result.Stderr);
        var gaps = stub.Requests.Zip(stub.Requests.Skip(1), (a, b) => b.At - a.At).ToList();
        Assert.True(gaps[0] >= TimeSpan.FromSeconds(1.9) && gaps[0] < TimeSpan.FromSeconds(2.9), $"timeout plus first backoff {gaps[0]}");
    }
}

internal static class ApiCli
{
    public static CliRunner For(StubDecisionsServer stub)
    {
        var cli = new CliRunner().WithEnv("OPENAI_API_KEY", "sk-test-KEY456").WithEnv("AUGUR_OPENAI_BASE_URL", stub.BaseUrl);
        cli.WriteFile("spec.md", "Build an order tracking API.\n");
        return cli;
    }

    public static Task<CliResult> Plan(CliRunner cli, params string[] extra) =>
        cli.RunAsync(["plan", "--spec", "spec.md", "--name", "Contoso.Orders", .. extra]);
}
