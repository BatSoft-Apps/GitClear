using GitClear.App.ViewModels;
using GitClear.Core.Model;
using GitClear.Core.Scanning;

namespace GitClear.App.Tests.ViewModels;

public sealed class SelectionTests
{
    private static (FolderNodeViewModel Root, SelectionTracker Tracker) BuildTree(params IgnoredFileEntry[] entries)
    {
        IgnoredFolderNode model = IgnoredTreeBuilder.Build(@"C:\repo", entries);
        SelectionTracker tracker = new();
        return (new FolderNodeViewModel(model, tracker), tracker);
    }

    private static FolderNodeViewModel Subfolder(FolderNodeViewModel folder, string name)
        => folder.Subfolders.Single(f => f.Name == name);

    [Fact]
    public void Checking_a_folder_checks_all_descendant_files()
    {
        (FolderNodeViewModel? root, SelectionTracker? tracker) = BuildTree(
            new IgnoredFileEntry("a.log", 10),
            new IgnoredFileEntry("build/one.bin", 100),
            new IgnoredFileEntry("build/sub/deep.bin", 1000));

        root.IsChecked = true;

        Assert.Equal(3, root.EnumerateDeletionTargets().Count());
        Assert.Equal(3, tracker.SelectedFileCount);
        Assert.Equal(1110, tracker.SelectedSize);
        Assert.True(Subfolder(root, "build").IsChecked);
    }

    [Fact]
    public void Unchecking_a_folder_clears_descendants_and_total()
    {
        (FolderNodeViewModel? root, SelectionTracker? tracker) = BuildTree(
            new IgnoredFileEntry("a.log", 10),
            new IgnoredFileEntry("build/one.bin", 100));

        root.IsChecked = true;
        root.IsChecked = false;

        Assert.Empty(root.EnumerateDeletionTargets());
        Assert.Equal(0, tracker.SelectedFileCount);
        Assert.Equal(0, tracker.SelectedSize);
    }

    [Fact]
    public void Checking_one_file_makes_ancestors_indeterminate()
    {
        (FolderNodeViewModel? root, SelectionTracker _) = BuildTree(
            new IgnoredFileEntry("build/one.bin", 100),
            new IgnoredFileEntry("build/two.bin", 200));

        FolderNodeViewModel build = Subfolder(root, "build");
        build.Files.Single(f => f.Name == "one.bin").IsChecked = true;

        Assert.Null(build.IsChecked);   // some but not all
        Assert.Null(root.IsChecked);    // indeterminate propagates up
    }

    [Fact]
    public void Checking_all_files_in_a_folder_makes_the_folder_checked()
    {
        (FolderNodeViewModel? root, SelectionTracker _) = BuildTree(
            new IgnoredFileEntry("build/one.bin", 100),
            new IgnoredFileEntry("build/two.bin", 200));

        FolderNodeViewModel build = Subfolder(root, "build");
        foreach (FileNodeViewModel file in build.Files)
        {
            file.IsChecked = true;
        }

        Assert.True(build.IsChecked);
        Assert.True(root.IsChecked); // build is root's only child
    }

    [Fact]
    public void Running_total_tracks_individual_file_toggles()
    {
        (FolderNodeViewModel? root, SelectionTracker? tracker) = BuildTree(
            new IgnoredFileEntry("a.bin", 100),
            new IgnoredFileEntry("b.bin", 200),
            new IgnoredFileEntry("c.bin", 300));

        root.Files.Single(f => f.Name == "a.bin").IsChecked = true;
        root.Files.Single(f => f.Name == "c.bin").IsChecked = true;
        Assert.Equal(2, tracker.SelectedFileCount);
        Assert.Equal(400, tracker.SelectedSize);

        root.Files.Single(f => f.Name == "a.bin").IsChecked = false;
        Assert.Equal(1, tracker.SelectedFileCount);
        Assert.Equal(300, tracker.SelectedSize);
    }

    [Fact]
    public void EnumerateDeletionTargets_returns_only_checked_files_across_the_subtree()
    {
        (FolderNodeViewModel? root, SelectionTracker _) = BuildTree(
            new IgnoredFileEntry("a.log", 10),
            new IgnoredFileEntry("build/one.bin", 100),
            new IgnoredFileEntry("build/two.bin", 200));

        Subfolder(root, "build").Files.Single(f => f.Name == "one.bin").IsChecked = true;

        string target = Assert.Single(root.EnumerateDeletionTargets());
        Assert.EndsWith("one.bin", target);
    }

    [Fact]
    public void Checking_a_fully_ignored_folder_selects_its_whole_size_as_one_target()
    {
        IgnoredFolderNode model = IgnoredTreeBuilder.Build(
            @"C:\repo",
            files: [],
            fullyIgnoredDirectories: [new IgnoredDirectoryEntry("node_modules", 5000, 42)]);
        SelectionTracker tracker = new();
        FolderNodeViewModel root = new(model, tracker);

        FolderNodeViewModel nodeModules = Assert.Single(root.Subfolders);
        Assert.True(nodeModules.IsFullyIgnored);

        nodeModules.IsChecked = true;

        // The whole directory counts as its full size / file count, but is a single delete target.
        Assert.Equal(42, tracker.SelectedFileCount);
        Assert.Equal(5000, tracker.SelectedSize);
        string target = Assert.Single(root.EnumerateDeletionTargets());
        Assert.EndsWith("node_modules", target);
    }

    [Fact]
    public void Toggling_a_folder_twice_leaves_the_total_balanced()
    {
        (FolderNodeViewModel? root, SelectionTracker? tracker) = BuildTree(
            new IgnoredFileEntry("x/a.bin", 100),
            new IgnoredFileEntry("x/b.bin", 100));

        for (int i = 0; i < 3; i++)
        {
            root.IsChecked = true;
            root.IsChecked = false;
        }

        Assert.Equal(0, tracker.SelectedFileCount);
        Assert.Equal(0, tracker.SelectedSize);
    }
}
