using Augur.Core;
using Augur.Core.Catalog;
using Augur.Core.Decisions;
using Augur.Core.Intake;
using Augur.Core.Plan;
using Augur.Oracle.Scripted;

namespace Augur.Cli.Commands;

/// <summary>The resolved decisions of one run, with the input they were resolved for.</summary>
internal sealed record DecisionRunResult(SpecificationInput Input, SolutionName Name, DecisionState State, GenerationPlan Plan, int ApiRequests);

/// <summary>Reads the inputs named by <see cref="DecisionOptions"/>, evaluates the decision tree, and builds the plan.</summary>
internal static class DecisionRunner
{
    public static async Task<DecisionRunResult> RunAsync(CommandRun run, DecisionOptions options)
    {
        var catalog = DecisionCatalog.BuiltIn;
        var args = run.ParseResult;
        var name = SolutionName.Parse(args.GetRequiredValue(options.Name));
        var overrides = OverrideSet.Parse(args.GetValue(options.Set) ?? [], catalog);
        var input = new SpecificationReader(run.Paths, run.Host.Stdin)
            .Read(args.GetRequiredValue(options.Spec), args.GetValue(options.Image) ?? []);

        var oracle = CreateOracle(run, options);
        var resolver = new DecisionResolver(new ResolverOptions(args.GetValue(options.MinConfidence)));
        var evaluator = new TreeEvaluator(catalog, oracle, resolver, CreateLowConfidenceHandler(run, options), TimeProvider.System, run.Reporter);
        var state = await evaluator.EvaluateAsync(input, overrides, run.Token);

        return new DecisionRunResult(
            input,
            name,
            state,
            GenerationPlan.From(state, name, input.InputHash, catalog),
            (oracle as IApiRequestCounter)?.ApiRequests ?? 0);
    }

    private static IDecisionOracle CreateOracle(CommandRun run, DecisionOptions options)
    {
        if (run.ParseResult.GetValue(options.OracleScript) is { } script)
        {
            return ScriptedOracle.Load(script, run.Paths.Resolve(script));
        }

        return new NoOracle();
    }

    private static ILowConfidenceHandler CreateLowConfidenceHandler(CommandRun run, DecisionOptions options)
    {
        LowConfidencePolicy? option = run.ParseResult.GetValue(options.OnLowConfidence) switch
        {
            "default" => LowConfidencePolicy.Default,
            "prompt" => LowConfidencePolicy.Prompt,
            "fail" => LowConfidencePolicy.Fail,
            _ => null,
        };

        return LowConfidencePolicySelector.Select(option, run.Host.StdinIsTerminal, run.Host.StderrIsTerminal) switch
        {
            LowConfidencePolicy.Default => new DefaultFallbackHandler(run.Reporter),
            LowConfidencePolicy.Fail => new FailHandler(run.Reporter),
            _ => new InteractivePromptHandler(run.StdinReader, run.Host.Stderr),
        };
    }
}
