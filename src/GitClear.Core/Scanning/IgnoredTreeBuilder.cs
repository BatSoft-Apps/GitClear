using GitClear.Core.Model;

namespace GitClear.Core.Scanning;

/// <summary>
/// Builds the immutable ignored-file tree from flat lists of ignored files and
/// wholly-ignored directories. Pure and filesystem-free, so the aggregation
/// logic is unit-testable in isolation (SCAN-2).
/// </summary>
public static class IgnoredTreeBuilder
{
    public static IgnoredFolderNode Build(string repositoryPath, IEnumerable<IgnoredFileEntry> files) =>
        Build(repositoryPath, files, []);

    public static IgnoredFolderNode Build(
        string repositoryPath,
        IEnumerable<IgnoredFileEntry> files,
        IEnumerable<IgnoredDirectoryEntry> fullyIgnoredDirectories)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(fullyIgnoredDirectories);

        var root = new FolderBuilder(new DirectoryInfo(repositoryPath).Name, relativePath: string.Empty);

        foreach (var entry in files)
        {
            var segments = Split(entry.RelativePath);
            if (segments.Length == 0)
            {
                continue; // Defensive: ignore blank paths.
            }

            var folder = root;
            for (var i = 0; i < segments.Length - 1; i++)
            {
                folder = folder.GetOrAddSubfolder(segments[i]);
            }

            folder.Files.Add(new FileLeaf(segments[^1], entry.RelativePath, entry.Size));
        }

        foreach (var entry in fullyIgnoredDirectories)
        {
            var segments = Split(entry.RelativePath);
            if (segments.Length == 0)
            {
                continue;
            }

            var folder = root;
            for (var i = 0; i < segments.Length - 1; i++)
            {
                folder = folder.GetOrAddSubfolder(segments[i]);
            }

            var target = folder.GetOrAddSubfolder(segments[^1]);
            target.MarkFullyIgnored(entry.TotalSize, entry.FileCount);
        }

        return Freeze(root, repositoryPath);
    }

    private static string[] Split(string relativePath) =>
        relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

    private static IgnoredFolderNode Freeze(FolderBuilder builder, string repositoryPath)
    {
        var fullPath = builder.RelativePath.Length == 0
            ? repositoryPath
            : ToFullPath(repositoryPath, builder.RelativePath);

        // A wholly-ignored directory is a leaf: no children, size from the walk.
        if (builder.IsFullyIgnored)
        {
            return new IgnoredFolderNode
            {
                Name = builder.Name,
                RelativePath = builder.RelativePath,
                FullPath = fullPath,
                IsFullyIgnored = true,
                Subfolders = [],
                Files = [],
                TotalSize = builder.FullyIgnoredSize,
                TotalFileCount = builder.FullyIgnoredCount,
            };
        }

        var subfolders = builder.Subfolders.Values
            .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .Select(f => Freeze(f, repositoryPath))
            .ToList();

        var childFiles = builder.Files
            .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .Select(f => new IgnoredFileNode
            {
                Name = f.Name,
                RelativePath = f.RelativePath,
                FullPath = ToFullPath(repositoryPath, f.RelativePath),
                Size = f.Size,
            })
            .ToList();

        var totalSize = childFiles.Sum(f => f.Size) + subfolders.Sum(f => f.TotalSize);
        var totalCount = childFiles.Count + subfolders.Sum(f => f.TotalFileCount);

        return new IgnoredFolderNode
        {
            Name = builder.Name,
            RelativePath = builder.RelativePath,
            FullPath = fullPath,
            Subfolders = subfolders,
            Files = childFiles,
            TotalSize = totalSize,
            TotalFileCount = totalCount,
        };
    }

    private static string ToFullPath(string repositoryPath, string relativePath) =>
        Path.Combine(repositoryPath, relativePath.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Mutable scaffold used only while building; frozen into records at the end.</summary>
    private sealed class FolderBuilder(string name, string relativePath)
    {
        public string Name { get; } = name;

        public string RelativePath { get; } = relativePath;

        // Case-insensitive keys: Windows paths are case-insensitive, so entries
        // differing only by case belong to the same folder.
        public Dictionary<string, FolderBuilder> Subfolders { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public List<FileLeaf> Files { get; } = [];

        public bool IsFullyIgnored { get; private set; }

        public long FullyIgnoredSize { get; private set; }

        public int FullyIgnoredCount { get; private set; }

        public FolderBuilder GetOrAddSubfolder(string name)
        {
            if (!Subfolders.TryGetValue(name, out var child))
            {
                var childRelative = RelativePath.Length == 0 ? name : $"{RelativePath}/{name}";
                child = new FolderBuilder(name, childRelative);
                Subfolders[name] = child;
            }

            return child;
        }

        public void MarkFullyIgnored(long totalSize, int fileCount)
        {
            IsFullyIgnored = true;
            FullyIgnoredSize = totalSize;
            FullyIgnoredCount = fileCount;
        }
    }

    private readonly record struct FileLeaf(string Name, string RelativePath, long Size);
}
