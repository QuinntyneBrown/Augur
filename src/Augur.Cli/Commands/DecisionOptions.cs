using System.CommandLine;

namespace Augur.Cli.Commands;

/// <summary>The options that control how decisions are resolved, shared by <c>plan</c> and <c>generate</c>.</summary>
internal sealed class DecisionOptions
{
    public Option<string> Spec { get; } = CliOptions.Spec();
    public Option<string[]> Image { get; } = CliOptions.Image();
    public Option<string> Name { get; } = CliOptions.Name();
    public Option<string[]> Set { get; } = CliOptions.Set();
    public Option<string> Lock { get; } = CliOptions.Lock();
    public Option<string[]> Refresh { get; } = CliOptions.Refresh();
    public Option<bool> Offline { get; } = CliOptions.Offline();
    public Option<string> OracleScript { get; } = CliOptions.OracleScript();
    public Option<string> OnLowConfidence { get; } = CliOptions.OnLowConfidence();
    public Option<double?> MinConfidence { get; } = CliOptions.MinConfidence();
    public Option<string> Model { get; } = CliOptions.Model();
    public Option<int> Timeout { get; } = CliOptions.Timeout();

    public void AddTo(Command command)
    {
        command.Options.Add(Spec);
        command.Options.Add(Image);
        command.Options.Add(Name);
        command.Options.Add(Set);
        command.Options.Add(Lock);
        command.Options.Add(Refresh);
        command.Options.Add(Offline);
        command.Options.Add(OracleScript);
        command.Options.Add(OnLowConfidence);
        command.Options.Add(MinConfidence);
        command.Options.Add(Model);
        command.Options.Add(Timeout);
    }
}
