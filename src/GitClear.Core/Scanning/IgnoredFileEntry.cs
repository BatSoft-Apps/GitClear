namespace GitClear.Core.Scanning;

/// <summary>
/// A flat (path, size) pair fed into <see cref="IgnoredTreeBuilder"/>.
/// </summary>
/// <param name="RelativePath">Repo-relative path with '/' separators (as git reports).</param>
/// <param name="Size">File size in bytes.</param>
public readonly record struct IgnoredFileEntry(string RelativePath, long Size);
