using GitClear.App.Tests.TestSupport;
using GitClear.App.ViewModels;
using GitClear.Core.Git;
using GitClear.Core.Model;
using GitClear.Core.Scanning;

namespace GitClear.App.Tests.ViewModels;

public sealed class MainViewModelTests
{
    [Fact]
    public async Task Browsing_a_folder_discovers_repositories()
    {
        RepositoryInfo repositoryA = new RepositoryInfo(@"C:\root\a", isWorktreeOrSubmodule: false);
        RepositoryInfo repositoryB = new RepositoryInfo(@"C:\root\b", isWorktreeOrSubmodule: false);
        MainViewModel viewModel = MainViewModelFactory.Create(discovery: new FakeDiscovery(repositoryA, repositoryB));

        await viewModel.BrowseForFolderCommand.ExecuteAsync(null);

        Assert.Equal(@"C:\root", viewModel.RootPath);
        Assert.Equal(new[] { repositoryA, repositoryB }, viewModel.Repositories);
        Assert.Contains("Found 2 repositories", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Selecting_a_repository_builds_the_sized_tree()
    {
        FakeScanner scanner = new(ScanResults.Of(
            new IgnoredFileEntry("app.log", 10),
            new IgnoredFileEntry("build/out.bin", 100)));
        MainViewModel viewModel = MainViewModelFactory.Create(scanner: scanner);

        viewModel.SelectedRepository = new RepositoryInfo(@"C:\root\a", false);
        await viewModel.ActiveScan;

        FolderNodeViewModel root = Assert.Single(viewModel.RootNodes);
        Assert.Equal("110 bytes", root.FormattedSize);
        Assert.Equal(2, root.TotalFileCount);
        Assert.Contains("2 ignored files", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Scan_selects_the_root_folder_and_exposes_its_files()
    {
        FakeScanner scanner = new(ScanResults.Of(
            new IgnoredFileEntry("app.log", 10),
            new IgnoredFileEntry("build/out.bin", 100)));
        MainViewModel viewModel = MainViewModelFactory.Create(scanner: scanner);

        viewModel.SelectedRepository = new RepositoryInfo(@"C:\root\a", false);
        await viewModel.ActiveScan;

        Assert.NotNull(viewModel.SelectedFolder);
        Assert.True(viewModel.SelectedFolder!.IsSelected);
        Assert.True(viewModel.SelectedFolder.IsExpanded);
        FileNodeViewModel file = Assert.Single(viewModel.SelectedFolder.Files);
        Assert.Equal("app.log", file.Name);
        FolderNodeViewModel subfolder = Assert.Single(viewModel.SelectedFolder.Subfolders);
        Assert.Equal("build", subfolder.Name);
    }

    [Fact]
    public async Task A_repository_with_no_ignored_files_reports_so()
    {
        MainViewModel viewModel = MainViewModelFactory.Create(scanner: new FakeScanner(ScanResults.Of()));

        viewModel.SelectedRepository = new RepositoryInfo(@"C:\root\a", false);
        await viewModel.ActiveScan;

        Assert.Empty(viewModel.RootNodes);
        Assert.Contains("No ignored files", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Git_not_found_sets_a_helpful_status()
    {
        MainViewModel viewModel = MainViewModelFactory.Create(scanner: new ThrowingScanner(new GitNotFoundException("missing")));

        viewModel.SelectedRepository = new RepositoryInfo(@"C:\root\a", false);
        await viewModel.ActiveScan;

        Assert.Empty(viewModel.RootNodes);
        Assert.Contains("Git was not found", viewModel.StatusMessage);
    }

    [Fact]
    public async Task A_git_failure_shows_gits_own_message_flattened_onto_one_line()
    {
        // Git's real diagnostic, complete with newlines and indentation.
        GitCommandException failure = new(
            "git ls-files failed (exit code 128): ...",
            exitCode: 128,
            standardError: "fatal: detected dubious ownership in repository at '//VBoxSvr/Claude/GitClear'\n\n  To add an exception, call:\n\n\tgit config --global --add safe.directory ...");
        MainViewModel viewModel = MainViewModelFactory.Create(scanner: new ThrowingScanner(failure));

        viewModel.SelectedRepository = new RepositoryInfo(@"C:\root\a", false);
        await viewModel.ActiveScan;

        Assert.Contains("Git says: fatal: detected dubious ownership", viewModel.StatusMessage);
        Assert.DoesNotContain("\n", viewModel.StatusMessage);
        Assert.DoesNotContain("\t", viewModel.StatusMessage);
        // The user should not be told to go and run git themselves.
        Assert.DoesNotContain("exit code 128", viewModel.StatusMessage);
    }

    [Fact]
    public async Task A_git_failure_with_no_output_falls_back_to_the_exit_code()
    {
        MainViewModel viewModel = MainViewModelFactory.Create(scanner: new ThrowingScanner(
            new GitCommandException("failed", exitCode: 9, standardError: "   ")));

        viewModel.SelectedRepository = new RepositoryInfo(@"C:\root\a", false);
        await viewModel.ActiveScan;

        Assert.Contains("git exit code 9", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Deselecting_a_repository_clears_the_tree()
    {
        FakeScanner scanner = new(ScanResults.Of(new IgnoredFileEntry("app.log", 10)));
        MainViewModel viewModel = MainViewModelFactory.Create(scanner: scanner);

        viewModel.SelectedRepository = new RepositoryInfo(@"C:\root\a", false);
        await viewModel.ActiveScan;
        Assert.Single(viewModel.RootNodes);

        viewModel.SelectedRepository = null;
        await viewModel.ActiveScan;

        Assert.Empty(viewModel.RootNodes);
        Assert.Null(viewModel.SelectedFolder);
    }
}
