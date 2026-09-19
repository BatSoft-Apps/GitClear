using GitClear.Core.Discovery;
using GitClear.Core.Model;
using GitClear.Core.Tests.TestSupport;

namespace GitClear.Core.Tests.Discovery;

public sealed class RepositoryDiscoveryServiceTests
{
    private readonly RepositoryDiscoveryService _discovery = new();

    private async Task<List<RepositoryInfo>> DiscoverAll(string root, CancellationToken cancellationToken = default)
    {
        List<RepositoryInfo> results = [];
        await foreach (RepositoryInfo repository in _discovery.DiscoverAsync(root, cancellationToken))
        {
            results.Add(repository);
        }

        return results;
    }

    [Fact]
    public async Task Finds_a_repository_with_a_dot_git_folder()
    {
        using TemporaryDirectory directory = new();
        string repositoryPath = directory.MakeRepository("project");

        List<RepositoryInfo> found = await DiscoverAll(directory.Path);

        RepositoryInfo only = Assert.Single(found);
        Assert.Equal(repositoryPath, only.FullPath);
        Assert.Equal("project", only.Name);
        Assert.False(only.IsWorktreeOrSubmodule);
    }

    [Fact]
    public async Task Detects_a_dot_git_file_as_worktree_or_submodule()
    {
        using TemporaryDirectory directory = new();
        directory.MakeGitFileRepository("worktree");

        List<RepositoryInfo> found = await DiscoverAll(directory.Path);

        RepositoryInfo only = Assert.Single(found);
        Assert.True(only.IsWorktreeOrSubmodule);
    }

    [Fact]
    public async Task Finds_multiple_sibling_repositories()
    {
        using TemporaryDirectory directory = new();
        directory.MakeRepository("a");
        directory.MakeRepository("b");
        directory.MakeRepository("nested", "c");

        List<RepositoryInfo> found = await DiscoverAll(directory.Path);

        Assert.Equal(3, found.Count);
        Assert.Equal(new[] { "a", "b", "c" }, found.Select(r => r.Name).OrderBy(n => n).ToArray());
    }

    [Fact]
    public async Task Does_not_descend_into_a_discovered_repository()
    {
        using TemporaryDirectory directory = new();
        string outer = directory.MakeRepository("outer");
        // A repository nested inside another repository must NOT be surfaced (DISC-2).
        Directory.CreateDirectory(Path.Combine(outer, "vendor", "inner"));
        Directory.CreateDirectory(Path.Combine(outer, "vendor", "inner", ".git"));

        List<RepositoryInfo> found = await DiscoverAll(directory.Path);

        RepositoryInfo only = Assert.Single(found);
        Assert.Equal(outer, only.FullPath);
    }

    [Fact]
    public async Task Returns_the_root_itself_when_it_is_a_repository()
    {
        using TemporaryDirectory directory = new();
        Directory.CreateDirectory(Path.Combine(directory.Path, ".git"));

        List<RepositoryInfo> found = await DiscoverAll(directory.Path);

        RepositoryInfo only = Assert.Single(found);
        Assert.Equal(directory.Path, only.FullPath);
    }

    [Fact]
    public async Task Returns_empty_when_no_repositories_present()
    {
        using TemporaryDirectory directory = new();
        directory.CreateDirectory("just", "some", "folders");
        directory.CreateFile("readme.txt", "no repos here");

        List<RepositoryInfo> found = await DiscoverAll(directory.Path);

        Assert.Empty(found);
    }

    [Fact]
    public async Task Throws_when_root_does_not_exist()
    {
        string missing = Path.Combine(Path.GetTempPath(), "gitclear-tests", Guid.NewGuid().ToString("N"));

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
        using TemporaryDirectory directory = new();
        directory.MakeRepository("a");
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => DiscoverAll(directory.Path, cancellation.Token));
    }
}
