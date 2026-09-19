using System.Collections.ObjectModel;

namespace GitClear.Core.Model;

/// <summary>
/// A folder in the ignored-file tree. Carries its direct files, its subfolders,
/// and the size / file-count aggregated over its entire subtree (UI-1).
/// The two factories are the only way to build one, so the totals always agree
/// with the contents.
/// </summary>
public sealed record IgnoredFolderNode
{
    private IgnoredFolderNode(
        string name,
        string relativePath,
        string fullPath,
        bool isFullyIgnored,
        IReadOnlyList<IgnoredFolderNode> subfolders,
        IReadOnlyList<IgnoredFileNode> files,
        long totalSize,
        int totalFileCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(relativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        ArgumentOutOfRangeException.ThrowIfNegative(totalSize);
        ArgumentOutOfRangeException.ThrowIfNegative(totalFileCount);

        Name = name;
        RelativePath = relativePath;
        FullPath = fullPath;
        IsFullyIgnored = isFullyIgnored;
        Subfolders = subfolders;
        Files = files;
        TotalSize = totalSize;
        TotalFileCount = totalFileCount;
    }

    /// <summary>
    /// An ordinary folder: its totals are aggregated from its children. The
    /// children are copied, so the caller cannot change them afterwards.
    /// </summary>
    /// <param name="relativePath">'/'-separated path from the repository root; empty for the root.</param>
    public static IgnoredFolderNode Folder(
        string name,
        string relativePath,
        string fullPath,
        IEnumerable<IgnoredFolderNode> subfolders,
        IEnumerable<IgnoredFileNode> files)
    {
        ArgumentNullException.ThrowIfNull(subfolders);
        ArgumentNullException.ThrowIfNull(files);

        ReadOnlyCollection<IgnoredFolderNode> subfolderCopy = subfolders.ToList().AsReadOnly();
        ReadOnlyCollection<IgnoredFileNode> fileCopy = files.ToList().AsReadOnly();

        return new IgnoredFolderNode(
            name,
            relativePath,
            fullPath,
            isFullyIgnored: false,
            subfolderCopy,
            fileCopy,
            totalSize: fileCopy.Sum(file => file.Size) + subfolderCopy.Sum(folder => folder.TotalSize),
            totalFileCount: fileCopy.Count + subfolderCopy.Sum(folder => folder.TotalFileCount));
    }

    /// <summary>
    /// A wholly-ignored directory (SCAN-2): a leaf with no enumerated children,
    /// whose totals come from walking it on disk.
    /// </summary>
    public static IgnoredFolderNode FullyIgnored(
        string name,
        string relativePath,
        string fullPath,
        long totalSize,
        int totalFileCount)
    {
        return new IgnoredFolderNode(
            name,
            relativePath,
            fullPath,
            isFullyIgnored: true,
            subfolders: [],
            files: [],
            totalSize,
            totalFileCount);
    }

    /// <summary>Folder name (leaf), for display. The repository root uses the repository folder name.</summary>
    public string Name { get; }

    /// <summary>Path relative to the repository root ('/' separators); empty for the root.</summary>
    public string RelativePath { get; }

    /// <summary>Absolute path on disk.</summary>
    public string FullPath { get; }

    /// <summary>
    /// True when git reported this whole directory as ignored (via
    /// <c>ls-files --directory</c>): it contains no tracked files, so it is safe
    /// to delete as a single unit (DEL-1). Such nodes have no enumerated
    /// children; their <see cref="TotalSize"/>/<see cref="TotalFileCount"/> come
    /// from walking the directory.
    /// </summary>
    public bool IsFullyIgnored { get; }

    /// <summary>Immediate subfolders, in the order given.</summary>
    public IReadOnlyList<IgnoredFolderNode> Subfolders { get; }

    /// <summary>Ignored files directly in this folder, in the order given.</summary>
    public IReadOnlyList<IgnoredFileNode> Files { get; }

    /// <summary>Total size in bytes of every ignored file in this subtree.</summary>
    public long TotalSize { get; }

    /// <summary>Total number of ignored files in this subtree.</summary>
    public int TotalFileCount { get; }
}
