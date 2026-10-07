using System.Text;

namespace Augur.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        await using var stdout = new StreamWriter(Console.OpenStandardOutput(), utf8) { NewLine = "\n", AutoFlush = true };
        await using var stderr = new StreamWriter(Console.OpenStandardError(), utf8) { NewLine = "\n", AutoFlush = true };
        var env = Environment.GetEnvironmentVariables()
            .Cast<System.Collections.DictionaryEntry>()
            .ToDictionary(e => (string)e.Key, e => (string?)e.Value, StringComparer.Ordinal);
        var host = new ConsoleHost(
            args,
            Console.OpenStandardInput(),
            stdout,
            stderr,
            env,
            Environment.CurrentDirectory,
            StdinIsTerminal: !Console.IsInputRedirected,
            StderrIsTerminal: !Console.IsErrorRedirected,
            CancellationToken.None);
        return await RunAsync(host);
    }

    /// <summary>Runs one augur invocation against <paramref name="host"/> and returns the process exit code.</summary>
    public static Task<int> RunAsync(ConsoleHost host) => CommandLine.RunAsync(host);
}
