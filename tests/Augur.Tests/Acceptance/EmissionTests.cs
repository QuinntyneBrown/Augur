using System.Diagnostics;
using System.Text;
using Augur.Emission;
using Augur.Tests.Support;

namespace Augur.Tests.Acceptance;

public sealed class EmissionTests
{
    [Fact]
    public async Task Emit_creates_the_output_directory_and_reports_the_files_written()
    {
        using var cli = await PlannedCli();

        var result = await cli.RunAsync("emit", "--plan", "plan.json", "--out", "out");

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Stdout);
        Assert.True(cli.Exists("out/README.md"));
        Assert.Matches(@"files written \d+, elapsed \d+\.\d\ds\n$", result.Stderr);
    }

    [Fact]
    public async Task Emitting_one_plan_twice_gives_byte_identical_files_without_bom_or_cr()
    {
        using var cli = await PlannedCli();

        await cli.RunAsync("emit", "--plan", "plan.json", "--out", "one");
        await cli.RunAsync("emit", "--plan", "plan.json", "--out", "two");

        var one = Snapshot(cli.PathOf("one"));
        var two = Snapshot(cli.PathOf("two"));
        Assert.Equal(one.Keys, two.Keys);
        foreach (var (path, bytes) in one)
        {
            Assert.Equal(bytes, two[path]);
            Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble), $"{path} starts with a BOM");
            Assert.DoesNotContain((byte)'\r', bytes);
        }
    }

    [Fact]
    public async Task A_non_empty_output_directory_is_refused_without_force()
    {
        using var cli = await PlannedCli();
        cli.WriteFile("out/notes.txt", "mine");

        var result = await cli.RunAsync("emit", "--plan", "plan.json", "--out", "out");

        Assert.Equal(5, result.ExitCode);
        Assert.Equal("error: out is not empty; use --force to overwrite the files Augur emits\n", result.Stderr);
        Assert.Equal(["notes.txt"], Snapshot(cli.PathOf("out")).Keys);
    }

    [Fact]
    public async Task Force_replaces_emitted_files_and_leaves_other_files_alone()
    {
        using var cli = await PlannedCli();
        cli.WriteFile("out/notes.txt", "mine");
        cli.WriteFile("out/README.md", "an older emitted readme");

        var result = await cli.RunAsync("emit", "--plan", "plan.json", "--out", "out", "--force");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("mine", cli.ReadFile("out/notes.txt"));
        Assert.NotEqual("an older emitted readme", cli.ReadFile("out/README.md"));
    }

    [Fact]
    public async Task Dry_run_lists_every_file_sorted_and_writes_nothing()
    {
        using var cli = await PlannedCli();
        await cli.RunAsync("emit", "--plan", "plan.json", "--out", "real");

        var result = await cli.RunAsync("emit", "--plan", "plan.json", "--out", "out", "--dry-run");

        Assert.Equal(0, result.ExitCode);
        Assert.False(cli.Exists("out"));
        var listed = result.Stdout.TrimEnd('\n').Split('\n');
        Assert.Equal(Snapshot(cli.PathOf("real")).Keys.Order(StringComparer.Ordinal), listed);
    }

    [Fact]
    public async Task Dry_run_marks_files_that_already_exist()
    {
        using var cli = await PlannedCli();
        cli.WriteFile("out/README.md", "old");

        var result = await cli.RunAsync("emit", "--plan", "plan.json", "--out", "out", "--dry-run");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("README.md (overwrite)\n", result.Stdout);
        Assert.Equal("old", cli.ReadFile("out/README.md"));
    }

    [Fact]
    public async Task A_rendering_fault_leaves_the_output_directory_unchanged_and_no_staging_directory()
    {
        using var cli = await PlannedCli();
        cli.WriteFile("out/notes.txt", "mine");
        var before = Directory.GetFileSystemEntries(cli.WorkingDirectory).Order().ToList();
        cli.EmitterDecorator = inner => new FaultyEmitter(inner);

        var result = await cli.RunAsync("emit", "--plan", "plan.json", "--out", "out", "--force");

        Assert.Equal(1, result.ExitCode);
        Assert.Equal("error: injected rendering fault\n", result.Stderr);
        Assert.Equal(["notes.txt"], Snapshot(cli.PathOf("out")).Keys);
        Assert.Equal(before, Directory.GetFileSystemEntries(cli.WorkingDirectory).Order().ToList());
    }

    [Fact]
    public async Task A_failure_while_moving_files_into_place_removes_the_staging_directory()
    {
        using var cli = await PlannedCli();
        cli.EmitterDecorator = inner => new ExtraFileEmitter(inner, "README.md/inside.txt");

        var result = await cli.RunAsync("emit", "--plan", "plan.json", "--out", "out");

        Assert.NotEqual(0, result.ExitCode);
        Assert.DoesNotContain(Directory.GetDirectories(cli.WorkingDirectory), d => Path.GetFileName(d).Contains("staging", StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_internal_error_shows_the_stack_trace_only_at_diagnostic_verbosity()
    {
        using var cli = await PlannedCli();
        cli.EmitterDecorator = inner => new FaultyEmitter(inner);

        var normal = await cli.RunAsync("emit", "--plan", "plan.json", "--out", "a");
        var diagnostic = await cli.RunAsync("emit", "--plan", "plan.json", "--out", "b", "--verbosity", "diagnostic");

        Assert.Equal(1, normal.ExitCode);
        Assert.DoesNotContain(" at ", normal.Stderr);
        Assert.Contains("error: injected rendering fault\n", diagnostic.Stderr);
        Assert.Contains(" at ", diagnostic.Stderr);
    }

    [Fact]
    public async Task Writing_through_a_link_that_leaves_the_output_directory_is_refused()
    {
        using var cli = await PlannedCli();
        Directory.CreateDirectory(cli.PathOf("outside"));
        Directory.CreateDirectory(cli.PathOf("out"));
        if (!TryLink(cli.PathOf("out/src"), cli.PathOf("outside")))
        {
            return;
        }

        cli.EmitterDecorator = inner => new ExtraFileEmitter(inner, "src/Program.cs");

        var result = await cli.RunAsync("emit", "--plan", "plan.json", "--out", "out", "--force");

        Assert.Equal(5, result.ExitCode);
        Assert.Equal("error: src is a symbolic link or junction; Augur does not write through links in the output directory\n", result.Stderr);
        Assert.Empty(Directory.GetFileSystemEntries(cli.PathOf("outside")));
        Assert.False(cli.Exists("out/README.md"));
    }

    [Fact]
    public async Task Every_written_file_is_inside_the_output_directory()
    {
        using var cli = await PlannedCli();

        await cli.RunAsync("emit", "--plan", "plan.json", "--out", "out");

        var root = Path.GetFullPath(cli.PathOf("out")) + Path.DirectorySeparatorChar;
        var outside = Directory.EnumerateFiles(cli.WorkingDirectory, "*", SearchOption.AllDirectories)
            .Where(f => !Path.GetFullPath(f).StartsWith(root, StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal);
        Assert.Equal(["answers.json", "decisions.json", "plan.json", "spec.md"], outside);
    }

    internal static async Task<CliRunner> PlannedCli(string target = "dotnet")
    {
        var cli = new CliRunner();
        cli.WriteFile("spec.md", "Build an order tracking app.\n");
        var script = AnswerScript.FullstackCleanArchitecture().Choice("target", target).WriteTo(cli);
        var result = await cli.RunAsync("plan", "--spec", "spec.md", "--name", "Contoso.Orders", "--oracle-script", script, "--out", "plan.json");
        Assert.Equal(0, result.ExitCode);
        return cli;
    }

    internal static SortedDictionary<string, byte[]> Snapshot(string directory) =>
        new(Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .ToDictionary(f => Path.GetRelativePath(directory, f).Replace('\\', '/'), File.ReadAllBytes), StringComparer.Ordinal);

    private static bool TryLink(string link, string target)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var mklink = Process.Start(new ProcessStartInfo("cmd.exe", ["/c", "mklink", "/J", link, target]) { RedirectStandardOutput = true, UseShellExecute = false })!;
                mklink.WaitForExit();
                return mklink.ExitCode == 0;
            }

            Directory.CreateSymbolicLink(link, target);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private sealed class FaultyEmitter(IEmitter inner) : IEmitter
    {
        public FileSet Emit(EmissionContext context)
        {
            inner.Emit(context);
            throw new InvalidOperationException("injected rendering fault");
        }
    }

    private sealed class ExtraFileEmitter(IEmitter inner, string extraPath) : IEmitter
    {
        public FileSet Emit(EmissionContext context)
        {
            var files = inner.Emit(context);
            files.Add(extraPath, "extra\n");
            return files;
        }
    }
}
