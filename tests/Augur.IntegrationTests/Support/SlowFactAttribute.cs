namespace Augur.IntegrationTests.Support;

/// <summary>
/// A test that builds emitted code, starts processes, or drives a browser. It runs only when <c>AUGUR_RUN_SLOW=1</c>,
/// so the default <c>dotnet test</c> stays fast.
/// </summary>
public sealed class SlowFactAttribute : FactAttribute
{
    public const string Variable = "AUGUR_RUN_SLOW";

    public SlowFactAttribute()
    {
        if (!Enabled)
        {
            Skip = $"slow test: set {Variable}=1 to run it";
        }
    }

    public static bool Enabled => Environment.GetEnvironmentVariable(Variable) == "1";
}
