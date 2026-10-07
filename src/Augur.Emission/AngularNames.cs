using System.Text;
using Augur.Core.Plan;

namespace Augur.Emission;

/// <summary>Derives Angular names from the .NET solution name.</summary>
public static class AngularNames
{
    /// <summary>
    /// Kebab-case of the solution name: words split at lower-to-upper and digit-to-upper boundaries and before the
    /// last capital of an acronym, joined with hyphens. <c>Acme.HRPortal</c> becomes <c>acme-hr-portal</c>.
    /// </summary>
    public static string ProjectName(SolutionName name) =>
        string.Join('-', Words(name).Select(w => w.ToLowerInvariant()));

    /// <summary>The same words with their original case, for page titles: <c>Acme.HRPortal</c> becomes <c>Acme HR Portal</c>.</summary>
    public static string DisplayName(SolutionName name) => string.Join(' ', Words(name));

    private static List<string> Words(SolutionName name)
    {
        var words = new List<string>();
        foreach (var segment in name.Segments)
        {
            var word = new StringBuilder();
            for (var i = 0; i < segment.Length; i++)
            {
                var c = segment[i];
                var previous = i > 0 ? segment[i - 1] : '\0';
                var next = i + 1 < segment.Length ? segment[i + 1] : '\0';
                var startsWord = i > 0 && char.IsUpper(c)
                    && (char.IsLower(previous) || char.IsDigit(previous) || (char.IsUpper(previous) && char.IsLower(next)));
                if (startsWord)
                {
                    words.Add(word.ToString());
                    word.Clear();
                }

                word.Append(c);
            }

            words.Add(word.ToString());
        }

        return words;
    }
}
