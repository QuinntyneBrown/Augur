namespace Augur.Core;

/// <summary>
/// The one place a path the user typed becomes a file-system path. Relative paths resolve against the
/// working directory of the invocation, never against the process's current directory.
/// </summary>
public sealed class PathResolver(string workingDirectory)
{
    public string WorkingDirectory { get; } = workingDirectory;

    /// <exception cref="UsageException">The path is empty or contains a NUL character.</exception>
    public string Resolve(string userPath)
    {
        if (string.IsNullOrWhiteSpace(userPath) || userPath.Contains('\0', StringComparison.Ordinal))
        {
            throw new UsageException($"'{userPath.Replace("\0", "\\0", StringComparison.Ordinal)}' is not a valid path");
        }

        return Path.GetFullPath(userPath, WorkingDirectory);
    }
}
