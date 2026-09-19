namespace GitClear.Core.Model;

/// <summary>
/// A Git repository discovered under a scanned root folder (DISC-1).
/// </summary>
public sealed record RepositoryInfo
{
    public RepositoryInfo(string fullPath, bool isWorktreeOrSubmodule)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        FullPath = fullPath;
        Name = new DirectoryInfo(fullPath).Name;
        IsWorktreeOrSubmodule = isWorktreeOrSubmodule;
    }

    /// <summary>Absolute path to the repository's working-tree root.</summary>
    public string FullPath { get; }

    /// <summary>Leaf folder name, for display.</summary>
    public string Name { get; }

    /// <summary>
    /// True when the repository's <c>.git</c> is a file rather than a folder —
    /// i.e. a worktree or submodule that points elsewhere.
    /// </summary>
    public bool IsWorktreeOrSubmodule { get; }
}
