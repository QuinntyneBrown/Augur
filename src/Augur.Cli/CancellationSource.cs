using System.Runtime.InteropServices;

namespace Augur.Cli;

/// <summary>
/// Turns Ctrl+C (SIGINT) and SIGTERM into cancellation of a shared token, so in-flight work stops cleanly
/// and augur exits with code 130 instead of being killed.
/// </summary>
internal sealed class CancellationSource : IDisposable
{
    private readonly CancellationTokenSource _source = new();
    private readonly PosixSignalRegistration? _sigterm;

    public CancellationSource()
    {
        Console.CancelKeyPress += OnCancelKeyPress;
        if (!OperatingSystem.IsWindows())
        {
            _sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
            {
                context.Cancel = true;
                _source.Cancel();
            });
        }
    }

    public CancellationToken Token => _source.Token;

    public void Dispose()
    {
        Console.CancelKeyPress -= OnCancelKeyPress;
        _sigterm?.Dispose();
        _source.Dispose();
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
        _source.Cancel();
    }
}
