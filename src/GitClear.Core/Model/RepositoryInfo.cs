namespace GitClear.Core.Model;

/// <summary>
/// A Git repository discovered under a scanned root folder (DISC-1).
/// </summary>
public sealed record RepositoryInfo
{
    /// <summary>Absolute path to the repository's working-tree root.</summary>
    public required string FullPath { get; init; }

    /// <summary>Leaf folder name, for display.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// True when the repository's <c>.git</c> is a file rather than a folder —
    /// i.e. a worktree or submodule that points elsewhere.
    /// </summary>
    public required bool IsWorktreeOrSubmodule { get; init; }

    /// <summary>Creates an instance from a repository root path.</summary>
    public static RepositoryInfo Create(string fullPath, bool isWorktreeOrSubmodule) => new()
    {
        FullPath = fullPath,
        Name = new DirectoryInfo(fullPath).Name,
        IsWorktreeOrSubmodule = isWorktreeOrSubmodule,
    };
}
