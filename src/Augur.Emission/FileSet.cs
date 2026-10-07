using System.Text;
using Augur.Core.Plan;

namespace Augur.Emission;

/// <summary>
/// The files an emitter produces, keyed by relative path with <c>/</c> separators. Text is normalized to UTF-8
/// without a BOM and LF line endings, so the same plan always yields the same bytes.
/// </summary>
public sealed class FileSet
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);
    private readonly SortedDictionary<string, byte[]> _files = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, byte[]> Files => _files;

    public int Count => _files.Count;

    public IEnumerable<string> Paths => _files.Keys;

    /// <summary>Adds a text file; CRLF and CR become LF.</summary>
    public FileSet Add(string path, string text) =>
        AddBinary(path, Utf8.GetBytes(text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n')));

    /// <summary>Adds a file whose bytes are written exactly as given.</summary>
    public FileSet AddBinary(string path, byte[] content)
    {
        CheckPath(path);
        if (!_files.TryAdd(path, content))
        {
            throw new InvalidOperationException($"two emitters produced {path}");
        }

        return this;
    }

    /// <summary>Adds every file of <paramref name="other"/>; a path produced twice is a defect.</summary>
    public FileSet Merge(FileSet other)
    {
        foreach (var (path, content) in other._files)
        {
            AddBinary(path, content);
        }

        return this;
    }

    private static void CheckPath(string path)
    {
        if (string.IsNullOrEmpty(path)
            || Path.IsPathRooted(path)
            || path.Contains('\\', StringComparison.Ordinal)
            || path.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            throw new ArgumentException($"'{path}' is not a relative path with / separators", nameof(path));
        }
    }
}

/// <summary>
/// The only inputs an emitter may use: validated plan values and names derived from the solution name.
/// Nothing from the specification or from model text ever reaches an emitter.
/// </summary>
public sealed record EmissionContext(GenerationPlan Plan, SolutionName SolutionName, string AngularProjectName)
{
    public static EmissionContext For(GenerationPlan plan) =>
        new(plan, plan.SolutionName, AngularNames.ProjectName(plan.SolutionName));

    public string? Value(string decisionId) => Plan.ValueOf(decisionId);

    public bool IsTrue(string decisionId) => Plan.ValueOf(decisionId) == Core.Catalog.DecisionDefinition.True;

    public string Target => Plan.ValueOf("target") ?? throw new InvalidOperationException("the plan has no target");
}

/// <summary>Turns a plan into files. Implementations must be deterministic and must not touch the file system, network, or processes.</summary>
public interface IEmitter
{
    FileSet Emit(EmissionContext context);
}

/// <summary>Runs several emitters and merges their files.</summary>
public sealed class CompositeEmitter(IReadOnlyList<IEmitter> emitters) : IEmitter
{
    public FileSet Emit(EmissionContext context)
    {
        var files = new FileSet();
        foreach (var emitter in emitters)
        {
            files.Merge(emitter.Emit(context));
        }

        return files;
    }
}
