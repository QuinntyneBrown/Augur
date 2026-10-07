using System.Diagnostics;
using System.Text;
using Augur.Tests.Support;

namespace Augur.IntegrationTests.Support;

/// <summary>Emits many plans side by side and builds and tests them together, so one build covers the whole set.</summary>
public sealed class EmittedBuild : IDisposable
{
    public EmittedBuild(string prefix)
    {
        Root = Path.Combine(Path.GetTempPath(), "augur-builds", $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    /// <summary>Emits each plan into its own directory named after its solution, such as <c>Matrix.P01</c>.</summary>
    public async Task<IReadOnlyList<string>> EmitAsync(string prefix, IReadOnlyList<Dictionary<string, object>> plans)
    {
        var names = new List<string>();
        for (var i = 0; i < plans.Count; i++)
        {
            var name = $"{prefix}.P{i + 1:00}";
            using var cli = new CliRunner();
            cli.WriteFile("plan.json", Plans.Json(name, plans[i]));
            var result = await cli.RunAsync("emit", "--plan", "plan.json", "--out", Path.Combine(Root, name));
            Assert.True(result.ExitCode == 0, $"{name} ({Plans.Describe(plans[i])}) failed to emit: {result.Stderr}");
            File.WriteAllText(Path.Combine(Root, name, "plan.txt"), Plans.Describe(plans[i]));
            names.Add(name);
        }

        return names;
    }

    /// <summary>Writes one solution that includes every emitted .NET project.</summary>
    public string WriteAggregateSolution(string name)
    {
        var projects = Directory.EnumerateFiles(Root, "*.csproj", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(Root, p).Replace('\\', '/'))
            .Order(StringComparer.Ordinal);
        var solution = new StringBuilder("<Solution>\n");
        foreach (var project in projects)
        {
            solution.Append("  <Project Path=\"").Append(project).Append("\" />\n");
        }

        solution.Append("</Solution>\n");
        var path = Path.Combine(Root, $"{name}.slnx");
        File.WriteAllText(path, solution.ToString());
        return path;
    }

    public static async Task<ProcessResult> RunAsync(string fileName, string arguments, string workingDirectory, TimeSpan timeout, IDictionary<string, string?>? environment = null)
    {
        var start = new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["DOTNET_NOLOGO"] = "1";
        foreach (var (key, value) in environment ?? new Dictionary<string, string?>())
        {
            start.Environment[key] = value;
        }

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var cancellation = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"{fileName} {arguments} did not finish within {timeout}");
        }

        return new ProcessResult(process.ExitCode, await stdout, await stderr);
    }

    public void Dispose()
    {
        if (Environment.GetEnvironmentVariable("AUGUR_KEEP_BUILDS") == "1")
        {
            return;
        }

        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}

public sealed record ProcessResult(int ExitCode, string Stdout, string Stderr)
{
    public string Output => Stdout + Stderr;

    /// <summary>The distinct error and warning lines of an MSBuild run.</summary>
    public string Problems => string.Join('\n', Output.Split('\n')
        .Where(l => l.Contains(": error ", StringComparison.Ordinal) || l.Contains(": warning ", StringComparison.Ordinal))
        .Select(l => l.Trim())
        .Distinct()
        .Take(50));
}
