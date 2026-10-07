using System.CommandLine;
using Augur.Core;

namespace Augur.Cli;

/// <summary>Per-invocation state shared by every command handler.</summary>
internal sealed class CliContext(ConsoleHost host)
{
    public ConsoleHost Host { get; } = host;

    public RootCommand Root { get; set; } = null!;

    public Option<string> Verbosity { get; set; } = null!;

    /// <summary>
    /// Runs a command body and maps its outcome to an exit code. This is the one place failures become
    /// a single <c>error:</c> line on stderr.
    /// </summary>
    public async Task<int> RunAsync(ParseResult parseResult, CancellationToken token, Func<CommandRun, Task<ExitCode>> body)
    {
        var reporter = new ConsoleReporter(Host.Stderr, ParseVerbosity(parseResult.GetValue(Verbosity)));
        reporter.AddSecret(Host.Env.GetValueOrDefault("OPENAI_API_KEY"));
        var run = new CommandRun(Host, parseResult, reporter, token);
        try
        {
            token.ThrowIfCancellationRequested();
            return (int)await body(run);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            reporter.Error("cancelled");
            return (int)ExitCode.Cancelled;
        }
        catch (AugurException ex)
        {
            reporter.Error(ex.Message);
            return (int)ex.ExitCode;
        }
        catch (Exception ex)
        {
            reporter.Error(ex.Message);
            reporter.Diagnostic(ex.ToString());
            return (int)ExitCode.InternalError;
        }
    }

    public Task<int> NotImplemented(string command)
    {
        Host.Stderr.WriteLine($"error: '{command}' is not implemented yet");
        return Task.FromResult(1);
    }

    /// <summary>Writes the help for <paramref name="commandPath"/> to <paramref name="writer"/> (the built-in help action only targets stdout).</summary>
    public void WriteHelp(string[] commandPath, TextWriter writer) =>
        Root.Parse([.. commandPath, "--help"], CommandLine.ParserConfiguration).Invoke(CommandLine.Invocation(writer, writer));

    private static Core.Verbosity ParseVerbosity(string? value) => value switch
    {
        "quiet" => Core.Verbosity.Quiet,
        "detailed" => Core.Verbosity.Detailed,
        "diagnostic" => Core.Verbosity.Diagnostic,
        _ => Core.Verbosity.Normal,
    };
}

/// <summary>Everything a running command needs: its arguments, its console, its reporter, and its cancellation token.</summary>
internal sealed record CommandRun(ConsoleHost Host, ParseResult ParseResult, ConsoleReporter Reporter, CancellationToken Token)
{
    /// <summary>Writes machine-readable output. Nothing else is ever written to stdout.</summary>
    public void WriteStdout(string text) => Host.Stdout.Write(text);
}
