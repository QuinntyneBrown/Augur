namespace Contoso.Orders.Api.Notes;

/// <summary>A short note. Replace this sample feature with your own.</summary>
public sealed class Note
{
    public const int MaxTitleLength = 200;

    public Guid Id { get; set; }

    public required string Title { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>The body of <c>POST /api/notes</c>.</summary>
public sealed record CreateNoteRequest(string? Title)
{
    /// <summary>The validation problems with this request, keyed by field; empty when it is valid.</summary>
    public Dictionary<string, string[]> Problems()
    {
        var problems = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(Title))
        {
            problems["title"] = ["A title is required."];
        }
        else if (Title.Trim().Length > Note.MaxTitleLength)
        {
            problems["title"] = [$"A title is at most {Note.MaxTitleLength} characters."];
        }

        return problems;
    }
}
