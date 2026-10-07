using System.CommandLine;
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

    public static async Task<int> RunAsync(ConsoleHost host)
    {
        var root = new RootCommand("Augur decides what to generate with the OpenAI Decisions API, then emits code deterministically.");
        root.SetAction(_ =>
        {
            WriteHelp(root, [], host.Stderr);
            return 2;
        });

        var parseResult = root.Parse(host.Args, ParserConfiguration);
        return await parseResult.InvokeAsync(Invocation(host.Stdout, host.Stderr), host.Token);
    }

    private static ParserConfiguration ParserConfiguration => new() { ResponseFileTokenReplacer = null };

    private static InvocationConfiguration Invocation(TextWriter output, TextWriter error) => new()
    {
        Output = output,
        Error = error,
        ProcessTerminationTimeout = null,
        EnableDefaultExceptionHandler = false,
    };

    /// <summary>Writes the help for <paramref name="commandPath"/> to <paramref name="writer"/> (the built-in help action only targets stdout).</summary>
    private static void WriteHelp(RootCommand root, string[] commandPath, TextWriter writer) =>
        root.Parse([.. commandPath, "--help"], ParserConfiguration).Invoke(Invocation(writer, writer));
}
