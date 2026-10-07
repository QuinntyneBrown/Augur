using System.Reflection;
using System.Text.RegularExpressions;
using Augur.Cli;
using Augur.Tests.Support;

namespace Augur.Tests.Acceptance;

public sealed class CommandLineTests
{
    private static readonly string[] Commands = ["plan", "emit", "generate", "explain", "catalog", "schema"];

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

    [Fact]
    public async Task Help_lists_all_six_commands_with_a_description()
    {
        using var cli = new CliRunner();

        var result = await cli.RunAsync("--help");

        Assert.Equal(0, result.ExitCode);
        foreach (var command in Commands)
        {
            Assert.Matches(new Regex($@"^\s+{command}\s+\S.*$", RegexOptions.Multiline), result.Stdout);
        }
    }

    [Theory]
    [InlineData(new[] { "plan" }, new[] { "--spec", "--image", "--name", "--out", "--set", "--lock", "--refresh", "--offline", "--oracle-script", "--on-low-confidence", "--min-confidence", "--model", "--timeout", "--verbosity" })]
    [InlineData(new[] { "emit" }, new[] { "--plan", "--out", "--force", "--dry-run", "--verbosity" })]
    [InlineData(new[] { "generate" }, new[] { "--spec", "--image", "--name", "--out", "--set", "--lock", "--refresh", "--offline", "--oracle-script", "--on-low-confidence", "--min-confidence", "--model", "--timeout", "--force", "--dry-run", "--verbosity" })]
    [InlineData(new[] { "explain" }, new[] { "--lock", "--json", "--verbosity" })]
    [InlineData(new[] { "catalog" }, new[] { "--json", "--verbosity" })]
    [InlineData(new[] { "schema", "plan" }, new[] { "--verbosity" })]
    public async Task Command_help_lists_every_option_with_a_description(string[] command, string[] options)
    {
        using var cli = new CliRunner();

        var result = await cli.RunAsync([.. command, "--help"]);

        Assert.Equal(0, result.ExitCode);
        foreach (var option in options)
        {
            Assert.Matches(new Regex($@"^\s+(-\w, )?{Regex.Escape(option)}\b.*\s{{2,}}\S.*$", RegexOptions.Multiline), result.Stdout);
        }
    }

    [Theory]
    [InlineData("plan", "--lock", "decisions.json")]
    [InlineData("plan", "--model", "gpt-6-luna")]
    [InlineData("plan", "--timeout", "30")]
    [InlineData("plan", "--verbosity", "normal")]
    [InlineData("explain", "--lock", "decisions.json")]
    public async Task Command_help_shows_option_defaults(string command, string option, string defaultValue)
    {
        using var cli = new CliRunner();

        var result = await cli.RunAsync(command, "--help");

        Assert.Matches(new Regex($@"^\s+{Regex.Escape(option)}\b.*\[default: {Regex.Escape(defaultValue)}\]", RegexOptions.Multiline), result.Stdout);
    }

    [Fact]
    public async Task An_unknown_command_is_named_in_a_single_error_line_with_exit_code_2()
    {
        using var cli = new CliRunner();

        var result = await cli.RunAsync("frobnicate");

        Assert.Equal(2, result.ExitCode);
        Assert.Empty(result.Stdout);
        Assert.Equal("error: Unknown command 'frobnicate'\n", result.Stderr);
    }

    [Fact]
    public async Task An_unknown_option_is_named_in_a_single_error_line_with_exit_code_2()
    {
        using var cli = new CliRunner();

        var result = await cli.RunAsync("catalog", "--nope");

        Assert.Equal(2, result.ExitCode);
        Assert.Empty(result.Stdout);
        Assert.Equal("error: Unknown option '--nope'\n", result.Stderr);
    }

    [Fact]
    public async Task An_api_key_option_does_not_exist()
    {
        using var cli = new CliRunner();

        var result = await cli.RunAsync("catalog", "--api-key", "x");

        Assert.Equal(2, result.ExitCode);
        Assert.StartsWith("error: Unknown option '--api-key'", result.Stderr);
    }

    [Fact]
    public async Task Schema_without_a_subcommand_writes_its_help_to_stderr_and_exits_with_2()
    {
        using var cli = new CliRunner();

        var result = await cli.RunAsync("schema");

        Assert.Equal(2, result.ExitCode);
        Assert.Empty(result.Stdout);
        Assert.Contains("plan", result.Stderr);
    }
}
