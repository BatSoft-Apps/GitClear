namespace GitClear.Core.Deletion;

/// <summary>
/// Default <see cref="IDeletionService"/>: filters to files that still exist and
/// recycles them via <see cref="IRecycleBinService"/> on a background thread.
/// </summary>
public sealed class RecycleBinDeletionService : IDeletionService
{
    private readonly IRecycleBinService _recycleBin;

    public RecycleBinDeletionService(IRecycleBinService recycleBin) => _recycleBin = recycleBin;

    public Task<DeletionResult> DeleteAsync(
        IReadOnlyCollection<string> filePaths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePaths);

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Targets are files or wholly-ignored directories (DEL-1).
            var existing = new List<string>(filePaths.Count);
            foreach (var path in filePaths)
            {
                if (File.Exists(path) || Directory.Exists(path))
                {
                    existing.Add(path);
                }
            }

            var skipped = filePaths.Count - existing.Count;

            if (existing.Count > 0)
            {
                _recycleBin.Recycle(existing);
            }

            return new DeletionResult(existing.Count, skipped);
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
