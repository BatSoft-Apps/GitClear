namespace GitClear.Core.Scanning;

/// <summary>
/// A wholly-ignored directory (git <c>ls-files --directory</c> collapsed it to a
/// single entry) fed into <see cref="IgnoredTreeBuilder"/>. Its size and file
/// count are computed by walking the directory.
/// </summary>
public sealed record IgnoredDirectoryEntry
{
    /// <param name="relativePath">Repository-relative path with '/' separators, no trailing slash.</param>
    /// <param name="totalSize">Sum of the sizes of all files under the directory.</param>
    /// <param name="fileCount">Number of files under the directory.</param>
    public IgnoredDirectoryEntry(string relativePath, long totalSize, int fileCount)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        // Must name at least one folder or file, not just separators.
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath.Trim('/'), nameof(relativePath));
        ArgumentOutOfRangeException.ThrowIfNegative(totalSize);
        ArgumentOutOfRangeException.ThrowIfNegative(fileCount);

        RelativePath = relativePath;
        TotalSize = totalSize;
        FileCount = fileCount;
    }

    public string RelativePath { get; }

    public long TotalSize { get; }

    public int FileCount { get; }
}
