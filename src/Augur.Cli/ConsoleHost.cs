namespace Augur.Cli;

/// <summary>Everything a single augur invocation reads from or writes to its environment.</summary>
public sealed record ConsoleHost(
    string[] Args,
    Stream Stdin,
    TextWriter Stdout,
    TextWriter Stderr,
    IReadOnlyDictionary<string, string?> Env,
    string Cwd,
    bool StdinIsTerminal,
    bool StderrIsTerminal,
    CancellationToken Token);
