namespace GitClear.Core.Scanning;

/// <summary>
/// A flat (path, size) pair fed into <see cref="IgnoredTreeBuilder"/>.
/// </summary>
public sealed record IgnoredFileEntry
{
    /// <param name="relativePath">Repository-relative path with '/' separators (as git reports).</param>
    /// <param name="size">File size in bytes.</param>
    public IgnoredFileEntry(string relativePath, long size)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        // Must name at least one folder or file, not just separators.
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath.Trim('/'), nameof(relativePath));
        ArgumentOutOfRangeException.ThrowIfNegative(size);

        RelativePath = relativePath;
        Size = size;
    }

    public string RelativePath { get; }

    public long Size { get; }
}
