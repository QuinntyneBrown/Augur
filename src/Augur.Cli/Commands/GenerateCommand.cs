using System.CommandLine;
using Augur.Core;
using Augur.Core.Catalog;
using Augur.Core.Plan;
using Augur.Emission;

namespace Augur.Cli.Commands;

internal sealed class GenerateCommand : Command
{
    public const string PlanFileName = "augur.plan.json";

    public GenerateCommand(CliContext context)
        : base("generate", "Resolve decisions, write the plan, and emit code in one step.")
    {
        Decisions.AddTo(this);
        Options.Add(Out);
        Options.Add(Force);
        Options.Add(DryRun);
        SetAction((parseResult, token) => context.RunAsync(parseResult, token, RunAsync));
    }

    public DecisionOptions Decisions { get; } = new();

    public Option<string> Out { get; } = CliOptions.OutDirectory();

    public Option<bool> Force { get; } = CliOptions.Force();

    public Option<bool> DryRun { get; } = CliOptions.DryRun();

    private async Task<ExitCode> RunAsync(CommandRun run)
    {
        var args = run.ParseResult;
        var outPath = args.GetRequiredValue(Out);
        var outRoot = run.Paths.Resolve(outPath);
        var force = args.GetValue(Force);
        var dryRun = args.GetValue(DryRun);
        if (!force && !dryRun && Directory.Exists(outRoot) && Directory.EnumerateFileSystemEntries(outRoot).Any())
        {
            throw new OutputConflictException($"{outPath} is not empty; use --force to overwrite the files Augur emits");
        }

        var result = await DecisionRunner.RunAsync(run, Decisions, writeLockfile: !dryRun);
        var catalog = DecisionCatalog.BuiltIn;
        var planJson = result.Plan.ToJson(catalog);
        var plan = PlanReader.Validate(CanonicalJson.Parse(planJson), PlanFileName, catalog);
        run.Token.ThrowIfCancellationRequested();

        var emission = new EmissionPipeline(Emitters.Create(run.Host))
            .Emit(plan, outRoot, outPath, force, dryRun, new FileSet().Add(PlanFileName, planJson));
        if (dryRun)
        {
            run.WriteStdout(string.Concat(emission.DryRunListing.Select(line => line + "\n")));
        }
        else
        {
            run.Reporter.Info(RunSummary.Render(result.State, result.ApiRequests, run.Clock.Elapsed, emission.FilesWritten));
        }

        return ExitCode.Success;
    }
}
