using System.CommandLine;
using Augur.Core;
using Augur.Core.Catalog;

namespace Augur.Cli.Commands;

internal sealed class PlanCommand : Command
{
    public PlanCommand(CliContext context)
        : base("plan", "Resolve decisions for a specification and write a GenerationPlan.")
    {
        Decisions.AddTo(this);
        Options.Add(Out);
        SetAction((parseResult, token) => context.RunAsync(parseResult, token, RunAsync));
    }

    public DecisionOptions Decisions { get; } = new();

    public Option<string> Out { get; } = CliOptions.OutFile();

    private async Task<ExitCode> RunAsync(CommandRun run)
    {
        var result = await DecisionRunner.RunAsync(run, Decisions);
        var json = result.Plan.ToJson(DecisionCatalog.BuiltIn);
        if (run.ParseResult.GetValue(Out) is { } outPath)
        {
            AtomicFile.WriteAllText(run.Paths.Resolve(outPath), json);
        }
        else
        {
            run.WriteStdout(json);
        }

        run.Reporter.Info(RunSummary.Render(result.State, result.ApiRequests, run.Clock.Elapsed));
        return ExitCode.Success;
    }
}
