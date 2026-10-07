using System.CommandLine;

namespace Augur.Cli.Commands;

internal sealed class ExplainCommand : Command
{
    public ExplainCommand(CliContext context)
        : base("explain", "Show how each decision in a lockfile was reached.")
    {
        Options.Add(Lock);
        Options.Add(Json);
        SetAction((_, _) => context.NotImplemented(Name));
    }

    public Option<string> Lock { get; } = CliOptions.Lock();

    public Option<bool> Json { get; } = CliOptions.Json();
}
