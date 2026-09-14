namespace GitClear.Core.Deletion;

/// <summary>
/// Moves filesystem paths to the Windows Recycle Bin (ARCH-4 / DEL-1). Isolated
/// behind an interface so the deletion orchestration can be tested without
/// touching the real shell.
/// </summary>
public interface IRecycleBinService
{
    /// <summary>
    /// Sends the given existing paths to the Recycle Bin. Anything too large to
    /// recycle prompts the user before being destroyed (DEL-5).
    /// </summary>
    /// <returns>
    /// <c>true</c> if the operation completed; <c>false</c> if the user declined a
    /// permanent-delete warning, in which case some earlier items may already
    /// have been recycled.
    /// </returns>
    /// <exception cref="DeletionException">The shell operation failed.</exception>
    bool Recycle(IReadOnlyList<string> paths);

    /// <summary>
    /// Restores Recycle Bin items back to the given original locations. Items no
    /// longer present are skipped. Returns the number of items restored.
    /// </summary>
    int Restore(IReadOnlyCollection<string> originalPaths);
}
