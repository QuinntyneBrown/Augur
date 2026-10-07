using System.CommandLine;
using Augur.Core;
using Augur.Core.Catalog;
using Augur.Core.Decisions;
using Augur.Core.Intake;
using Augur.Core.Plan;

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
        var catalog = DecisionCatalog.BuiltIn;
        var name = SolutionName.Parse(run.ParseResult.GetRequiredValue(Decisions.Name));
        var overrides = OverrideSet.Parse(run.ParseResult.GetValue(Decisions.Set) ?? [], catalog);
        var input = new SpecificationReader(run.Paths, run.Host.Stdin)
            .Read(run.ParseResult.GetRequiredValue(Decisions.Spec), run.ParseResult.GetValue(Decisions.Image) ?? []);

        var evaluator = new TreeEvaluator(catalog, new NoOracle(), TimeProvider.System);
        var state = await evaluator.EvaluateAsync(input, overrides, run.Token);

        var json = GenerationPlan.From(state, name, input.InputHash, catalog).ToJson(catalog);
        if (run.ParseResult.GetValue(Out) is { } outPath)
        {
            AtomicFile.WriteAllText(run.Paths.Resolve(outPath), json);
        }
        else
        {
            run.WriteStdout(json);
        }

        return ExitCode.Success;
    }
}
