using GitClear.Core.Model;

namespace GitClear.Core.Scanning;

/// <summary>
/// Scans a repository for git-ignored files and returns a sized tree (SCAN-1/2/3).
/// </summary>
public interface IIgnoredFileScanner
{
    /// <summary>
    /// Asks git for the ignored files, stats their sizes, and builds the tree.
    /// The heavy sizing work runs off the caller's thread.
    /// </summary>
    /// <param name="repositoryPath">Absolute path to the repository root.</param>
    /// <param name="progress">Optional sizing progress.</param>
    /// <param name="cancellationToken">Cancels the scan.</param>
    /// <exception cref="ArgumentException"><paramref name="repositoryPath"/> is blank.</exception>
    /// <exception cref="DirectoryNotFoundException">The repository folder is missing.</exception>
    /// <exception cref="Git.GitNotFoundException">git is not installed.</exception>
    /// <exception cref="Git.GitCommandException">git returned a non-zero exit code.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    Task<IgnoredScanResult> ScanAsync(
        string repositoryPath,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
