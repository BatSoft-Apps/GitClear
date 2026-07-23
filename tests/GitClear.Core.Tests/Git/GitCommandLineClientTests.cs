using GitClear.Core.Git;
using GitClear.Core.Tests.TestSupport;

namespace GitClear.Core.Tests.Git;

public sealed class GitCommandLineClientTests
{
    private readonly GitCommandLineClient _sut = new();

    [Fact]
    public async Task Lists_ignored_entries_honoring_nested_gitignore_and_collapsing_whole_dirs()
    {
        using var repo = new TempGitRepo();
        repo.WriteText(".gitignore", "*.log\nbuild/\n");
        repo.WriteText("app.log", "ignored");
        repo.WriteText("build/out.bin", "ignored");
        repo.WriteText("nested/.gitignore", "*.tmp\n");
        repo.WriteText("nested/a.tmp", "ignored");
        repo.WriteText("tracked.txt", "tracked");
        repo.StageAndCommit(".gitignore", "tracked.txt", "nested/.gitignore");

        var paths = await _sut.GetIgnoredPathsAsync(repo.Path);

        // git reports '/'-separated, repo-relative paths.
        Assert.Contains("app.log", paths);
        // --directory collapses the wholly-ignored build/ folder to one entry (trailing slash).
        Assert.Contains("build/", paths);
        Assert.DoesNotContain("build/out.bin", paths);
        // nested/ is a mixed folder (has tracked content), so its ignored file is listed individually.
        Assert.Contains("nested/a.tmp", paths);
        // Tracked files are not "others" and must never appear.
        Assert.DoesNotContain("tracked.txt", paths);
    }

    [Fact]
    public async Task Returns_empty_when_nothing_is_ignored()
    {
        using var repo = new TempGitRepo();
        repo.WriteText("readme.txt", "not ignored, just untracked");

        var paths = await _sut.GetIgnoredPathsAsync(repo.Path);

        Assert.Empty(paths);
    }

    [Fact]
    public async Task Throws_when_path_is_blank()
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _sut.GetIgnoredPathsAsync("  "));
    }
}
