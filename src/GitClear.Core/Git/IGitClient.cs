namespace GitClear.Core.Git;

/// <summary>
/// A thin wrapper over the git command line. Isolating git behind an interface
/// keeps the scanner unit-testable with canned path lists (SCAN-1).
/// </summary>
public interface IGitClient
{
    /// <summary>
    /// Returns the repository-relative ignored entries, using '/' separators.
    /// Runs <c>git ls-files --others --ignored --exclude-standard -z --directory</c>,
    /// so a wholly-ignored directory appears as a single entry ending in '/';
    /// files in mixed folders are listed individually.
    /// </summary>
    /// <exception cref="GitNotFoundException">git is not installed / not on PATH.</exception>
    /// <exception cref="GitCommandException">git ran but returned a non-zero exit code.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    Task<IReadOnlyList<string>> GetIgnoredPathsAsync(
        string repositoryPath,
        CancellationToken cancellationToken = default);
}
