using System.Diagnostics;

namespace Augur.IntegrationTests.Support;

/// <summary>Packs augur from source and installs it into a private tool path, so tests run exactly what users install.</summary>
public sealed class InstalledTool : IDisposable
{
    private InstalledTool(string root)
    {
        Root = root;
    }

    public string Root { get; }

    public string ToolPath => Path.Combine(Root, "tools");

    public string Executable => Path.Combine(ToolPath, OperatingSystem.IsWindows() ? "augur.exe" : "augur");

    public static async Task<InstalledTool> InstallAsync()
    {
        var tool = new InstalledTool(Path.Combine(Path.GetTempPath(), "augur-tool", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(tool.Root);
        var feed = Path.Combine(tool.Root, "feed");
        var project = Path.Combine(RepositoryRoot(), "src", "Augur.Cli", "Augur.Cli.csproj");

        var pack = await EmittedBuild.RunAsync("dotnet", $"pack \"{project}\" -c Release -o \"{feed}\"", tool.Root, TimeSpan.FromMinutes(10));
        Assert.True(pack.ExitCode == 0, pack.Output);
        var install = await EmittedBuild.RunAsync(
            "dotnet", $"tool install Augur.Cli --tool-path \"{tool.ToolPath}\" --add-source \"{feed}\"", tool.Root, TimeSpan.FromMinutes(5));
        Assert.True(install.ExitCode == 0, install.Output);
        return tool;
    }

    public Task<ProcessResult> RunAsync(string workingDirectory, string arguments, Dictionary<string, string?>? environment = null) =>
        EmittedBuild.RunAsync(Executable, arguments, workingDirectory, TimeSpan.FromMinutes(5), environment);

    /// <summary>Runs augur and samples its peak working set until it exits.</summary>
    public async Task<(ProcessResult Result, long PeakWorkingSet)> RunMeasuredAsync(string workingDirectory, string arguments, Dictionary<string, string?>? environment = null)
    {
        using var process = Start(workingDirectory, arguments, environment);
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        long peak = 0;
        while (!process.HasExited)
        {
            try
            {
                process.Refresh();
                peak = Math.Max(peak, process.PeakWorkingSet64);
            }
            catch (InvalidOperationException)
            {
                break;
            }

            await Task.Delay(20);
        }

        await process.WaitForExitAsync();
        return (new ProcessResult(process.ExitCode, await stdout, await stderr), peak);
    }

    public Process Start(string workingDirectory, string arguments, Dictionary<string, string?>? environment = null)
    {
        var start = new ProcessStartInfo(Executable, arguments)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var (key, value) in environment ?? [])
        {
            start.Environment[key] = value;
        }

        return Process.Start(start)!;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static string RepositoryRoot()
    {
        var directory = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(directory, "Augur.sln")))
        {
            directory = Path.GetDirectoryName(directory) ?? throw new InvalidOperationException("Augur.sln not found above the test assembly");
        }

        return directory;
    }
}
