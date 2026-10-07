using System.CommandLine;

namespace Augur.Cli.Commands;

internal sealed class CatalogCommand : Command
{
    public CatalogCommand(CliContext context)
        : base("catalog", "List the decisions Augur can make and their allowed answers.")
    {
        Options.Add(Json);
        SetAction((_, _) => context.NotImplemented(Name));
    }

    public Option<bool> Json { get; } = CliOptions.Json();
}
