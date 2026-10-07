using Augur.IntegrationTests.Support;
using Augur.Tests.Support;

namespace Augur.IntegrationTests;

public sealed class DotNetBuildTests
{
    /// <summary>The plans the build check covers: every pair of values, or every combination with <c>AUGUR_FULL_MATRIX=1</c>.</summary>
    public static IReadOnlyList<Dictionary<string, object>> DotNetPlans() =>
        Environment.GetEnvironmentVariable("AUGUR_FULL_MATRIX") == "1"
            ? [.. Plans.AllDotNet()]
            : Pairwise.Cover([.. Plans.AllDotNet()]);

    [Fact]
    public void The_pairwise_dotnet_set_covers_every_pair_of_values()
    {
        var all = Plans.AllDotNet().ToList();
        var chosen = Pairwise.Cover(all);

        Assert.Equal(all.SelectMany(Pairwise.Pairs).ToHashSet(), chosen.SelectMany(Pairwise.Pairs).ToHashSet());
        Assert.True(chosen.Count <= 25, $"{chosen.Count} plans");
    }

    [SlowFact]
    public async Task Emitted_dotnet_solutions_build_without_warnings_and_pass_their_tests()
    {
        using var build = new EmittedBuild("dotnet");
        await build.EmitAsync("Matrix", DotNetPlans());
        var solution = build.WriteAggregateSolution("Matrix");

        var compiled = await EmittedBuild.RunAsync("dotnet", $"build \"{solution}\" -warnaserror", build.Root, TimeSpan.FromMinutes(20));
        Assert.True(compiled.ExitCode == 0 && compiled.Problems.Length == 0, $"build failed in {build.Root}:\n{compiled.Problems}\n{Tail(compiled.Output)}");

        var tested = await EmittedBuild.RunAsync("dotnet", $"test \"{solution}\" --no-build", build.Root, TimeSpan.FromMinutes(20));
        Assert.True(tested.ExitCode == 0, $"tests failed in {build.Root}:\n{Tail(tested.Output)}");
    }

    private static string Tail(string output) => string.Join('\n', output.Split('\n').TakeLast(60));
}
