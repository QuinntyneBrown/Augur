using System.CommandLine;
using Augur.Core;
using Augur.Core.Catalog;
using Augur.Core.Locking;

namespace Augur.Cli.Commands;

internal sealed class ExplainCommand : Command
{
    public ExplainCommand(CliContext context)
        : base("explain", "Show how each decision in a lockfile was reached.")
    {
        Options.Add(Lock);
        Options.Add(Json);
        SetAction((parseResult, token) => context.RunAsync(parseResult, token, run =>
        {
            var catalog = DecisionCatalog.BuiltIn;
            var lockPath = run.ParseResult.GetRequiredValue(Lock);
            var lockfile = new LockfileStore(run.Paths.Resolve(lockPath), lockPath, catalog).Read()
                ?? throw new UsageException($"lockfile not found: {lockPath}");
            run.WriteStdout(run.ParseResult.GetValue(Json) ? Explanation.RenderJson(lockfile, catalog) : Explanation.RenderText(lockfile, catalog));
            return Task.FromResult(ExitCode.Success);
        }));
    }

    public Option<string> Lock { get; } = CliOptions.Lock();

    public Option<bool> Json { get; } = CliOptions.Json();
}
