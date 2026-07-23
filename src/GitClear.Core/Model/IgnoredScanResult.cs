namespace GitClear.Core.Model;

/// <summary>
/// The outcome of scanning one repository for git-ignored files (SCAN-1/2).
/// </summary>
public sealed record IgnoredScanResult
{
    /// <summary>
    /// Root of the ignored-file tree (represents the repository root folder).
    /// Its <see cref="IgnoredFolderNode.FullPath"/> is the scanned repository path.
    /// </summary>
    public required IgnoredFolderNode Root { get; init; }

    /// <summary>Number of ignored files that could not be statted (counted as size 0).</summary>
    public required int UnreadableFileCount { get; init; }

    /// <summary>Total size in bytes of all ignored files.</summary>
    public long TotalSize => Root.TotalSize;

    /// <summary>Total number of ignored files found.</summary>
    public int TotalFileCount => Root.TotalFileCount;
}
