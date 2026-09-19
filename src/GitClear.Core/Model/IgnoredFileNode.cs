namespace GitClear.Core.Model;

/// <summary>
/// A single git-ignored file in the scan tree.
/// </summary>
public sealed record IgnoredFileNode
{
    public IgnoredFileNode(string name, string relativePath, string fullPath, long size)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        ArgumentOutOfRangeException.ThrowIfNegative(size);

        Name = name;
        RelativePath = relativePath;
        FullPath = fullPath;
        Size = size;
    }

    /// <summary>File name (leaf), for display.</summary>
    public string Name { get; }

    /// <summary>Path relative to the repository root, using '/' separators (as git reports).</summary>
    public string RelativePath { get; }

    /// <summary>Absolute path on disk.</summary>
    public string FullPath { get; }

    /// <summary>File size in bytes (0 if it could not be statted).</summary>
    public long Size { get; }
}
