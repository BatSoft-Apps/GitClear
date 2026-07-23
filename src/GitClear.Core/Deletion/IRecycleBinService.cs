namespace GitClear.Core.Deletion;

/// <summary>
/// Moves filesystem paths to the Windows Recycle Bin (ARCH-4 / DEL-1). Isolated
/// behind an interface so the deletion orchestration can be tested without
/// touching the real shell.
/// </summary>
public interface IRecycleBinService
{
    /// <summary>
    /// Sends the given existing paths to the Recycle Bin in one shell operation.
    /// </summary>
    /// <exception cref="DeletionException">The shell operation failed or was aborted.</exception>
    void Recycle(IReadOnlyList<string> paths);

    /// <summary>
    /// Restores Recycle Bin items back to the given original locations. Items no
    /// longer present are skipped. Returns the number of items restored.
    /// </summary>
    int Restore(IReadOnlyCollection<string> originalPaths);
}
