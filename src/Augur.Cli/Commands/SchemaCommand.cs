using System.CommandLine;
using Augur.Core;
using Augur.Core.Catalog;
using Augur.Core.Plan;

namespace Augur.Cli.Commands;

internal sealed class SchemaCommand : Command
{
    public SchemaCommand(CliContext context)
        : base("schema", "Print JSON Schemas for Augur's file formats.")
    {
        var plan = new Command("plan", "Print the JSON Schema for GenerationPlan.");
        plan.SetAction((parseResult, token) => context.RunAsync(parseResult, token, run =>
        {
            run.WriteStdout(PlanSchema.Generate(DecisionCatalog.BuiltIn));
            return Task.FromResult(ExitCode.Success);
        }));
        Subcommands.Add(plan);
        SetAction(_ =>
        {
            context.WriteHelp(["schema"], context.Host.Stderr);
            return 2;
        });
    }
}
