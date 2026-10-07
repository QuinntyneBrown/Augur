using System.Reflection;
using Augur.Cli;
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

    [Fact]
    public async Task Version_prints_the_tool_version_and_the_catalog_version()
    {
        using var cli = new CliRunner();
        var toolVersion = typeof(Program).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

        var result = await cli.RunAsync("--version");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal($"augur {toolVersion} (catalog 2)\n", result.Stdout);
        Assert.Matches(@"^\d+\.\d+\.\d+$", toolVersion);
    }
}
