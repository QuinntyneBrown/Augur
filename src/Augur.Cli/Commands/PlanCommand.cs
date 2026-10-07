using System.CommandLine;
using Augur.Core;
using Augur.Core.Intake;

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

    private Task<ExitCode> RunAsync(CommandRun run)
    {
        var reader = new SpecificationReader(run.Paths, run.Host.Stdin);
        _ = reader.Read(run.ParseResult.GetRequiredValue(Decisions.Spec), run.ParseResult.GetValue(Decisions.Image) ?? []);
        throw new NotImplementedException("'plan' is not implemented yet");
    }
}
