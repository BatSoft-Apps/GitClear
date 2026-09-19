namespace GitClear.Core.Deletion;

/// <summary>
/// Deletes selected ignored targets by moving them to the Recycle Bin (DEL-1).
/// </summary>
public interface IDeletionService
{
    /// <summary>
    /// Sends the given targets — individual files and wholly-ignored directories
    /// (DEL-1) — to the Recycle Bin, off the caller's thread. Targets that no longer
    /// exist are skipped and counted, not treated as errors. If the user declines a
    /// permanent-delete warning the result is <see cref="DeletionResult.Aborted"/>,
    /// not an exception (DEL-5).
    /// </summary>
    /// <exception cref="DeletionException">The shell delete operation failed.</exception>
    /// <exception cref="OperationCanceledException">Cancelled before the shell call.</exception>
    Task<DeletionResult> DeleteAsync(
        IReadOnlyCollection<string> targetPaths,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores previously-recycled items back to their original locations, off
    /// the caller's thread. Returns the number of items restored (best-effort).
    /// </summary>
    Task<int> RestoreAsync(
        IReadOnlyCollection<string> originalPaths,
        CancellationToken cancellationToken = default);
}
