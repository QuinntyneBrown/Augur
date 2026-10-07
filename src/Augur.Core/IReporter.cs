namespace Augur.Core;

/// <summary>How much augur writes to stderr.</summary>
public enum Verbosity
{
    Quiet,
    Normal,
    Detailed,
    Diagnostic,
}

/// <summary>
/// The only way augur code writes diagnostics. Everything goes to stderr; stdout is reserved for
/// machine-readable output.
/// </summary>
public interface IReporter
{
    Verbosity Level { get; }

    /// <summary>A problem the user must see, written at every verbosity, as <c>error: ...</c>.</summary>
    void Error(string message);

    /// <summary>Something the user should know about, written at normal and above, as <c>warning: ...</c>.</summary>
    void Warn(string message);

    /// <summary>Progress and summaries, written at normal and above.</summary>
    void Info(string message);

    /// <summary>Per-decision detail, written at detailed and above.</summary>
    void Detail(string message);

    /// <summary>Request-level diagnostics, written only at diagnostic.</summary>
    void Diagnostic(string message);
}
