using System.CommandLine;
using System.CommandLine.Invocation;

namespace Augur.Cli;

/// <summary>Replaces the built-in version action, which prints the entry assembly's version.</summary>
internal sealed class VersionAction : SynchronousCommandLineAction
{
    public override int Invoke(ParseResult parseResult)
    {
        parseResult.InvocationConfiguration.Output.WriteLine(VersionInfo.Render());
        return 0;
    }
}
