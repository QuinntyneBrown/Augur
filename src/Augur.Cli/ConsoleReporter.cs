using Augur.Core;

namespace Augur.Cli;

/// <summary>Writes diagnostics to stderr at the selected verbosity, removing any registered secret from every line.</summary>
internal sealed class ConsoleReporter(TextWriter stderr, Verbosity level) : IReporter
{
    private readonly List<string> _secrets = [];

    public Verbosity Level { get; } = level;

    /// <summary>Registers a value that must never be written, such as the API key.</summary>
    public void AddSecret(string? secret)
    {
        if (!string.IsNullOrEmpty(secret))
        {
            _secrets.Add(secret);
        }
    }

    public string Redact(string message)
    {
        foreach (var secret in _secrets)
        {
            message = message.Replace(secret, "[redacted]", StringComparison.Ordinal);
        }

        return message;
    }

    public void Error(string message) => Write("error: " + message);

    public void Warn(string message)
    {
        if (Level >= Verbosity.Normal)
        {
            Write("warning: " + message);
        }
    }

    public void Info(string message)
    {
        if (Level >= Verbosity.Normal)
        {
            Write(message);
        }
    }

    public void Detail(string message)
    {
        if (Level >= Verbosity.Detailed)
        {
            Write(message);
        }
    }

    public void Diagnostic(string message)
    {
        if (Level >= Verbosity.Diagnostic)
        {
            Write(message);
        }
    }

    private void Write(string message) => stderr.WriteLine(Redact(message));
}
