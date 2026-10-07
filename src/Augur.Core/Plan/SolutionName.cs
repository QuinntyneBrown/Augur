using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Augur.Core.Plan;

/// <summary>A validated .NET solution name such as <c>Contoso.Orders</c>.</summary>
public sealed partial record SolutionName
{
    public const int MaxLength = 64;

    public const string Rule =
        "--name must be dot-separated segments that each start with an upper-case letter followed by letters or digits, "
        + "at most 64 characters, with no segment that is a C# keyword";

    private static readonly HashSet<string> CSharpKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const",
        "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern",
        "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface",
        "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override",
        "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
        "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof",
        "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while",
    };

    private SolutionName(string value) => Value = value;

    public string Value { get; }

    public IReadOnlyList<string> Segments => Value.Split('.');

    public static bool TryParse(string? value, [NotNullWhen(true)] out SolutionName? name)
    {
        name = value is { Length: > 0 and <= MaxLength }
            && Pattern().IsMatch(value)
            && !value.Split('.').Any(CSharpKeywords.Contains)
            ? new SolutionName(value)
            : null;
        return name is not null;
    }

    /// <exception cref="UsageException">The name breaks the naming rule.</exception>
    public static SolutionName Parse(string? value) =>
        TryParse(value, out var name) ? name : throw new UsageException(Rule);

    public override string ToString() => Value;

    [GeneratedRegex("^[A-Z][A-Za-z0-9]*(\\.[A-Z][A-Za-z0-9]*)*$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
