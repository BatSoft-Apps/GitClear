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
        var repoA = RepositoryInfo.Create(@"C:\root\a", isWorktreeOrSubmodule: false);
        var repoB = RepositoryInfo.Create(@"C:\root\b", isWorktreeOrSubmodule: false);
        var sut = Sut.Create(discovery: new FakeDiscovery(repoA, repoB));

        await sut.BrowseForFolderCommand.ExecuteAsync(null);

        Assert.Equal(@"C:\root", sut.RootPath);
        Assert.Equal(new[] { repoA, repoB }, sut.Repositories);
        Assert.Contains("Found 2 repositories", sut.StatusMessage);
    }

    [Fact]
    public async Task Selecting_a_repository_builds_the_sized_tree()
    {
        var scanner = new FakeScanner(Sut.Result(
            new IgnoredFileEntry("app.log", 10),
            new IgnoredFileEntry("build/out.bin", 100)));
        var sut = Sut.Create(scanner: scanner);

        sut.SelectedRepository = RepositoryInfo.Create(@"C:\root\a", false);
        await sut.ActiveScan;

        var root = Assert.Single(sut.RootNodes);
        Assert.Equal("110 bytes", root.FormattedSize);
        Assert.Equal(2, root.TotalFileCount);
        Assert.Contains("2 ignored files", sut.StatusMessage);
    }

    [Fact]
    public async Task Scan_selects_the_root_folder_and_exposes_its_files()
    {
        var scanner = new FakeScanner(Sut.Result(
            new IgnoredFileEntry("app.log", 10),
            new IgnoredFileEntry("build/out.bin", 100)));
        var sut = Sut.Create(scanner: scanner);

        sut.SelectedRepository = RepositoryInfo.Create(@"C:\root\a", false);
        await sut.ActiveScan;

        Assert.NotNull(sut.SelectedFolder);
        Assert.True(sut.SelectedFolder!.IsSelected);
        Assert.True(sut.SelectedFolder.IsExpanded);
        var file = Assert.Single(sut.SelectedFolder.Files);
        Assert.Equal("app.log", file.Name);
        var subfolder = Assert.Single(sut.SelectedFolder.Subfolders);
        Assert.Equal("build", subfolder.Name);
    }

    [Fact]
    public async Task A_repository_with_no_ignored_files_reports_so()
    {
        var sut = Sut.Create(scanner: new FakeScanner(Sut.Result()));

        sut.SelectedRepository = RepositoryInfo.Create(@"C:\root\a", false);
        await sut.ActiveScan;

        Assert.Empty(sut.RootNodes);
        Assert.Contains("No ignored files", sut.StatusMessage);
    }

    [Fact]
    public async Task Git_not_found_sets_a_helpful_status()
    {
        var sut = Sut.Create(scanner: new ThrowingScanner(new GitNotFoundException("missing")));

        sut.SelectedRepository = RepositoryInfo.Create(@"C:\root\a", false);
        await sut.ActiveScan;

        Assert.Empty(sut.RootNodes);
        Assert.Contains("Git was not found", sut.StatusMessage);
    }

    [Fact]
    public async Task A_git_failure_shows_gits_own_message_flattened_onto_one_line()
    {
        // Git's real diagnostic, complete with newlines and indentation.
        GitCommandException failure = new(
            "git ls-files failed (exit code 128): ...",
            exitCode: 128,
            standardError: "fatal: detected dubious ownership in repository at '//VBoxSvr/Claude/GitClear'\n\n  To add an exception, call:\n\n\tgit config --global --add safe.directory ...");
        MainViewModel sut = Sut.Create(scanner: new ThrowingScanner(failure));

        sut.SelectedRepository = RepositoryInfo.Create(@"C:\root\a", false);
        await sut.ActiveScan;

        Assert.Contains("Git says: fatal: detected dubious ownership", sut.StatusMessage);
        Assert.DoesNotContain("\n", sut.StatusMessage);
        Assert.DoesNotContain("\t", sut.StatusMessage);
        // The user should not be told to go and run git themselves.
        Assert.DoesNotContain("exit code 128", sut.StatusMessage);
    }

    [Fact]
    public async Task A_git_failure_with_no_output_falls_back_to_the_exit_code()
    {
        MainViewModel sut = Sut.Create(scanner: new ThrowingScanner(
            new GitCommandException("failed", exitCode: 9, standardError: "   ")));

        sut.SelectedRepository = RepositoryInfo.Create(@"C:\root\a", false);
        await sut.ActiveScan;

        Assert.Contains("git exit code 9", sut.StatusMessage);
    }

    [Fact]
    public async Task Deselecting_a_repository_clears_the_tree()
    {
        var scanner = new FakeScanner(Sut.Result(new IgnoredFileEntry("app.log", 10)));
        var sut = Sut.Create(scanner: scanner);

        sut.SelectedRepository = RepositoryInfo.Create(@"C:\root\a", false);
        await sut.ActiveScan;
        Assert.Single(sut.RootNodes);

        sut.SelectedRepository = null;
        await sut.ActiveScan;

        Assert.Empty(sut.RootNodes);
        Assert.Null(sut.SelectedFolder);
    }
}
