using GitClear.Core.Git;
using GitClear.Core.Scanning;
using GitClear.Core.Tests.TestSupport;

namespace GitClear.Core.Tests.Scanning;

public sealed class GitIgnoredFileScannerTests
{
    private readonly GitIgnoredFileScanner _sut = new(new GitCommandLineClient());

    [Fact]
    public async Task Builds_a_sized_tree_from_ignored_files()
    {
        using var repo = new TempGitRepo();
        repo.WriteText(".gitignore", "*.log\nbuild/\n");
        repo.WriteFile("app.log", byteLength: 10);
        repo.WriteFile("build/out.bin", byteLength: 100);
        repo.WriteFile("build/sub/deep.bin", byteLength: 1000);
        repo.StageAndCommit(".gitignore");

        var result = await _sut.ScanAsync(repo.Path);

        Assert.Equal(1110, result.TotalSize);
        Assert.Equal(3, result.TotalFileCount);
        Assert.Equal(0, result.UnreadableFileCount);

        var build = result.Root.Subfolders.Single(f => f.Name == "build");
        Assert.Equal(1100, build.TotalSize);
        Assert.Equal(2, build.TotalFileCount);

        var appLog = result.Root.Files.Single(f => f.Name == "app.log");
        Assert.Equal(10, appLog.Size);
    }

    [Fact]
    public async Task Collapses_a_wholly_ignored_directory_into_a_single_sized_node()
    {
        using var repo = new TempGitRepo();
        repo.WriteText(".gitignore", "node_modules/\n");
        repo.WriteFile("node_modules/a.js", byteLength: 100);
        repo.WriteFile("node_modules/pkg/b.js", byteLength: 200);
        repo.StageAndCommit(".gitignore");

        var result = await _sut.ScanAsync(repo.Path);

        var node = Assert.Single(result.Root.Subfolders);
        Assert.Equal("node_modules", node.Name);
        Assert.True(node.IsFullyIgnored);
        Assert.Empty(node.Subfolders); // collapsed, not enumerated into the tree
        Assert.Equal(300, node.TotalSize);
        Assert.Equal(2, node.TotalFileCount);
    }

    [Fact]
    public async Task Empty_repo_yields_an_empty_result()
    {
        using var repo = new TempGitRepo();
        repo.WriteText("readme.txt", "untracked but not ignored");

        var result = await _sut.ScanAsync(repo.Path);

        Assert.Equal(0, result.TotalFileCount);
        Assert.Equal(0, result.TotalSize);
        Assert.Empty(result.Root.Files);
        Assert.Empty(result.Root.Subfolders);
    }

    [Fact]
    public async Task Reports_progress_ending_at_completion()
    {
        using var repo = new TempGitRepo();
        repo.WriteText(".gitignore", "*.log\n");
        repo.WriteFile("a.log", byteLength: 1);
        repo.WriteFile("b.log", byteLength: 1);
        repo.StageAndCommit(".gitignore");

        var progress = new RecordingProgress<ScanProgress>();
        await _sut.ScanAsync(repo.Path, progress);

        // Fewer files than the report interval → one final report at completion.
        var last = Assert.Single(progress.Reports);
        Assert.Equal(2, last.FilesProcessed);
    }

    [Fact]
    public async Task Throws_when_repository_folder_missing()
    {
        var missing = Path.Combine(Path.GetTempPath(), "gitclear-tests", Guid.NewGuid().ToString("N"));

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => _sut.ScanAsync(missing));
    }

    /// <summary>Captures progress reports synchronously (unlike <see cref="Progress{T}"/>).</summary>
    private sealed class RecordingProgress<T> : IProgress<T>
    {
        public List<T> Reports { get; } = [];

        public void Report(T value) => Reports.Add(value);
    }
}
