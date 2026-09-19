using GitClear.Core.Model;
using GitClear.Core.Scanning;

namespace GitClear.Core.Tests.Scanning;

public sealed class IgnoredTreeBuilderTests
{
    // A path that need not exist: the builder only parses it, never touches disk.
    private const string RepositoryRoot = @"C:\repo";

    private static IgnoredFolderNode Build(params IgnoredFileEntry[] entries)
        => IgnoredTreeBuilder.Build(RepositoryRoot, entries);

    private static IgnoredFolderNode Subfolder(IgnoredFolderNode folder, string name)
        => folder.Subfolders.Single(f => f.Name == name);

    [Fact]
    public void Empty_input_yields_an_empty_root_named_after_the_repository()
    {
        IgnoredFolderNode root = Build();

        Assert.Equal("repo", root.Name);
        Assert.Equal(string.Empty, root.RelativePath);
        Assert.Equal(RepositoryRoot, root.FullPath);
        Assert.Empty(root.Files);
        Assert.Empty(root.Subfolders);
        Assert.Equal(0, root.TotalSize);
        Assert.Equal(0, root.TotalFileCount);
    }

    [Fact]
    public void A_root_level_file_lands_directly_under_the_root()
    {
        IgnoredFolderNode root = Build(new IgnoredFileEntry("app.log", 100));

        IgnoredFileNode file = Assert.Single(root.Files);
        Assert.Equal("app.log", file.Name);
        Assert.Equal("app.log", file.RelativePath);
        Assert.Equal(@"C:\repo\app.log", file.FullPath);
        Assert.Equal(100, file.Size);
        Assert.Equal(100, root.TotalSize);
        Assert.Equal(1, root.TotalFileCount);
    }

    [Fact]
    public void Nested_paths_create_intermediate_folders_with_correct_paths()
    {
        IgnoredFolderNode root = Build(new IgnoredFileEntry("build/sub/deep.bin", 42));

        IgnoredFolderNode build = Assert.Single(root.Subfolders);
        Assert.Equal("build", build.Name);
        Assert.Equal("build", build.RelativePath);
        Assert.Equal(@"C:\repo\build", build.FullPath);

        IgnoredFolderNode subfolder = Assert.Single(build.Subfolders);
        Assert.Equal("sub", subfolder.Name);
        Assert.Equal("build/sub", subfolder.RelativePath);

        IgnoredFileNode file = Assert.Single(subfolder.Files);
        Assert.Equal("deep.bin", file.Name);
        Assert.Equal(@"C:\repo\build\sub\deep.bin", file.FullPath);
    }

    [Fact]
    public void Sizes_and_counts_aggregate_up_every_level()
    {
        IgnoredFolderNode root = Build(
            new IgnoredFileEntry("a.log", 10),
            new IgnoredFileEntry("build/one.bin", 100),
            new IgnoredFileEntry("build/two.bin", 200),
            new IgnoredFileEntry("build/sub/deep.bin", 1000));

        Assert.Equal(1310, root.TotalSize);
        Assert.Equal(4, root.TotalFileCount);

        IgnoredFolderNode build = Subfolder(root, "build");
        Assert.Equal(1300, build.TotalSize);
        Assert.Equal(3, build.TotalFileCount);

        IgnoredFolderNode subfolder = Subfolder(build, "sub");
        Assert.Equal(1000, subfolder.TotalSize);
        Assert.Equal(1, subfolder.TotalFileCount);
    }

    [Fact]
    public void Multiple_files_in_one_folder_are_grouped()
    {
        IgnoredFolderNode root = Build(
            new IgnoredFileEntry("binFolder/a.dll", 1),
            new IgnoredFileEntry("binFolder/b.dll", 2),
            new IgnoredFileEntry("binFolder/c.dll", 3));

        IgnoredFolderNode binFolder = Assert.Single(root.Subfolders);
        Assert.Equal(3, binFolder.Files.Count);
        Assert.Equal(6, binFolder.TotalSize);
    }

    [Fact]
    public void Subfolders_and_files_are_sorted_by_name()
    {
        IgnoredFolderNode root = Build(
            new IgnoredFileEntry("zebra/z.txt", 1),
            new IgnoredFileEntry("alpha/a.txt", 1),
            new IgnoredFileEntry("m2.txt", 1),
            new IgnoredFileEntry("m1.txt", 1));

        Assert.Equal(new[] { "alpha", "zebra" }, root.Subfolders.Select(f => f.Name).ToArray());
        Assert.Equal(new[] { "m1.txt", "m2.txt" }, root.Files.Select(f => f.Name).ToArray());
    }

    [Fact]
    public void A_fully_ignored_directory_becomes_a_leaf_carrying_its_own_size()
    {
        IgnoredFolderNode root = IgnoredTreeBuilder.Build(
            RepositoryRoot,
            files: [new IgnoredFileEntry("app.log", 10)],
            fullyIgnoredDirectories: [new IgnoredDirectoryEntry("node_modules", 5000, 42)]);

        IgnoredFolderNode nodeModules = Subfolder(root, "node_modules");
        Assert.True(nodeModules.IsFullyIgnored);
        Assert.Empty(nodeModules.Subfolders);
        Assert.Empty(nodeModules.Files);
        Assert.Equal(5000, nodeModules.TotalSize);
        Assert.Equal(42, nodeModules.TotalFileCount);

        // The directory's totals aggregate up through the root alongside the file.
        Assert.Equal(5010, root.TotalSize);
        Assert.Equal(43, root.TotalFileCount);
    }

    [Fact]
    public void A_nested_fully_ignored_directory_creates_ordinary_intermediate_folders()
    {
        IgnoredFolderNode root = IgnoredTreeBuilder.Build(
            RepositoryRoot,
            files: [],
            fullyIgnoredDirectories: [new IgnoredDirectoryEntry("tools/node_modules", 200, 3)]);

        IgnoredFolderNode tools = Assert.Single(root.Subfolders);
        Assert.False(tools.IsFullyIgnored);
        Assert.Equal(3, tools.TotalFileCount);

        IgnoredFolderNode nodeModules = Assert.Single(tools.Subfolders);
        Assert.True(nodeModules.IsFullyIgnored);
        Assert.Equal(200, nodeModules.TotalSize);
    }

    [Fact]
    public void Folder_names_differing_only_by_case_merge_into_one_folder()
    {
        IgnoredFolderNode root = Build(
            new IgnoredFileEntry("Build/a.bin", 5),
            new IgnoredFileEntry("build/b.bin", 7));

        IgnoredFolderNode build = Assert.Single(root.Subfolders);
        Assert.Equal(2, build.Files.Count);
        Assert.Equal(12, build.TotalSize);
    }
}
