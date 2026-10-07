using System.Runtime.CompilerServices;
using System.Text;

namespace Augur.Tests.Support;

/// <summary>
/// Compares emitted files with committed copies under <c>tests/Augur.Tests/Golden</c>. After an intended template
/// change, run the tests with <c>AUGUR_UPDATE_GOLDEN=1</c> to rewrite the copies, and review the diff.
/// </summary>
public static class Golden
{
    public static void Compare(string name, IReadOnlyDictionary<string, byte[]> actual, [CallerFilePath] string caller = "")
    {
        var root = Path.Combine(GoldenRoot(caller), name.Replace('/', Path.DirectorySeparatorChar));
        if (Environment.GetEnvironmentVariable("AUGUR_UPDATE_GOLDEN") == "1")
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }

            foreach (var (path, content) in actual)
            {
                var target = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllBytes(target, content);
            }

            return;
        }

        Assert.True(Directory.Exists(root), $"no golden copy at {root}; run the tests with AUGUR_UPDATE_GOLDEN=1 to create it");
        var expected = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(f => Path.GetRelativePath(root, f).Replace('\\', '/'), File.ReadAllBytes, StringComparer.Ordinal);
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), actual.Keys.Order(StringComparer.Ordinal));
        foreach (var (path, content) in actual)
        {
            Assert.True(
                expected[path].AsSpan().SequenceEqual(content),
                $"{name}/{path} differs from its golden copy:\n{Encoding.UTF8.GetString(content)}");
        }
    }

    private static string GoldenRoot(string caller)
    {
        var directory = Path.GetDirectoryName(caller)!;
        while (!File.Exists(Path.Combine(directory, "Augur.Tests.csproj")))
        {
            directory = Path.GetDirectoryName(directory)!;
        }

        return Path.Combine(directory, "Golden");
    }
}
