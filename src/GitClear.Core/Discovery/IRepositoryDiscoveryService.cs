using GitClear.Core.Model;

namespace GitClear.Core.Discovery;

/// <summary>
/// Finds Git repositories under a root folder (DISC-1/2). Results are streamed
/// as they are found so a UI can display them incrementally.
/// </summary>
public interface IRepositoryDiscoveryService
{
    /// <summary>
    /// Walks <paramref name="rootPath"/> and yields each Git repository found.
    /// Once a repository is found, its subtree is not descended into (DISC-2).
    /// Unreadable folders (permissions, junctions) are skipped silently.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="rootPath"/> is null or blank.</exception>
    /// <exception cref="DirectoryNotFoundException"><paramref name="rootPath"/> does not exist.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    IAsyncEnumerable<RepositoryInfo> DiscoverAsync(
        string rootPath,
        CancellationToken cancellationToken = default);
}
