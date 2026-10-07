using System.CommandLine;

namespace Augur.Cli;

/// <summary>Per-invocation state shared by every command handler.</summary>
internal sealed class CliContext(ConsoleHost host)
{
    public ConsoleHost Host { get; } = host;

    public RootCommand Root { get; set; } = null!;

    public Task<int> NotImplemented(string command)
    {
        Host.Stderr.WriteLine($"error: '{command}' is not implemented yet");
        return Task.FromResult(1);
    }

    /// <summary>Writes the help for <paramref name="commandPath"/> to <paramref name="writer"/> (the built-in help action only targets stdout).</summary>
    public void WriteHelp(string[] commandPath, TextWriter writer) =>
        Root.Parse([.. commandPath, "--help"], CommandLine.ParserConfiguration).Invoke(CommandLine.Invocation(writer, writer));
}
