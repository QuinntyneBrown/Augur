using System.CommandLine;

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
        SetAction((_, _) => context.NotImplemented(Name));
    }

    public Option<string> Plan { get; } = CliOptions.Plan();

    public Option<string> Out { get; } = CliOptions.OutDirectory();

    public Option<bool> Force { get; } = CliOptions.Force();

    public Option<bool> DryRun { get; } = CliOptions.DryRun();
}
