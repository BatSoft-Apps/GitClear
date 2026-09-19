namespace GitClear.Core.Model;

/// <summary>
/// The outcome of scanning one repository for git-ignored files (SCAN-1/2).
/// </summary>
public sealed record IgnoredScanResult
{
    public IgnoredScanResult(IgnoredFolderNode root, int unreadableFileCount)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentOutOfRangeException.ThrowIfNegative(unreadableFileCount);

        Root = root;
        UnreadableFileCount = unreadableFileCount;
    }

    /// <summary>Root of the ignored-file tree (represents the repository root folder).</summary>
    public IgnoredFolderNode Root { get; }

    /// <summary>Number of ignored files that could not be statted (counted as size 0).</summary>
    public int UnreadableFileCount { get; }

    /// <summary>Total size in bytes of all ignored files.</summary>
    public long TotalSize => Root.TotalSize;

    /// <summary>Total number of ignored files found.</summary>
    public int TotalFileCount => Root.TotalFileCount;
}
