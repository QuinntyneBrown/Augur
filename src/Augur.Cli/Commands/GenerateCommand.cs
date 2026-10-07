using System.CommandLine;

namespace Augur.Cli.Commands;

internal sealed class GenerateCommand : Command
{
    public GenerateCommand(CliContext context)
        : base("generate", "Resolve decisions, write the plan, and emit code in one step.")
    {
        Decisions.AddTo(this);
        Options.Add(Out);
        Options.Add(Force);
        Options.Add(DryRun);
        SetAction((_, _) => context.NotImplemented(Name));
    }

    public DecisionOptions Decisions { get; } = new();

    public Option<string> Out { get; } = CliOptions.OutDirectory();

    public Option<bool> Force { get; } = CliOptions.Force();

    public Option<bool> DryRun { get; } = CliOptions.DryRun();
}
