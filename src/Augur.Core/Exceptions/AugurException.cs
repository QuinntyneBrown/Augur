namespace Augur.Core;

/// <summary>A failure that augur reports as a single <c>error:</c> line with a documented exit code.</summary>
public abstract class AugurException(string message, ExitCode exitCode, Exception? innerException = null)
    : Exception(message, innerException)
{
    public ExitCode ExitCode { get; } = exitCode;

    /// <summary>Lines that explain the failure, written before the single <c>error:</c> line at every verbosity.</summary>
    public virtual IReadOnlyList<string> Details => [];
}

/// <summary>Invalid arguments or invalid input files (exit code 2).</summary>
public class UsageException(string message, Exception? innerException = null)
    : AugurException(message, ExitCode.InvalidUsage, innerException);

/// <summary>One or more decisions could not be resolved (exit code 3).</summary>
public class UnresolvedDecisionsException(string message, IReadOnlyList<string> decisionIds)
    : AugurException(message, ExitCode.Unresolved)
{
    public IReadOnlyList<string> DecisionIds { get; } = decisionIds;
}

/// <summary>The Decisions API failed or returned something unusable (exit code 4).</summary>
public class DecisionsApiException(string message, Exception? innerException = null)
    : AugurException(message, ExitCode.ApiFailure, innerException);

/// <summary>The output location conflicts with what augur would write, or a write failed (exit code 5).</summary>
public class OutputConflictException(string message, Exception? innerException = null)
    : AugurException(message, ExitCode.OutputConflict, innerException);

/// <summary>The built-in catalog is malformed (exit code 1); this is a defect in augur itself.</summary>
public sealed class InvalidCatalogException(string decisionId, string reason)
    : AugurException($"invalid catalog: decision '{decisionId}' {reason}", ExitCode.InternalError)
{
    public string DecisionId { get; } = decisionId;

    public string Reason { get; } = reason;
}
