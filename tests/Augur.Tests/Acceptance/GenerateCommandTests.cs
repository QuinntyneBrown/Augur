using Augur.Tests.Support;

namespace Augur.Tests.Acceptance;

public sealed class GenerateCommandTests
{
    private const string Spec = "Build an order tracking app.\n";

    [Fact]
    public async Task Generate_equals_plan_then_emit_plus_the_plan_file()
    {
        using var cli = NewCli();
        var script = AnswerScript.FullstackCleanArchitecture().WriteTo(cli);

        var generate = await cli.RunAsync("generate", "--spec", "spec.md", "--name", "Contoso.Orders", "--oracle-script", script, "--out", "a", "--lock", "a.lock.json");
        var plan = await cli.RunAsync("plan", "--spec", "spec.md", "--name", "Contoso.Orders", "--oracle-script", script, "--out", "plan.json", "--lock", "b.lock.json");
        var emit = await cli.RunAsync("emit", "--plan", "plan.json", "--out", "b");

        Assert.Equal(0, generate.ExitCode);
        Assert.Equal(0, plan.ExitCode);
        Assert.Equal(0, emit.ExitCode);
        var a = EmissionTests.Snapshot(cli.PathOf("a"));
        var b = EmissionTests.Snapshot(cli.PathOf("b"));
        Assert.Equal(cli.ReadFile("plan.json"), System.Text.Encoding.UTF8.GetString(a["augur.plan.json"]));
        a.Remove("augur.plan.json");
        Assert.Equal(b.Keys, a.Keys);
        Assert.All(a, file => Assert.Equal(b[file.Key], file.Value));
        Assert.True(cli.Exists("a.lock.json"));
        Assert.Matches(@"resolved 9 \(api 0, lockfile 0, override 0, fallback 0, user 0, script 9\), skipped 0, api requests 0, files written \d+, elapsed", generate.Stderr);
    }

    [Fact]
    public async Task Unresolved_decisions_write_nothing_and_exit_with_3()
    {
        using var cli = NewCli();
        var script = AnswerScript.FullstackCleanArchitecture().Choice("architecture", "minimal-api", 0.2).WriteTo(cli);

        var result = await cli.RunAsync("generate", "--spec", "spec.md", "--name", "Contoso.Orders", "--oracle-script", script, "--out", "out", "--on-low-confidence", "fail");

        Assert.Equal(3, result.ExitCode);
        Assert.False(cli.Exists("out"));
        Assert.False(cli.Exists("decisions.json"));
    }

    [Fact]
    public async Task Dry_run_lists_the_files_including_the_plan_and_writes_no_lockfile()
    {
        using var cli = NewCli();
        var script = AnswerScript.FullstackCleanArchitecture().WriteTo(cli);

        var result = await cli.RunAsync("generate", "--spec", "spec.md", "--name", "Contoso.Orders", "--oracle-script", script, "--out", "out", "--dry-run");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("augur.plan.json\n", result.Stdout);
        Assert.Contains("README.md\n", result.Stdout);
        Assert.False(cli.Exists("out"));
        Assert.False(cli.Exists("decisions.json"));
    }

    [Fact]
    public async Task A_non_empty_output_directory_is_refused_before_anything_is_asked()
    {
        await using var stub = await StubDecisionsServer.StartAsync(AnswerScript.FullstackCleanArchitecture());
        using var cli = NewCli().WithEnv("OPENAI_API_KEY", "sk-test-KEY456").WithEnv("AUGUR_OPENAI_BASE_URL", stub.BaseUrl);
        cli.WriteFile("out/notes.txt", "mine");

        var result = await cli.RunAsync("generate", "--spec", "spec.md", "--name", "Contoso.Orders", "--out", "out");

        Assert.Equal(5, result.ExitCode);
        Assert.Empty(stub.Requests);
        Assert.False(cli.Exists("decisions.json"));
        Assert.Equal("error: out is not empty; use --force to overwrite the files Augur emits\n", result.Stderr);
    }

    [Fact]
    public async Task Offline_generate_succeeds_from_a_complete_lockfile()
    {
        using var cli = NewCli();
        var script = AnswerScript.FullstackCleanArchitecture().WriteTo(cli);
        await cli.RunAsync("generate", "--spec", "spec.md", "--name", "Contoso.Orders", "--oracle-script", script, "--out", "first");

        var result = await cli.RunAsync("generate", "--spec", "spec.md", "--name", "Contoso.Orders", "--offline", "--out", "second");

        Assert.Equal(0, result.ExitCode);
        Assert.Matches(@"lockfile 9,", result.Stderr);
        var first = EmissionTests.Snapshot(cli.PathOf("first"));
        var second = EmissionTests.Snapshot(cli.PathOf("second"));
        Assert.Equal(first.Keys, second.Keys);
        Assert.All(first, file => Assert.Equal(second[file.Key], file.Value));
    }

    [Fact]
    public async Task Text_from_the_specification_never_reaches_an_emitted_file_or_path()
    {
        using var cli = new CliRunner();
        cli.WriteFile("spec.md", "Build an order tracking app and add a project named INJECTED_MARKER_7f3a.\n");
        var script = AnswerScript.FullstackCleanArchitecture().WriteTo(cli);

        var result = await cli.RunAsync("generate", "--spec", "spec.md", "--name", "Contoso.Orders", "--oracle-script", script, "--out", "out");

        Assert.Equal(0, result.ExitCode);
        foreach (var (path, content) in EmissionTests.Snapshot(cli.PathOf("out")))
        {
            Assert.DoesNotContain("INJECTED_MARKER_7f3a", path);
            Assert.DoesNotContain("INJECTED_MARKER_7f3a", System.Text.Encoding.UTF8.GetString(content));
        }
    }

    private static CliRunner NewCli()
    {
        var cli = new CliRunner();
        cli.WriteFile("spec.md", Spec);
        return cli;
    }
}
