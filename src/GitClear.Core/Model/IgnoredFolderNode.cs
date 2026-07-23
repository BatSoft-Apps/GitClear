namespace GitClear.Core.Model;

/// <summary>
/// A folder in the ignored-file tree. Carries its direct files, its subfolders,
/// and the size / file-count aggregated over its entire subtree (UI-1).
/// </summary>
public sealed record IgnoredFolderNode
{
    /// <summary>Folder name (leaf), for display. The repo root uses the repo folder name.</summary>
    public required string Name { get; init; }

    /// <summary>Path relative to the repository root ('/' separators); empty for the root.</summary>
    public required string RelativePath { get; init; }

    /// <summary>Absolute path on disk.</summary>
    public required string FullPath { get; init; }

    /// <summary>
    /// True when git reported this whole directory as ignored (via
    /// <c>ls-files --directory</c>): it contains no tracked files, so it is safe
    /// to delete as a single unit (DEL-1). Such nodes have no enumerated
    /// children; their <see cref="TotalSize"/>/<see cref="TotalFileCount"/> come
    /// from walking the directory.
    /// </summary>
    public bool IsFullyIgnored { get; init; }

    /// <summary>Immediate subfolders, sorted by name.</summary>
    public required IReadOnlyList<IgnoredFolderNode> Subfolders { get; init; }

    /// <summary>Ignored files directly in this folder, sorted by name.</summary>
    public required IReadOnlyList<IgnoredFileNode> Files { get; init; }

    /// <summary>Total size in bytes of every ignored file in this subtree.</summary>
    public required long TotalSize { get; init; }

    /// <summary>Total number of ignored files in this subtree.</summary>
    public required int TotalFileCount { get; init; }
}
