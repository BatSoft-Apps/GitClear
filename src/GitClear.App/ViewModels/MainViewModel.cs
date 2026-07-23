using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitClear.App.Formatting;
using GitClear.App.Services;
using GitClear.Core.Deletion;
using GitClear.Core.Discovery;
using GitClear.Core.Git;
using GitClear.Core.Model;
using GitClear.Core.Scanning;

namespace GitClear.App.ViewModels;

/// <summary>
/// Root view model for the main window. Owns folder selection, repository
/// discovery, the ignored-file scan, tri-state selection, deletion to the
/// Recycle Bin, and single-level Undo.
/// </summary>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IRepositoryDiscoveryService _discovery;
    private readonly IFolderPickerService _folderPicker;
    private readonly IIgnoredFileScanner _scanner;
    private readonly IDeletionService _deletion;
    private readonly IConfirmationDialog _confirmation;

    private CancellationTokenSource? _discoveryCts;
    private CancellationTokenSource? _scanCts;
    private CancellationTokenSource? _deleteCts;
    private Task _activeScan = Task.CompletedTask;

    // Single-level undo of the most recent deletion; cleared on repo change/close.
    private List<string> _lastDeletedTargets = [];
    private int _lastDeletedFileCount;

    public MainViewModel(
        IRepositoryDiscoveryService discovery,
        IFolderPickerService folderPicker,
        IIgnoredFileScanner scanner,
        IDeletionService deletion,
        IConfirmationDialog confirmation)
    {
        _discovery = discovery;
        _folderPicker = folderPicker;
        _scanner = scanner;
        _deletion = deletion;
        _confirmation = confirmation;

        Selection.PropertyChanged += OnSelectionChanged;
    }

    private void OnSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SelectionTracker.HasSelection))
        {
            DeleteSelectedCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>Repositories found under <see cref="RootPath"/>, in discovery order.</summary>
    public ObservableCollection<RepositoryInfo> Repositories { get; } = [];

    /// <summary>Top of the ignored-file tree (a single repo-root node) for the tree view.</summary>
    public ObservableCollection<FolderNodeViewModel> RootNodes { get; } = [];

    /// <summary>Running total of checked items for deletion (UI-3).</summary>
    public SelectionTracker Selection { get; } = new();

    /// <summary>The folder whose files are shown in the right-hand list (bound from the tree).</summary>
    [ObservableProperty]
    private FolderNodeViewModel? _selectedFolder;

    [ObservableProperty]
    private string _title = "GitClear";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FindRepositoriesCommand))]
    private string? _rootPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    [NotifyCanExecuteChangedFor(nameof(FindRepositoriesCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(UndoCommand))]
    private bool _isDiscovering;

    [ObservableProperty]
    private RepositoryInfo? _selectedRepository;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(UndoCommand))]
    private bool _isScanning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    [NotifyCanExecuteChangedFor(nameof(FindRepositoriesCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(UndoCommand))]
    private bool _isDeleting;

    /// <summary>True while any long-running operation (discovery, scan, delete) is active.</summary>
    public bool IsBusy => IsDiscovering || IsScanning || IsDeleting;

    /// <summary>Result of the most recent scan; consumed by the tree UI.</summary>
    [ObservableProperty]
    private IgnoredScanResult? _scanResult;

    [ObservableProperty]
    private string _statusMessage = "Select a folder to scan for Git repositories.";

    // ---- Discovery --------------------------------------------------------

    /// <summary>Opens the folder picker and, if a folder is chosen, searches it.</summary>
    [RelayCommand]
    private async Task BrowseForFolderAsync()
    {
        var picked = _folderPicker.PickFolder(RootPath);
        if (picked is null)
        {
            return;
        }

        RootPath = picked;
        if (FindRepositoriesCommand.CanExecute(null))
        {
            await FindRepositoriesCommand.ExecuteAsync(null);
        }
    }

    private bool CanFindRepositories() =>
        !IsDiscovering && !IsDeleting && !string.IsNullOrWhiteSpace(RootPath);

    [RelayCommand(CanExecute = nameof(CanFindRepositories))]
    private async Task FindRepositoriesAsync()
    {
        Repositories.Clear();
        SelectedRepository = null;

        _discoveryCts = new CancellationTokenSource();
        IsDiscovering = true;
        StatusMessage = "Searching for Git repositories…";

        try
        {
            await foreach (var repo in _discovery.DiscoverAsync(RootPath!, _discoveryCts.Token))
            {
                Repositories.Add(repo);
                StatusMessage = DescribeDiscovery(Repositories.Count, searching: true);
            }

            StatusMessage = Repositories.Count == 0
                ? "No Git repositories found under the selected folder."
                : DescribeDiscovery(Repositories.Count, searching: false);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = $"Search cancelled — {DescribeDiscovery(Repositories.Count, searching: false)}";
        }
#pragma warning disable CA1031 // UI-boundary safety net: never let discovery fail silently.
        catch (Exception ex)
        {
            StatusMessage = $"Could not search that folder: {ex.Message}";
        }
#pragma warning restore CA1031
        finally
        {
            IsDiscovering = false;
            _discoveryCts.Dispose();
            _discoveryCts = null;
        }
    }

    // ---- Stop (unified cancel) --------------------------------------------

    private bool CanStop() => IsDiscovering || IsScanning || IsDeleting;

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop()
    {
        _discoveryCts?.Cancel();
        _scanCts?.Cancel();
        _deleteCts?.Cancel();
    }

    // ---- Scan -------------------------------------------------------------

    /// <summary>The in-flight (or last) scan task. Exposed for tests to await.</summary>
    internal Task ActiveScan => _activeScan;

    partial void OnSelectedRepositoryChanged(RepositoryInfo? value)
    {
        ClearUndo(); // Undo is forgotten when the repository changes.
        _activeScan = ScanRepositoryAsync(value);
    }

    partial void OnScanResultChanged(IgnoredScanResult? value)
    {
        RootNodes.Clear();
        SelectedFolder = null;
        Selection.Reset();

        // Nothing ignored → leave the tree empty; the status line explains why.
        if (value is null || value.TotalFileCount == 0)
        {
            return;
        }

        var root = new FolderNodeViewModel(value.Root, Selection) { IsExpanded = true };
        RootNodes.Add(root);

        // Show the repo root's files immediately and highlight it in the tree.
        SelectedFolder = root;
        root.IsSelected = true;
    }

    private async Task ScanRepositoryAsync(RepositoryInfo? repository)
    {
        // Supersede any in-flight scan; the superseded call will observe cancellation.
        _scanCts?.Cancel();

        ScanResult = null;

        if (repository is null)
        {
            return;
        }

        var cts = new CancellationTokenSource();
        _scanCts = cts;
        IsScanning = true;
        StatusMessage = $"Scanning “{repository.Name}” for ignored files…";

        try
        {
            // No progress→status coupling: progress callbacks are delivered
            // asynchronously and could clobber a later status. The busy bar
            // signals activity instead.
            var result = await _scanner.ScanAsync(repository.FullPath, progress: null, cts.Token);
            ScanResult = result;
            StatusMessage = DescribeScan(result);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer selection; that scan now owns the UI state.
        }
        catch (GitNotFoundException)
        {
            StatusMessage = "Git was not found on your PATH. Install Git to scan repositories.";
        }
        catch (GitCommandException ex)
        {
            StatusMessage = $"Git could not scan this repository: {ex.Message}";
        }
#pragma warning disable CA1031 // UI-boundary safety net: fire-and-forget scan must never fault unobserved.
        catch (Exception ex)
        {
            StatusMessage = $"Could not scan this repository: {ex.Message}";
        }
#pragma warning restore CA1031
        finally
        {
            if (ReferenceEquals(cts, _scanCts))
            {
                IsScanning = false;
                _scanCts = null;
            }

            cts.Dispose();
        }
    }

    // ---- Deletion ---------------------------------------------------------

    private bool CanDeleteSelected() =>
        Selection.HasSelection && !IsDeleting && !IsScanning && !IsDiscovering;

    [RelayCommand(CanExecute = nameof(CanDeleteSelected))]
    private async Task DeleteSelectedAsync()
    {
        var repository = SelectedRepository;
        if (repository is null || RootNodes.Count == 0)
        {
            return;
        }

        var targets = RootNodes[0].EnumerateDeletionTargets().ToList();
        if (targets.Count == 0)
        {
            return;
        }

        var fileCount = Selection.SelectedFileCount;
        var noun = fileCount == 1 ? "file" : "files";

        var confirmed = _confirmation.Confirm(
            "Move to Recycle Bin",
            $"Move {fileCount:N0} ignored {noun} ({Selection.FormattedSelectedSize}) to the Recycle Bin?\n\n" +
            "You can restore them with Undo, or from the Recycle Bin.");
        if (!confirmed)
        {
            return;
        }

        _deleteCts = new CancellationTokenSource();
        IsDeleting = true;
        StatusMessage = $"Moving {fileCount:N0} {noun} to the Recycle Bin…";

        try
        {
            await _deletion.DeleteAsync(targets, _deleteCts.Token);

            _lastDeletedTargets = targets;
            _lastDeletedFileCount = fileCount;
            UndoCommand.NotifyCanExecuteChanged();

            // Refresh so the tree reflects reality and the selection resets (DEL-3).
            await ScanRepositoryAsync(repository);

            StatusMessage = $"Moved {fileCount:N0} {noun} to the Recycle Bin. Use Undo to restore.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Deletion cancelled.";
        }
        catch (DeletionException ex)
        {
            StatusMessage = $"Some items could not be deleted: {ex.Message}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusMessage = $"Deletion failed: {ex.Message}";
        }
#pragma warning disable CA1031 // UI-boundary safety net: never let deletion fail silently.
        catch (Exception ex)
        {
            StatusMessage = $"Deletion failed: {ex.Message}";
        }
#pragma warning restore CA1031
        finally
        {
            _deleteCts?.Dispose();
            _deleteCts = null;
            IsDeleting = false;
        }
    }

    // ---- Undo -------------------------------------------------------------

    private bool CanUndo() =>
        _lastDeletedTargets.Count > 0 && !IsDeleting && !IsScanning && !IsDiscovering;

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private async Task UndoAsync()
    {
        var targets = _lastDeletedTargets;
        var fileCount = _lastDeletedFileCount;
        if (targets.Count == 0)
        {
            return;
        }

        var repository = SelectedRepository;
        IsDeleting = true;
        StatusMessage = "Restoring from the Recycle Bin…";

        try
        {
            var restored = await _deletion.RestoreAsync(targets);
            ClearUndo();

            if (repository is not null)
            {
                await ScanRepositoryAsync(repository);
            }

            var noun = fileCount == 1 ? "file" : "files";
            StatusMessage = restored > 0
                ? $"Restored {fileCount:N0} {noun} from the Recycle Bin."
                : "Nothing could be restored automatically — check the Recycle Bin.";
        }
        catch (DeletionException ex)
        {
            StatusMessage = $"Undo failed: {ex.Message}";
        }
#pragma warning disable CA1031 // UI-boundary safety net: never let undo fail silently.
        catch (Exception ex)
        {
            StatusMessage = $"Undo failed: {ex.Message}";
        }
#pragma warning restore CA1031
        finally
        {
            IsDeleting = false;
        }
    }

    private void ClearUndo()
    {
        if (_lastDeletedTargets.Count == 0)
        {
            return;
        }

        _lastDeletedTargets = [];
        _lastDeletedFileCount = 0;
        UndoCommand.NotifyCanExecuteChanged();
    }

    // ---- Helpers ----------------------------------------------------------

    private static string DescribeDiscovery(int count, bool searching)
    {
        var noun = count == 1 ? "repository" : "repositories";
        return searching ? $"Found {count} {noun}…" : $"Found {count} {noun}.";
    }

    private static string DescribeScan(IgnoredScanResult result)
    {
        if (result.TotalFileCount == 0)
        {
            return "No ignored files found in this repository.";
        }

        var files = result.TotalFileCount == 1 ? "file" : "files";
        var unreadable = result.UnreadableFileCount > 0
            ? $" · {result.UnreadableFileCount} unreadable"
            : string.Empty;
        return $"{result.TotalFileCount:N0} ignored {files} · {ByteSize.Format(result.TotalSize)}{unreadable}";
    }

    public void Dispose()
    {
        Selection.PropertyChanged -= OnSelectionChanged;
        _discoveryCts?.Dispose();
        _discoveryCts = null;
        _scanCts?.Dispose();
        _scanCts = null;
        _deleteCts?.Dispose();
        _deleteCts = null;
    }
}
