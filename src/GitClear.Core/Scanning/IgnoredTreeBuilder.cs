using GitClear.Core.Model;

namespace GitClear.Core.Scanning;

/// <summary>
/// Builds the immutable ignored-file tree from flat lists of ignored files and
/// wholly-ignored directories. Pure and filesystem-free, so the aggregation
/// logic is unit-testable in isolation (SCAN-2).
/// </summary>
public static class IgnoredTreeBuilder
{
    public static IgnoredFolderNode Build(string repositoryPath, IEnumerable<IgnoredFileEntry> files)
        => Build(repositoryPath, files, []);

    public static IgnoredFolderNode Build(
        string repositoryPath,
        IEnumerable<IgnoredFileEntry> files,
        IEnumerable<IgnoredDirectoryEntry> fullyIgnoredDirectories)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(fullyIgnoredDirectories);

        FolderBuilder root = new(new DirectoryInfo(repositoryPath).Name, relativePath: string.Empty);

        foreach (IgnoredFileEntry file in files)
        {
            string[] segments = Split(file.RelativePath);
            root.GetOrAddDescendant(segments[..^1])
                .Files.Add(new FileLeaf(segments[^1], file.RelativePath, file.Size));
        }

        foreach (IgnoredDirectoryEntry directory in fullyIgnoredDirectories)
        {
            root.GetOrAddDescendant(Split(directory.RelativePath))
                .MarkFullyIgnored(directory.TotalSize, directory.FileCount);
        }

        return Freeze(root, repositoryPath);
    }

    /// <summary>Turns a git-style ('/'-separated) repository-relative path into an absolute one.</summary>
    internal static string ToFullPath(string repositoryPath, string relativePath)
        => Path.Combine(repositoryPath, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static string[] Split(string relativePath)
        => relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

    private static IgnoredFolderNode Freeze(FolderBuilder builder, string repositoryPath)
    {
        string fullPath = ToFullPath(repositoryPath, builder.RelativePath);

        // A wholly-ignored directory is a leaf: no children, size from the walk.
        if (builder.IsFullyIgnored)
        {
            return IgnoredFolderNode.FullyIgnored(
                builder.Name, builder.RelativePath, fullPath, builder.FullyIgnoredSize, builder.FullyIgnoredCount);
        }

        IEnumerable<IgnoredFolderNode> subfolders = builder.Subfolders.Values
            .OrderBy(subfolder => subfolder.Name, StringComparer.OrdinalIgnoreCase)
            .Select(subfolder => Freeze(subfolder, repositoryPath));

        IEnumerable<IgnoredFileNode> files = builder.Files
            .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .Select(file => new IgnoredFileNode(
                file.Name, file.RelativePath, ToFullPath(repositoryPath, file.RelativePath), file.Size));

        return IgnoredFolderNode.Folder(builder.Name, builder.RelativePath, fullPath, subfolders, files);
    }

    /// <summary>Mutable scaffold used only while building; frozen into records at the end.</summary>
    private sealed class FolderBuilder(string name, string relativePath)
    {
        public string Name { get; } = name;

        public string RelativePath { get; } = relativePath;

        // Case-insensitive keys: Windows paths are case-insensitive, so entries
        // differing only by case belong to the same folder.
        public Dictionary<string, FolderBuilder> Subfolders { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<FileLeaf> Files { get; } = [];

        public bool IsFullyIgnored { get; private set; }

        public long FullyIgnoredSize { get; private set; }

        public int FullyIgnoredCount { get; private set; }

        /// <summary>Walks down the given folder names, creating any that are missing.</summary>
        public FolderBuilder GetOrAddDescendant(IEnumerable<string> folderNames)
        {
            FolderBuilder folder = this;
            foreach (string folderName in folderNames)
            {
                folder = folder.GetOrAddSubfolder(folderName);
            }

            return folder;
        }

        public void MarkFullyIgnored(long totalSize, int fileCount)
        {
            IsFullyIgnored = true;
            FullyIgnoredSize = totalSize;
            FullyIgnoredCount = fileCount;
        }

        private FolderBuilder GetOrAddSubfolder(string folderName)
        {
            if (!Subfolders.TryGetValue(folderName, out FolderBuilder? child))
            {
                string childRelativePath = RelativePath.Length == 0 ? folderName : $"{RelativePath}/{folderName}";
                child = new FolderBuilder(folderName, childRelativePath);
                Subfolders[folderName] = child;
            }

            return child;
        }
    }

    private readonly record struct FileLeaf(string Name, string RelativePath, long Size);
}
