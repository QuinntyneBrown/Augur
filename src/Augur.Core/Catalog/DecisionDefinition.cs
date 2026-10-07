namespace Augur.Core.Catalog;

/// <summary>One question the catalog can pose about a specification, with its closed set of answers.</summary>
public abstract record DecisionDefinition(string Id, string Instructions, string Default, WhenClause? When)
{
    /// <summary>The catch-all choice option. The model may return it, but it is never a resolved value.</summary>
    public const string Other = "other";

    public const string True = "true";
    public const string False = "false";

    public abstract DecisionType Type { get; }

    /// <summary>Every answer the decision can produce, in catalog order, as listed by <c>augur catalog</c>.</summary>
    public abstract IReadOnlyList<string> Answers { get; }

    /// <summary>The answers a plan, an override, or a prompt may use: <see cref="Answers"/> without <see cref="Other"/>.</summary>
    public IReadOnlyList<string> AcceptedValues => [.. Answers.Where(a => a != Other)];

    /// <summary>Whether the resolved value is a boolean (<c>true</c> or <c>false</c>) rather than an option name.</summary>
    public bool IsBoolean => Type is DecisionType.Predicate or DecisionType.Score;

    public string QuestionHash => QuestionHasher.Hash(this);
}

/// <summary>A yes/no decision answered with a probability.</summary>
public sealed record PredicateDefinition(
    string Id,
    string Instructions,
    string Default,
    WhenClause? When,
    double UpperThreshold = 0.80,
    double LowerThreshold = 0.20)
    : DecisionDefinition(Id, Instructions, Default, When)
{
    public override DecisionType Type => DecisionType.Predicate;

    public override IReadOnlyList<string> Answers { get; } = [True, False];
}

/// <summary>A pick-one decision answered with a choice and a confidence.</summary>
public sealed record ChoiceDefinition(
    string Id,
    string Instructions,
    string Default,
    WhenClause? When,
    IReadOnlyList<ChoiceOption> Options,
    double MinConfidence = 0.70)
    : DecisionDefinition(Id, Instructions, Default, When)
{
    public override DecisionType Type => DecisionType.Choice;

    public override IReadOnlyList<string> Answers => [.. Options.Select(o => o.Value)];
}

/// <summary>A decision answered with a score on ordered levels; it resolves to <c>true</c> when the score reaches <see cref="CutOff"/>.</summary>
public sealed record ScoreDefinition(
    string Id,
    string Instructions,
    string Default,
    WhenClause? When,
    IReadOnlyList<ScoreLevel> Levels,
    double CutOff,
    string ResolvedId,
    double MinConfidence = 0.70)
    : DecisionDefinition(Id, Instructions, Default, When)
{
    public override DecisionType Type => DecisionType.Score;

    public override IReadOnlyList<string> Answers { get; } = [True, False];
}

public sealed record ChoiceOption(string Value, string Description);

public sealed record ScoreLevel(string Label, string Description);

/// <summary>The decision applies only when <see cref="DependsOn"/> resolved to one of <see cref="AcceptedValues"/>.</summary>
public sealed record WhenClause(string DependsOn, IReadOnlyList<string> AcceptedValues)
{
    public string ToText() => $"{DependsOn} is {string.Join(" or ", AcceptedValues)}";
}
