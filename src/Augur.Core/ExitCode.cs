namespace Augur.Core;

/// <summary>The process exit codes augur documents (see the conventions in docs/specs/L2.md).</summary>
public enum ExitCode
{
    Success = 0,
    InternalError = 1,
    InvalidUsage = 2,
    Unresolved = 3,
    ApiFailure = 4,
    OutputConflict = 5,
    Cancelled = 130,
}
