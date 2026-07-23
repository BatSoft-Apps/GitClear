namespace GitClear.Core.Git;

/// <summary>
/// Thrown when a git command runs but exits with a non-zero status.
/// </summary>
public sealed class GitCommandException : Exception
{
    public GitCommandException(string message, int exitCode)
        : base(message)
    {
        ExitCode = exitCode;
    }

    /// <summary>The process exit code git returned.</summary>
    public int ExitCode { get; }
}
