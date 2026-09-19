using System.Runtime.CompilerServices;
using GitClear.App.Services;
using GitClear.App.ViewModels;
using GitClear.Core.Deletion;
using GitClear.Core.Discovery;
using GitClear.Core.Model;
using GitClear.Core.Scanning;

namespace GitClear.App.Tests.TestSupport;

/// <summary>Builds a <see cref="MainViewModel"/>, with a test double for each dependency not given.</summary>
internal static class MainViewModelFactory
{
    public static MainViewModel Create(
        IRepositoryDiscoveryService? discovery = null,
        IFolderPickerService? folderPicker = null,
        IIgnoredFileScanner? scanner = null,
        IDeletionService? deletion = null,
        IConfirmationDialog? confirmation = null,
        IUserGuideService? userGuide = null)
    {
        return new MainViewModel(
            discovery ?? new FakeDiscovery(),
            folderPicker ?? new StubFolderPicker(@"C:\root"),
            scanner ?? new FakeScanner(ScanResults.Of()),
            deletion ?? new FakeDeletionService(),
            confirmation ?? new StubConfirmation(true),
            userGuide ?? new StubUserGuide());
    }
}

/// <summary>Scan results for a repository at <see cref="RepositoryPath"/>.</summary>
internal static class ScanResults
{
    public const string RepositoryPath = @"C:\repo";

    public static IgnoredScanResult Of(params IgnoredFileEntry[] files) => Of(files, []);

    public static IgnoredScanResult Of(IgnoredFileEntry[] files, IgnoredDirectoryEntry[] directories)
        => new(IgnoredTreeBuilder.Build(RepositoryPath, files, directories), unreadableFileCount: 0);
}

internal sealed class FakeDiscovery(params RepositoryInfo[] repositories) : IRepositoryDiscoveryService
{
    public async IAsyncEnumerable<RepositoryInfo> DiscoverAsync(
        string rootPath,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (RepositoryInfo repository in repositories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return repository;
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
        IgnoredScanResult result = results[Math.Min(_index, results.Length - 1)];
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
        CancellationToken cancellationToken = default)
        => Task.FromException<IgnoredScanResult>(exception);
}

internal sealed class FakeDeletionService(DeletionResult? result = null, Exception? throwOnDelete = null)
    : IDeletionService
{
    /// <summary>Models the user declining a permanent-delete warning (DEL-5).</summary>
    public static FakeDeletionService Aborting() => new(new DeletionResult(0, 0, aborted: true));

    public List<string> ReceivedPaths { get; } = [];

    public List<string> RestoredPaths { get; } = [];

    public Task<DeletionResult> DeleteAsync(
        IReadOnlyCollection<string> targetPaths,
        CancellationToken cancellationToken = default)
    {
        ReceivedPaths.AddRange(targetPaths);

        if (throwOnDelete is not null)
        {
            return Task.FromException<DeletionResult>(throwOnDelete);
        }

        return Task.FromResult(result ?? new DeletionResult(targetPaths.Count, 0));
    }

    public Task<int> RestoreAsync(
        IReadOnlyCollection<string> originalPaths,
        CancellationToken cancellationToken = default)
    {
        RestoredPaths.AddRange(originalPaths);
        return Task.FromResult(originalPaths.Count);
    }
}

internal sealed class StubConfirmation(bool answer) : IConfirmationDialog
{
    public bool Confirm(string title, string message) => answer;
}

internal sealed class StubUserGuide(bool opens = true) : IUserGuideService
{
    public bool TryOpen() => opens;
}
