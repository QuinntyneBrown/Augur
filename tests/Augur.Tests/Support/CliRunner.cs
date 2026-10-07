using System.Text;
using Augur.Cli;

namespace Augur.Tests.Support;

/// <summary>Runs the augur CLI in-process the way a user would, inside an isolated working directory.</summary>
public sealed class CliRunner : IDisposable
{
    private readonly Dictionary<string, string?> _env = new(StringComparer.Ordinal);

    public CliRunner()
    {
        WorkingDirectory = Path.Combine(Path.GetTempPath(), "augur-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(WorkingDirectory);
    }

    public string WorkingDirectory { get; }

    public bool StdinIsTerminal { get; set; }

    public bool StderrIsTerminal { get; set; }

    /// <summary>Wraps the emitter for fault injection.</summary>
    public Func<Augur.Emission.IEmitter, Augur.Emission.IEmitter>? EmitterDecorator { get; set; }

    public CliRunner WithEnv(string name, string? value)
    {
        _env[name] = value;
        return this;
    }

    public string PathOf(string relative) => Path.Combine(WorkingDirectory, relative);

    public string WriteFile(string relative, string content)
    {
        var path = PathOf(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(false));
        return path;
    }

    public string WriteBytes(string relative, byte[] content)
    {
        var path = PathOf(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
        return path;
    }

    public string ReadFile(string relative) => File.ReadAllText(PathOf(relative));

    public bool Exists(string relative) => File.Exists(PathOf(relative)) || Directory.Exists(PathOf(relative));

    public Task<CliResult> RunAsync(params string[] args) => RunAsync(args, stdin: null, CancellationToken.None);

    public Task<CliResult> RunWithStdinAsync(string stdin, params string[] args) =>
        RunAsync(args, Encoding.UTF8.GetBytes(stdin), CancellationToken.None);

    public async Task<CliResult> RunAsync(string[] args, byte[]? stdin, CancellationToken token)
    {
        var stdout = new StringWriter { NewLine = "\n" };
        var stderr = new StringWriter { NewLine = "\n" };
        var host = new ConsoleHost(
            Args: args,
            Stdin: new MemoryStream(stdin ?? []),
            Stdout: stdout,
            Stderr: stderr,
            Env: new Dictionary<string, string?>(_env, StringComparer.Ordinal),
            Cwd: WorkingDirectory,
            StdinIsTerminal: StdinIsTerminal,
            StderrIsTerminal: StderrIsTerminal,
            Token: token)
        {
            EmitterDecorator = EmitterDecorator,
        };
        var exitCode = await Program.RunAsync(host);
        return new CliResult(exitCode, stdout.ToString(), stderr.ToString());
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(WorkingDirectory, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

public sealed record CliResult(int ExitCode, string Stdout, string Stderr);
