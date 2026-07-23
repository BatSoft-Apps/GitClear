using GitClear.Core.Model;
using GitClear.Core.Scanning;

namespace GitClear.Core.Tests.Scanning;

public sealed class IgnoredTreeBuilderTests
{
    // A path that need not exist: the builder only parses it, never touches disk.
    private const string RepoRoot = @"C:\repo";

    private static IgnoredFolderNode Build(params IgnoredFileEntry[] entries) =>
        IgnoredTreeBuilder.Build(RepoRoot, entries);

    private static IgnoredFolderNode Sub(IgnoredFolderNode folder, string name) =>
        folder.Subfolders.Single(f => f.Name == name);

    [Fact]
    public void Empty_input_yields_an_empty_root_named_after_the_repo()
    {
        var root = Build();

        Assert.Equal("repo", root.Name);
        Assert.Equal(string.Empty, root.RelativePath);
        Assert.Equal(RepoRoot, root.FullPath);
        Assert.Empty(root.Files);
        Assert.Empty(root.Subfolders);
        Assert.Equal(0, root.TotalSize);
        Assert.Equal(0, root.TotalFileCount);
    }

    [Fact]
    public void A_root_level_file_lands_directly_under_the_root()
    {
        var root = Build(new IgnoredFileEntry("app.log", 100));

        var file = Assert.Single(root.Files);
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
        var root = Build(new IgnoredFileEntry("build/sub/deep.bin", 42));

        var build = Assert.Single(root.Subfolders);
        Assert.Equal("build", build.Name);
        Assert.Equal("build", build.RelativePath);
        Assert.Equal(@"C:\repo\build", build.FullPath);

        var sub = Assert.Single(build.Subfolders);
        Assert.Equal("sub", sub.Name);
        Assert.Equal("build/sub", sub.RelativePath);

        var file = Assert.Single(sub.Files);
        Assert.Equal("deep.bin", file.Name);
        Assert.Equal(@"C:\repo\build\sub\deep.bin", file.FullPath);
    }

    [Fact]
    public void Sizes_and_counts_aggregate_up_every_level()
    {
        var root = Build(
            new IgnoredFileEntry("a.log", 10),
            new IgnoredFileEntry("build/one.bin", 100),
            new IgnoredFileEntry("build/two.bin", 200),
            new IgnoredFileEntry("build/sub/deep.bin", 1000));

        Assert.Equal(1310, root.TotalSize);
        Assert.Equal(4, root.TotalFileCount);

        var build = Sub(root, "build");
        Assert.Equal(1300, build.TotalSize);
        Assert.Equal(3, build.TotalFileCount);

        var sub = Sub(build, "sub");
        Assert.Equal(1000, sub.TotalSize);
        Assert.Equal(1, sub.TotalFileCount);
    }

    [Fact]
    public void Multiple_files_in_one_folder_are_grouped()
    {
        var root = Build(
            new IgnoredFileEntry("bin/a.dll", 1),
            new IgnoredFileEntry("bin/b.dll", 2),
            new IgnoredFileEntry("bin/c.dll", 3));

        var bin = Assert.Single(root.Subfolders);
        Assert.Equal(3, bin.Files.Count);
        Assert.Equal(6, bin.TotalSize);
    }

    [Fact]
    public void Subfolders_and_files_are_sorted_by_name()
    {
        var root = Build(
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
        var root = IgnoredTreeBuilder.Build(
            RepoRoot,
            files: [new IgnoredFileEntry("app.log", 10)],
            fullyIgnoredDirectories: [new IgnoredDirectoryEntry("node_modules", 5000, 42)]);

        var nodeModules = Sub(root, "node_modules");
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
        var root = IgnoredTreeBuilder.Build(
            RepoRoot,
            files: [],
            fullyIgnoredDirectories: [new IgnoredDirectoryEntry("tools/node_modules", 200, 3)]);

        var tools = Assert.Single(root.Subfolders);
        Assert.False(tools.IsFullyIgnored);
        Assert.Equal(3, tools.TotalFileCount);

        var nodeModules = Assert.Single(tools.Subfolders);
        Assert.True(nodeModules.IsFullyIgnored);
        Assert.Equal(200, nodeModules.TotalSize);
    }

    [Fact]
    public void Folder_names_differing_only_by_case_merge_into_one_folder()
    {
        var root = Build(
            new IgnoredFileEntry("Build/a.bin", 5),
            new IgnoredFileEntry("build/b.bin", 7));

        var build = Assert.Single(root.Subfolders);
        Assert.Equal(2, build.Files.Count);
        Assert.Equal(12, build.TotalSize);
    }
}
