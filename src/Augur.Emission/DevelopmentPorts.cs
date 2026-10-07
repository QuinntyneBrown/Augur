using Augur.Core.Plan;

namespace Augur.Emission;

/// <summary>
/// Development ports for an emitted API, derived from the solution name so that the API's launch profile and the
/// Angular proxy agree, and so that two generated apps rarely collide. The same name always gives the same ports.
/// </summary>
public static class DevelopmentPorts
{
    public static int Https(SolutionName name) => 7000 + (StableHash(name.Value) % 1000);

    public static int Http(SolutionName name) => 5000 + (StableHash(name.Value) % 1000);

    /// <summary>A hash that is the same on every machine and run, unlike <see cref="string.GetHashCode()"/>.</summary>
    private static int StableHash(string text)
    {
        var hash = 17;
        foreach (var c in text)
        {
            hash = unchecked((hash * 31) + c);
        }

        return Math.Abs(hash % 100_000);
    }
}
