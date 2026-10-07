using System.CommandLine;

namespace Augur.Cli.Commands;

internal sealed class SchemaCommand : Command
{
    public SchemaCommand(CliContext context)
        : base("schema", "Print JSON Schemas for Augur's file formats.")
    {
        var plan = new Command("plan", "Print the JSON Schema for GenerationPlan.");
        plan.SetAction((_, _) => context.NotImplemented("schema plan"));
        Subcommands.Add(plan);
        SetAction(_ =>
        {
            context.WriteHelp(["schema"], context.Host.Stderr);
            return 2;
        });
    }
}
