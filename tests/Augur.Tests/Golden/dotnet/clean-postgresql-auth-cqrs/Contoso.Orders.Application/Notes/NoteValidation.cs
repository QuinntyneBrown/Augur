using Contoso.Orders.Domain.Notes;

namespace Contoso.Orders.Application.Notes;

/// <summary>The rules a new note must follow.</summary>
public static class NoteValidation
{
    /// <summary>The validation problems with <paramref name="title"/>, keyed by field; empty when it is valid.</summary>
    public static Dictionary<string, string[]> Problems(string? title)
    {
        var problems = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(title))
        {
            problems["title"] = ["A title is required."];
        }
        else if (title.Trim().Length > Note.MaxTitleLength)
        {
            problems["title"] = [$"A title is at most {Note.MaxTitleLength} characters."];
        }

        return problems;
    }
}
