using System.CommandLine;
using Augur.Core;
using Augur.Core.Catalog;

namespace Augur.Cli.Commands;

internal sealed class CatalogCommand : Command
{
    public CatalogCommand(CliContext context)
        : base("catalog", "List the decisions Augur can make and their allowed answers.")
    {
        Options.Add(Json);
        SetAction((parseResult, token) => context.RunAsync(parseResult, token, run =>
        {
            var catalog = DecisionCatalog.BuiltIn;
            run.WriteStdout(run.ParseResult.GetValue(Json) ? CatalogListing.RenderJson(catalog) : CatalogListing.RenderText(catalog));
            return Task.FromResult(ExitCode.Success);
        }));
    }

    public Option<bool> Json { get; } = CliOptions.Json();
}
