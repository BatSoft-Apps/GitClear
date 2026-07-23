using System.Runtime.CompilerServices;
using GitClear.App.Services;
using GitClear.App.ViewModels;
using GitClear.Core.Deletion;
using GitClear.Core.Discovery;
using GitClear.Core.Model;
using GitClear.Core.Scanning;

namespace GitClear.App.Tests.TestSupport;

/// <summary>Builds a <see cref="MainViewModel"/> with test doubles for its dependencies.</summary>
internal static class Sut
{
    public static IgnoredScanResult Result(params IgnoredFileEntry[] entries) => new()
    {
        Root = IgnoredTreeBuilder.Build(@"C:\repo", entries),
        UnreadableFileCount = 0,
    };

    public static IgnoredScanResult ResultWith(IgnoredFileEntry[] files, IgnoredDirectoryEntry[] directories) => new()
    {
        Root = IgnoredTreeBuilder.Build(@"C:\repo", files, directories),
        UnreadableFileCount = 0,
    };

    public static MainViewModel Create(
        IRepositoryDiscoveryService? discovery = null,
        IFolderPickerService? picker = null,
        IIgnoredFileScanner? scanner = null,
        IDeletionService? deletion = null,
        IConfirmationDialog? confirmation = null) =>
        new(
            discovery ?? new FakeDiscovery(),
            picker ?? new StubFolderPicker(@"C:\root"),
            scanner ?? new FakeScanner(Result()),
            deletion ?? new FakeDeletionService(),
            confirmation ?? new ConfirmationStub(true));
}

internal sealed class FakeDiscovery(params RepositoryInfo[] repositories) : IRepositoryDiscoveryService
{
    public async IAsyncEnumerable<RepositoryInfo> DiscoverAsync(
        string rootPath,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var repo in repositories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return repo;
            await Task.Yield();
        }
    }
}

internal sealed class StubFolderPicker(string? folder) : IFolderPickerService
{
    public string? PickFolder(string? initialFolder = null) => folder;
}

internal sealed class FakeScanner(IgnoredScanResult result) : IIgnoredFileScanner
{
    public Task<IgnoredScanResult> ScanAsync(
        string repositoryPath,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(new ScanProgress(1));
        return Task.FromResult(result);
    }
}

/// <summary>Returns each queued result in turn; the last repeats. Models a refresh re-scan.</summary>
internal sealed class QueueScanner(params IgnoredScanResult[] results) : IIgnoredFileScanner
{
    private int _index;

    public Task<IgnoredScanResult> ScanAsync(
        string repositoryPath,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = results[Math.Min(_index, results.Length - 1)];
        _index++;
        progress?.Report(new ScanProgress(1));
        return Task.FromResult(result);
    }
}

internal sealed class ThrowingScanner(Exception exception) : IIgnoredFileScanner
{
    public Task<IgnoredScanResult> ScanAsync(
        string repositoryPath,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        Task.FromException<IgnoredScanResult>(exception);
}

internal sealed class FakeDeletionService(DeletionResult? result = null, Exception? throwOnDelete = null)
    : IDeletionService
{
    public List<string> ReceivedPaths { get; } = [];

    public List<string> RestoredPaths { get; } = [];

    public Task<DeletionResult> DeleteAsync(
        IReadOnlyCollection<string> filePaths,
        CancellationToken cancellationToken = default)
    {
        ReceivedPaths.AddRange(filePaths);

        if (throwOnDelete is not null)
        {
            return Task.FromException<DeletionResult>(throwOnDelete);
        }

        return Task.FromResult(result ?? new DeletionResult(filePaths.Count, 0));
    }

    public Task<int> RestoreAsync(
        IReadOnlyCollection<string> originalPaths,
        CancellationToken cancellationToken = default)
    {
        RestoredPaths.AddRange(originalPaths);
        return Task.FromResult(originalPaths.Count);
    }
}

internal sealed class ConfirmationStub(bool result) : IConfirmationDialog
{
    public int Calls { get; private set; }

    public string? LastMessage { get; private set; }

    public bool Confirm(string title, string message)
    {
        Calls++;
        LastMessage = message;
        return result;
    }
}
