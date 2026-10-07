using Augur.Tests.Support;

namespace Augur.Tests.Acceptance;

public sealed class PathHandlingTests
{
    [Fact]
    public async Task Paths_with_spaces_and_non_ascii_characters_work_for_plan_emit_and_explain()
    {
        using var cli = new CliRunner();
        const string folder = "my specs/é ü 日本";
        cli.WriteFile($"{folder}/spec file.md", "Build an order tracking API.\n");
        var script = AnswerScript.DotNetMinimalApi().WriteTo(cli, $"{folder}/answers ü.json");

        var plan = await cli.RunAsync(
            "plan", "--spec", $"{folder}/spec file.md", "--name", "Contoso.Orders", "--oracle-script", script,
            "--out", $"{folder}/plan é.json", "--lock", $"{folder}/decisions 日本.json");
        var emit = await cli.RunAsync("emit", "--plan", $"{folder}/plan é.json", "--out", $"{folder}/out dir 日本");
        var explain = await cli.RunAsync("explain", "--lock", $"{folder}/decisions 日本.json");

        Assert.Equal(0, plan.ExitCode);
        Assert.Equal(0, emit.ExitCode);
        Assert.Equal(0, explain.ExitCode);
        Assert.True(cli.Exists($"{folder}/out dir 日本/Contoso.Orders.slnx"));
        Assert.Contains("target = dotnet (script)", explain.Stdout);
    }

    [Fact]
    public async Task An_absolute_path_is_used_as_given()
    {
        using var cli = new CliRunner();
        var spec = cli.WriteFile("spec.md", "Build an order tracking API.\n");
        var script = AnswerScript.DotNetMinimalApi().WriteTo(cli);

        var result = await cli.RunAsync("plan", "--spec", spec, "--name", "Contoso.Orders", "--oracle-script", cli.PathOf(script), "--lock", cli.PathOf("lock.json"));

        Assert.Equal(0, result.ExitCode);
        Assert.True(cli.Exists("lock.json"));
    }
}
