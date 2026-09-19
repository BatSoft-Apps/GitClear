using GitClear.Core.Git;
using GitClear.Core.Tests.TestSupport;

namespace GitClear.Core.Tests.Git;

public sealed class GitCommandLineClientTests
{
    private readonly GitCommandLineClient _client = new();

    [Fact]
    public async Task Lists_ignored_entries_honoring_nested_gitignore_and_collapsing_whole_directories()
    {
        using TemporaryGitRepository repository = new();
        repository.WriteText(".gitignore", "*.log\nbuild/\n");
        repository.WriteText("app.log", "ignored");
        repository.WriteText("build/out.bin", "ignored");
        repository.WriteText("nested/.gitignore", "*.tmp\n");
        repository.WriteText("nested/a.tmp", "ignored");
        repository.WriteText("tracked.txt", "tracked");
        repository.StageAndCommit(".gitignore", "tracked.txt", "nested/.gitignore");

        IReadOnlyList<string> paths = await _client.GetIgnoredPathsAsync(repository.Path);

        // git reports '/'-separated, repository-relative paths.
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
        using TemporaryGitRepository repository = new();
        repository.WriteText("readme.txt", "not ignored, just untracked");

        IReadOnlyList<string> paths = await _client.GetIgnoredPathsAsync(repository.Path);

        Assert.Empty(paths);
    }

    [Fact]
    public async Task Throws_when_path_is_blank()
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _client.GetIgnoredPathsAsync("  "));
    }
}
