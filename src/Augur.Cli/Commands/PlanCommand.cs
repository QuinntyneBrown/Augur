using System.CommandLine;

namespace Augur.Cli.Commands;

internal sealed class PlanCommand : Command
{
    public PlanCommand(CliContext context)
        : base("plan", "Resolve decisions for a specification and write a GenerationPlan.")
    {
        Decisions.AddTo(this);
        Options.Add(Out);
        SetAction((_, _) => context.NotImplemented(Name));
    }

    public DecisionOptions Decisions { get; } = new();

    public Option<string> Out { get; } = CliOptions.OutFile();
}
