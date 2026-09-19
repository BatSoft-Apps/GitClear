namespace GitClear.Core.Deletion;

/// <summary>
/// Default <see cref="IDeletionService"/>: keeps the targets that still exist and
/// recycles them via <see cref="IRecycleBinService"/> on a background thread.
/// </summary>
public sealed class RecycleBinDeletionService : IDeletionService
{
    private readonly IRecycleBinService _recycleBin;

    public RecycleBinDeletionService(IRecycleBinService recycleBin)
    {
        ArgumentNullException.ThrowIfNull(recycleBin);

        _recycleBin = recycleBin;
    }

    public Task<DeletionResult> DeleteAsync(
        IReadOnlyCollection<string> targetPaths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targetPaths);

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Targets are files or wholly-ignored directories (DEL-1).
            List<string> existing = new(targetPaths.Count);
            foreach (string path in targetPaths)
            {
                if (File.Exists(path) || Directory.Exists(path))
                {
                    existing.Add(path);
                }
            }

            int skipped = targetPaths.Count - existing.Count;
            bool completed = existing.Count == 0 || _recycleBin.Recycle(existing);

            return new DeletionResult(existing.Count, skipped, aborted: !completed);
        }, cancellationToken);
    }

    public Task<int> RestoreAsync(
        IReadOnlyCollection<string> originalPaths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(originalPaths);

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return _recycleBin.Restore(originalPaths);
        }, cancellationToken);
    }
}
