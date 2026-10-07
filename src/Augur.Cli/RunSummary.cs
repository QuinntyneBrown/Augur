using System.Globalization;
using Augur.Core.Decisions;

namespace Augur.Cli;

/// <summary>The one-line summary that ends a successful plan, emit, or generate run at normal verbosity.</summary>
internal static class RunSummary
{
    public static string Render(DecisionState? state, int apiRequests, TimeSpan elapsed, int? filesWritten = null)
    {
        var parts = new List<string>();
        if (state is not null)
        {
            var bySource = Enum.GetValues<DecisionSource>()
                .Select(source => $"{source.ToWireName()} {state.Resolved.Count(d => d.Source == source)}");
            parts.Add($"resolved {state.Resolved.Count} ({string.Join(", ", bySource)})");
            parts.Add($"skipped {state.Skipped.Count}");
            parts.Add($"api requests {apiRequests}");
        }

        if (filesWritten is { } files)
        {
            parts.Add($"files written {files}");
        }

        parts.Add(string.Create(CultureInfo.InvariantCulture, $"elapsed {elapsed.TotalSeconds:0.00}s"));
        return string.Join(", ", parts);
    }
}
