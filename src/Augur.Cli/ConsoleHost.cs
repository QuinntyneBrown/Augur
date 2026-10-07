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
    CancellationToken Token)
{
    /// <summary>Test seam: wraps the emitter so a test can inject a fault or an extra file. Never set by <c>Main</c>.</summary>
    public Func<Augur.Emission.IEmitter, Augur.Emission.IEmitter>? EmitterDecorator { get; init; }
}
