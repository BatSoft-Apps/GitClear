namespace GitClear.Core.Model;

/// <summary>
/// A single git-ignored file in the scan tree.
/// </summary>
public sealed record IgnoredFileNode
{
    /// <summary>File name (leaf), for display.</summary>
    public required string Name { get; init; }

    /// <summary>Path relative to the repository root, using '/' separators (as git reports).</summary>
    public required string RelativePath { get; init; }

    /// <summary>Absolute path on disk.</summary>
    public required string FullPath { get; init; }

    /// <summary>File size in bytes (0 if it could not be statted).</summary>
    public required long Size { get; init; }
}
