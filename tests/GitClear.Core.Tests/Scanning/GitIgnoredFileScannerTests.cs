using GitClear.Core.Git;
using GitClear.Core.Model;
using GitClear.Core.Scanning;
using GitClear.Core.Tests.TestSupport;

namespace GitClear.Core.Tests.Scanning;

public sealed class GitIgnoredFileScannerTests
{
    private readonly GitIgnoredFileScanner _scanner = new(new GitCommandLineClient());

    [Fact]
    public async Task Builds_a_sized_tree_from_ignored_files()
    {
        using TemporaryGitRepository repository = new();
        repository.WriteText(".gitignore", "*.log\nbuild/\n");
        repository.WriteFile("app.log", byteLength: 10);
        repository.WriteFile("build/out.bin", byteLength: 100);
        repository.WriteFile("build/sub/deep.bin", byteLength: 1000);
        repository.StageAndCommit(".gitignore");

        IgnoredScanResult result = await _scanner.ScanAsync(repository.Path);

        Assert.Equal(1110, result.TotalSize);
        Assert.Equal(3, result.TotalFileCount);
        Assert.Equal(0, result.UnreadableFileCount);

        IgnoredFolderNode build = result.Root.Subfolders.Single(f => f.Name == "build");
        Assert.Equal(1100, build.TotalSize);
        Assert.Equal(2, build.TotalFileCount);

        IgnoredFileNode appLog = result.Root.Files.Single(f => f.Name == "app.log");
        Assert.Equal(10, appLog.Size);
    }

    [Fact]
    public async Task Collapses_a_wholly_ignored_directory_into_a_single_sized_node()
    {
        using TemporaryGitRepository repository = new();
        repository.WriteText(".gitignore", "node_modules/\n");
        repository.WriteFile("node_modules/a.js", byteLength: 100);
        repository.WriteFile("node_modules/pkg/b.js", byteLength: 200);
        repository.StageAndCommit(".gitignore");

        IgnoredScanResult result = await _scanner.ScanAsync(repository.Path);

        IgnoredFolderNode node = Assert.Single(result.Root.Subfolders);
        Assert.Equal("node_modules", node.Name);
        Assert.True(node.IsFullyIgnored);
        Assert.Empty(node.Subfolders); // collapsed, not enumerated into the tree
        Assert.Equal(300, node.TotalSize);
        Assert.Equal(2, node.TotalFileCount);
    }

    [Fact]
    public async Task Empty_repository_yields_an_empty_result()
    {
        using TemporaryGitRepository repository = new();
        repository.WriteText("readme.txt", "untracked but not ignored");

        IgnoredScanResult result = await _scanner.ScanAsync(repository.Path);

        Assert.Equal(0, result.TotalFileCount);
        Assert.Equal(0, result.TotalSize);
        Assert.Empty(result.Root.Files);
        Assert.Empty(result.Root.Subfolders);
    }

    [Fact]
    public async Task Reports_progress_ending_at_completion()
    {
        using TemporaryGitRepository repository = new();
        repository.WriteText(".gitignore", "*.log\n");
        repository.WriteFile("a.log", byteLength: 1);
        repository.WriteFile("b.log", byteLength: 1);
        repository.StageAndCommit(".gitignore");

        RecordingProgress<ScanProgress> progress = new();
        await _scanner.ScanAsync(repository.Path, progress);

        // Fewer files than the report interval → one final report at completion.
        ScanProgress last = Assert.Single(progress.Reports);
        Assert.Equal(2, last.FilesProcessed);
    }

    [Fact]
    public async Task Throws_when_repository_folder_missing()
    {
        string missing = Path.Combine(Path.GetTempPath(), "gitclear-tests", Guid.NewGuid().ToString("N"));

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => _scanner.ScanAsync(missing));
    }

    /// <summary>Captures progress reports synchronously (unlike <see cref="Progress{T}"/>).</summary>
    private sealed class RecordingProgress<T> : IProgress<T>
    {
        public List<T> Reports { get; } = [];

        public void Report(T value) => Reports.Add(value);
    }
}
