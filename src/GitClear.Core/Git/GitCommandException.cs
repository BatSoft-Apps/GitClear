namespace GitClear.Core.Git;

/// <summary>
/// Thrown when a git command runs but exits with a non-zero status. Git's own
/// diagnostic is kept in <see cref="StandardError"/> so the UI can show the user
/// what git actually said instead of making them re-run git themselves (ARCH-3).
/// </summary>
public sealed class GitCommandException : Exception
{
    public GitCommandException(string message, int exitCode, string standardError = "")
        : base(message)
    {
        ArgumentNullException.ThrowIfNull(standardError);

        ExitCode = exitCode;
        StandardError = standardError;
    }

    /// <summary>The process exit code git returned.</summary>
    public int ExitCode { get; }

    /// <summary>Git's stderr text, verbatim. May be empty.</summary>
    public string StandardError { get; }
}
