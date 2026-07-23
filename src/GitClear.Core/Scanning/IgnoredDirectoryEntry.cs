namespace GitClear.Core.Scanning;

/// <summary>
/// A wholly-ignored directory (git <c>ls-files --directory</c> collapsed it to a
/// single entry) fed into <see cref="IgnoredTreeBuilder"/>. Its size and file
/// count are computed by walking the directory.
/// </summary>
/// <param name="RelativePath">Repo-relative path with '/' separators, no trailing slash.</param>
/// <param name="TotalSize">Sum of the sizes of all files under the directory.</param>
/// <param name="FileCount">Number of files under the directory.</param>
public readonly record struct IgnoredDirectoryEntry(string RelativePath, long TotalSize, int FileCount);
