using Augur.Core;
using Augur.Core.Plan;

namespace Augur.Emission;

/// <summary>Writing through a link would put files outside the output directory (exit code 5).</summary>
public sealed class ContainmentException(string message) : OutputConflictException(message);

/// <summary>What emission did.</summary>
public sealed record EmissionResult(int FilesWritten, IReadOnlyList<string> DryRunListing);

/// <summary>
/// Renders the plan in memory, checks every target path, stages the files beside the output directory, and only then
/// moves them into place. A failure before the move leaves the output directory exactly as it was.
/// </summary>
public sealed class EmissionPipeline(IEmitter emitter)
{
    /// <exception cref="OutputConflictException">The output directory is not empty without <c>force</c>, or a write failed.</exception>
    /// <exception cref="ContainmentException">A target path would pass through a link.</exception>
    public EmissionResult Emit(GenerationPlan plan, string outputRoot, string outputDisplayName, bool force, bool dryRun, FileSet? extraFiles = null)
    {
        var files = emitter.Emit(EmissionContext.For(plan));
        if (extraFiles is not null)
        {
            files.Merge(extraFiles);
        }

        var root = Path.GetFullPath(outputRoot);
        OutputDirectoryGuard.Check(files, root);
        if (dryRun)
        {
            return new EmissionResult(0, [.. files.Paths.Select(p => File.Exists(Target(root, p)) ? $"{p} (overwrite)" : p)]);
        }

        if (!force && Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
        {
            throw new OutputConflictException($"{outputDisplayName} is not empty; use --force to overwrite the files Augur emits");
        }

        using var staging = StagingDirectory.CreateBeside(root);
        try
        {
            staging.Write(files);
            staging.MoveInto(root, files, force);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new OutputConflictException($"could not write to {outputDisplayName}: {ex.Message}", ex);
        }

        return new EmissionResult(files.Count, []);
    }

    internal static string Target(string root, string relativePath) =>
        Path.Combine([root, .. relativePath.Split('/')]);
}

/// <summary>Refuses any target that would leave the output directory or pass through a symbolic link or junction.</summary>
public static class OutputDirectoryGuard
{
    public static void Check(FileSet files, string root)
    {
        var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var checkedPaths = new HashSet<string>(StringComparer.Ordinal);
        if (IsLink(root))
        {
            throw new ContainmentException($"the output directory is a symbolic link or junction; Augur does not write through links");
        }

        foreach (var relative in files.Paths)
        {
            var target = Path.GetFullPath(EmissionPipeline.Target(root, relative));
            if (!target.StartsWith(prefix, comparison))
            {
                throw new ContainmentException($"{relative} would be written outside the output directory");
            }

            var segments = relative.Split('/');
            for (var i = 1; i <= segments.Length; i++)
            {
                var partial = string.Join('/', segments[..i]);
                if (checkedPaths.Add(partial) && IsLink(EmissionPipeline.Target(root, partial)))
                {
                    throw new ContainmentException($"{partial} is a symbolic link or junction; Augur does not write through links in the output directory");
                }
            }
        }
    }

    private static bool IsLink(string path)
    {
        FileSystemInfo info = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
        return info.Exists && (info.LinkTarget is not null || info.Attributes.HasFlag(FileAttributes.ReparsePoint));
    }
}

/// <summary>A sibling of the output directory on the same volume, deleted however emission ends.</summary>
public sealed class StagingDirectory : IDisposable
{
    private readonly string _path;

    private StagingDirectory(string path) => _path = path;

    public static StagingDirectory CreateBeside(string outputRoot)
    {
        var parent = Path.GetDirectoryName(outputRoot) ?? outputRoot;
        Directory.CreateDirectory(parent);
        var path = Path.Combine(parent, $".{Path.GetFileName(outputRoot)}.augur-staging-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return new StagingDirectory(path);
    }

    public void Write(FileSet files)
    {
        foreach (var (relative, content) in files.Files)
        {
            var target = EmissionPipeline.Target(_path, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllBytes(target, content);
        }
    }

    public void MoveInto(string root, FileSet files, bool overwrite)
    {
        foreach (var relative in files.Paths)
        {
            var target = EmissionPipeline.Target(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Move(EmissionPipeline.Target(_path, relative), target, overwrite);
        }
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
        }
    }
}
