using System.CommandLine;
using Augur.Core;
using Augur.Core.Catalog;
using Augur.Core.Plan;

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
        var planPath = run.ParseResult.GetRequiredValue(Plan);
        _ = PlanReader.Read(planPath, run.Paths.Resolve(planPath), DecisionCatalog.BuiltIn);
        throw new NotImplementedException("'emit' is not implemented yet");
    }
}
