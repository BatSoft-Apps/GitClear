using GitClear.Core.Discovery;
using GitClear.Core.Model;
using GitClear.Core.Tests.TestSupport;

namespace GitClear.Core.Tests.Discovery;

public sealed class RepositoryDiscoveryServiceTests
{
    private readonly RepositoryDiscoveryService _sut = new();

    private async Task<List<RepositoryInfo>> DiscoverAll(string root, CancellationToken ct = default)
    {
        var results = new List<RepositoryInfo>();
        await foreach (var repo in _sut.DiscoverAsync(root, ct))
        {
            results.Add(repo);
        }

        return results;
    }

    [Fact]
    public async Task Finds_a_repo_with_a_dot_git_folder()
    {
        using var temp = new TempDirectory();
        var repo = temp.MakeRepo("project");

        var found = await DiscoverAll(temp.Path);

        var only = Assert.Single(found);
        Assert.Equal(repo, only.FullPath);
        Assert.Equal("project", only.Name);
        Assert.False(only.IsWorktreeOrSubmodule);
    }

    [Fact]
    public async Task Detects_a_dot_git_file_as_worktree_or_submodule()
    {
        using var temp = new TempDirectory();
        temp.MakeGitFileRepo("worktree");

        var found = await DiscoverAll(temp.Path);

        var only = Assert.Single(found);
        Assert.True(only.IsWorktreeOrSubmodule);
    }

    [Fact]
    public async Task Finds_multiple_sibling_repos()
    {
        using var temp = new TempDirectory();
        temp.MakeRepo("a");
        temp.MakeRepo("b");
        temp.MakeRepo("nested", "c");

        var found = await DiscoverAll(temp.Path);

        Assert.Equal(3, found.Count);
        Assert.Equal(new[] { "a", "b", "c" }, found.Select(r => r.Name).OrderBy(n => n).ToArray());
    }

    [Fact]
    public async Task Does_not_descend_into_a_discovered_repo()
    {
        using var temp = new TempDirectory();
        var outer = temp.MakeRepo("outer");
        // A repo nested inside another repo must NOT be surfaced (DISC-2).
        Directory.CreateDirectory(Path.Combine(outer, "vendor", "inner"));
        Directory.CreateDirectory(Path.Combine(outer, "vendor", "inner", ".git"));

        var found = await DiscoverAll(temp.Path);

        var only = Assert.Single(found);
        Assert.Equal(outer, only.FullPath);
    }

    [Fact]
    public async Task Returns_the_root_itself_when_it_is_a_repo()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, ".git"));

        var found = await DiscoverAll(temp.Path);

        var only = Assert.Single(found);
        Assert.Equal(temp.Path, only.FullPath);
    }

    [Fact]
    public async Task Returns_empty_when_no_repos_present()
    {
        using var temp = new TempDirectory();
        temp.CreateDir("just", "some", "folders");
        temp.CreateFile("readme.txt", "no repos here");

        var found = await DiscoverAll(temp.Path);

        Assert.Empty(found);
    }

    [Fact]
    public async Task Throws_when_root_does_not_exist()
    {
        var missing = Path.Combine(Path.GetTempPath(), "gitclear-tests", Guid.NewGuid().ToString("N"));

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => DiscoverAll(missing));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Throws_when_root_is_blank(string? root)
    {
        // null yields ArgumentNullException, blank yields ArgumentException — both derive from ArgumentException.
        await Assert.ThrowsAnyAsync<ArgumentException>(() => DiscoverAll(root!));
    }

    [Fact]
    public async Task Cancellation_stops_enumeration()
    {
        using var temp = new TempDirectory();
        temp.MakeRepo("a");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => DiscoverAll(temp.Path, cts.Token));
    }
}
