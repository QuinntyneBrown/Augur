using System.CommandLine;
using Augur.Core;
using Augur.Core.Catalog;
using Augur.Core.Plan;
using Augur.Emission;

namespace Augur.Cli.Commands;

internal sealed class EmitCommand : Command
{
    public EmitCommand(CliContext context)
        : base("emit", "Emit code from an existing GenerationPlan.")
    {
        Options.Add(Plan);
        Options.Add(Out);
        Options.Add(Force);
        Options.Add(DryRun);
        SetAction((parseResult, token) => context.RunAsync(parseResult, token, RunAsync));
    }

    public Option<string> Plan { get; } = CliOptions.Plan();

    public Option<string> Out { get; } = CliOptions.OutDirectory();

    public Option<bool> Force { get; } = CliOptions.Force();

    public Option<bool> DryRun { get; } = CliOptions.DryRun();

    private Task<ExitCode> RunAsync(CommandRun run)
    {
        var args = run.ParseResult;
        var planPath = args.GetRequiredValue(Plan);
        var plan = PlanReader.Read(planPath, run.Paths.Resolve(planPath), DecisionCatalog.BuiltIn);
        var outPath = args.GetRequiredValue(Out);
        run.Token.ThrowIfCancellationRequested();

        var result = new EmissionPipeline(Emitters.Create(run.Host))
            .Emit(plan, run.Paths.Resolve(outPath), outPath, args.GetValue(Force), args.GetValue(DryRun));
        if (args.GetValue(DryRun))
        {
            run.WriteStdout(string.Concat(result.DryRunListing.Select(line => line + "\n")));
        }
        else
        {
            run.Reporter.Info(RunSummary.Render(null, 0, run.Clock.Elapsed, result.FilesWritten));
        }

        return Task.FromResult(ExitCode.Success);
    }
}
