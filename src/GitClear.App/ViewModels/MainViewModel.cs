using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitClear.App.Services;
using GitClear.Core.Deletion;
using GitClear.Core.Discovery;
using GitClear.Core.Git;
using GitClear.Core.Model;
using GitClear.Core.Scanning;

namespace GitClear.App.ViewModels;

/// <summary>
/// Root view model for the main window: runs the find → scan → select → delete →
/// undo workflow. What it coordinates lives elsewhere — the tree and its running
/// total (<see cref="FolderNodeViewModel"/>, <see cref="SelectionTracker"/>), the
/// wording (<see cref="UserMessages"/>) and the work itself (the Core services).
/// </summary>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IRepositoryDiscoveryService _discovery;
    private readonly IFolderPickerService _folderPicker;
    private readonly IIgnoredFileScanner _scanner;
    private readonly IDeletionService _deletion;
    private readonly IConfirmationDialog _confirmation;
    private readonly IUserGuideService _userGuide;

    private readonly ObservableCollection<RepositoryInfo> _repositories = [];
    private readonly ObservableCollection<FolderNodeViewModel> _rootNodes = [];

    // Each operation creates, owns and disposes its own source; the field lets Stop reach it.
    private CancellationTokenSource? _discoveryCancellation;
    private CancellationTokenSource? _scanCancellation;
    private CancellationTokenSource? _deleteCancellation;
    private Task _activeScan = Task.CompletedTask;

    private bool _isDiscovering;
    private bool _isScanning;
    private bool _isDeleting;
    private string _statusMessage = UserMessages.Welcome;
    private LastDeletion? _undoableDeletion;

    public MainViewModel(
        IRepositoryDiscoveryService discovery,
        IFolderPickerService folderPicker,
        IIgnoredFileScanner scanner,
        IDeletionService deletion,
        IConfirmationDialog confirmation,
        IUserGuideService userGuide)
    {
        ArgumentNullException.ThrowIfNull(discovery);
        ArgumentNullException.ThrowIfNull(folderPicker);
        ArgumentNullException.ThrowIfNull(scanner);
        ArgumentNullException.ThrowIfNull(deletion);
        ArgumentNullException.ThrowIfNull(confirmation);
        ArgumentNullException.ThrowIfNull(userGuide);

        _discovery = discovery;
        _folderPicker = folderPicker;
        _scanner = scanner;
        _deletion = deletion;
        _confirmation = confirmation;
        _userGuide = userGuide;

        Repositories = new ReadOnlyObservableCollection<RepositoryInfo>(_repositories);
        RootNodes = new ReadOnlyObservableCollection<FolderNodeViewModel>(_rootNodes);

        Selection.PropertyChanged += OnSelectionChanged;
    }

    #region State

    public string Title { get; } = "GitClear";

    /// <summary>Repositories found under <see cref="RootPath"/>, in discovery order.</summary>
    public ReadOnlyObservableCollection<RepositoryInfo> Repositories { get; }

    /// <summary>Top of the ignored-file tree (a single repository-root node) for the tree view.</summary>
    public ReadOnlyObservableCollection<FolderNodeViewModel> RootNodes { get; }

    /// <summary>Running total of checked items for deletion (UI-3).</summary>
    public SelectionTracker Selection { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FindRepositoriesCommand))]
    private string? _rootPath;

    [ObservableProperty]
    private RepositoryInfo? _selectedRepository;

    /// <summary>The folder whose files are shown in the right-hand list (bound from the tree).</summary>
    [ObservableProperty]
    private FolderNodeViewModel? _selectedFolder;

    public bool IsDiscovering
    {
        get => _isDiscovering;
        private set => SetActivity(ref _isDiscovering, value);
    }

    public bool IsScanning
    {
        get => _isScanning;
        private set => SetActivity(ref _isScanning, value);
    }

    /// <summary>True while deleting or restoring.</summary>
    public bool IsDeleting
    {
        get => _isDeleting;
        private set => SetActivity(ref _isDeleting, value);
    }

    /// <summary>True while any long-running operation (discovery, scan, delete) is active.</summary>
    public bool IsBusy => IsDiscovering || IsScanning || IsDeleting;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>
    /// Sets one of the busy flags. Every command's availability, and
    /// <see cref="IsBusy"/>, depend on them.
    /// </summary>
    private void SetActivity(ref bool flag, bool value, [CallerMemberName] string? propertyName = null)
    {
        if (!SetProperty(ref flag, value, propertyName))
        {
            return;
        }

        OnPropertyChanged(nameof(IsBusy));
        FindRepositoriesCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
        UndoCommand.NotifyCanExecuteChanged();
    }

    #endregion

    #region Discovery

    /// <summary>Opens the folder picker and, if a folder is chosen, searches it.</summary>
    [RelayCommand]
    private async Task BrowseForFolderAsync()
    {
        string? picked = _folderPicker.PickFolder(RootPath);
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

    private bool CanFindRepositories() => !IsDiscovering && !IsDeleting && !string.IsNullOrWhiteSpace(RootPath);

    [RelayCommand(CanExecute = nameof(CanFindRepositories))]
    private async Task FindRepositoriesAsync()
    {
        _repositories.Clear();
        SelectedRepository = null;

        CancellationTokenSource cancellation = new();
        _discoveryCancellation = cancellation;
        IsDiscovering = true;
        StatusMessage = UserMessages.Searching;

        try
        {
            await foreach (RepositoryInfo repository in _discovery.DiscoverAsync(RootPath!, cancellation.Token))
            {
                _repositories.Add(repository);
                StatusMessage = UserMessages.Found(_repositories.Count, stillSearching: true);
            }

            StatusMessage = _repositories.Count == 0
                ? UserMessages.NoRepositoriesFound
                : UserMessages.Found(_repositories.Count, stillSearching: false);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = UserMessages.SearchCancelled(_repositories.Count);
        }
#pragma warning disable CA1031 // UI-boundary safety net: never let discovery fail silently.
        catch (Exception exception)
        {
            StatusMessage = UserMessages.SearchFailed(exception.Message);
        }
#pragma warning restore CA1031
        finally
        {
            _discoveryCancellation = null;
            cancellation.Dispose();
            IsDiscovering = false;
        }
    }

    #endregion

    #region Stop

    private bool CanStop() => IsBusy;

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop()
    {
        _discoveryCancellation?.Cancel();
        _scanCancellation?.Cancel();
        _deleteCancellation?.Cancel();
    }

    #endregion

    #region Scan

    /// <summary>The in-flight (or last) scan task. Exposed for tests to await.</summary>
    internal Task ActiveScan => _activeScan;

    partial void OnSelectedRepositoryChanged(RepositoryInfo? value)
    {
        UndoableDeletion = null; // Undo is forgotten when the repository changes (DEL-4).
        _activeScan = ScanRepositoryAsync(value);
    }

    private async Task ScanRepositoryAsync(RepositoryInfo? repository)
    {
        // Supersede any in-flight scan; the superseded call will observe cancellation.
        _scanCancellation?.Cancel();

        ShowTree(null);

        if (repository is null)
        {
            return;
        }

        CancellationTokenSource cancellation = new();
        _scanCancellation = cancellation;
        IsScanning = true;
        StatusMessage = UserMessages.Scanning(repository.Name);

        try
        {
            // No progress→status coupling: progress callbacks are delivered
            // asynchronously and could clobber a later status. The busy bar
            // signals activity instead.
            IgnoredScanResult result = await _scanner.ScanAsync(repository.FullPath, progress: null, cancellation.Token);
            ShowTree(result);
            StatusMessage = UserMessages.ScanSummary(result);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer selection; that scan now owns the UI state.
        }
        catch (GitNotFoundException)
        {
            StatusMessage = UserMessages.GitNotFound;
        }
        catch (GitCommandException exception)
        {
            StatusMessage = UserMessages.GitFailed(exception.ExitCode, exception.StandardError);
        }
#pragma warning disable CA1031 // UI-boundary safety net: fire-and-forget scan must never fault unobserved.
        catch (Exception exception)
        {
            StatusMessage = UserMessages.ScanFailed(exception.Message);
        }
#pragma warning restore CA1031
        finally
        {
            // A superseded scan leaves the flag and field to the scan that replaced it.
            if (ReferenceEquals(cancellation, _scanCancellation))
            {
                _scanCancellation = null;
                IsScanning = false;
            }

            cancellation.Dispose();
        }
    }

    /// <summary>Replaces the tree with the given scan's; empty when nothing is ignored.</summary>
    private void ShowTree(IgnoredScanResult? result)
    {
        _rootNodes.Clear();
        SelectedFolder = null;
        Selection.Reset();

        // Nothing ignored → leave the tree empty; the status line explains why.
        if (result is null || result.TotalFileCount == 0)
        {
            return;
        }

        FolderNodeViewModel root = new(result.Root, Selection) { IsExpanded = true };
        _rootNodes.Add(root);

        // Show the repository root's files immediately and highlight it in the tree.
        SelectedFolder = root;
        root.IsSelected = true;
    }

    #endregion

    #region Deletion

    private void OnSelectionChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is nameof(SelectionTracker.HasSelection))
        {
            DeleteSelectedCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanDeleteSelected() => Selection.HasSelection && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanDeleteSelected))]
    private async Task DeleteSelectedAsync()
    {
        RepositoryInfo? repository = SelectedRepository;
        if (repository is null || _rootNodes.Count == 0)
        {
            return;
        }

        List<string> targets = [.. _rootNodes[0].EnumerateDeletionTargets()];
        if (targets.Count == 0)
        {
            return;
        }

        int fileCount = Selection.SelectedFileCount;
        bool confirmed = _confirmation.Confirm(
            UserMessages.ConfirmDeletionTitle,
            UserMessages.ConfirmDeletion(fileCount, Selection.FormattedSelectedSize));
        if (!confirmed)
        {
            return;
        }

        CancellationTokenSource cancellation = new();
        _deleteCancellation = cancellation;
        IsDeleting = true;
        StatusMessage = UserMessages.Moving(fileCount);

        try
        {
            DeletionResult result = await _deletion.DeleteAsync(targets, cancellation.Token);

            // Even a declined permanent-delete warning (DEL-5) may leave earlier
            // targets already recycled, so record Undo and refresh either way.
            UndoableDeletion = new LastDeletion(targets, fileCount);

            // Refresh so the tree reflects reality and the selection resets (DEL-3).
            await ScanRepositoryAsync(repository);

            StatusMessage = result.Aborted ? UserMessages.DeletionAborted : UserMessages.Moved(fileCount);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = UserMessages.DeletionCancelled;
        }
        catch (DeletionException exception)
        {
            StatusMessage = UserMessages.DeletionPartlyFailed(exception.Message);
        }
#pragma warning disable CA1031 // UI-boundary safety net: never let deletion fail silently.
        catch (Exception exception)
        {
            StatusMessage = UserMessages.DeletionFailed(exception.Message);
        }
#pragma warning restore CA1031
        finally
        {
            _deleteCancellation = null;
            cancellation.Dispose();
            IsDeleting = false;
        }
    }

    #endregion

    #region Undo

    /// <summary>
    /// The most recent deletion, for single-level Undo (DEL-4); <c>null</c> once
    /// it has been undone or forgotten.
    /// </summary>
    private LastDeletion? UndoableDeletion
    {
        get => _undoableDeletion;
        set
        {
            _undoableDeletion = value;
            UndoCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanUndo() => UndoableDeletion is not null && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private async Task UndoAsync()
    {
        LastDeletion? deletion = UndoableDeletion;
        if (deletion is null)
        {
            return;
        }

        RepositoryInfo? repository = SelectedRepository;
        IsDeleting = true;
        StatusMessage = UserMessages.Restoring;

        try
        {
            int restored = await _deletion.RestoreAsync(deletion.Targets);
            UndoableDeletion = null;

            if (repository is not null)
            {
                await ScanRepositoryAsync(repository);
            }

            StatusMessage = restored > 0
                ? UserMessages.Restored(deletion.FileCount)
                : UserMessages.NothingRestored;
        }
#pragma warning disable CA1031 // UI-boundary safety net: never let undo fail silently.
        catch (Exception exception)
        {
            StatusMessage = UserMessages.UndoFailed(exception.Message);
        }
#pragma warning restore CA1031
        finally
        {
            IsDeleting = false;
        }
    }

    /// <summary>What Undo needs: the targets that were recycled and how many files they held.</summary>
    private sealed record LastDeletion(IReadOnlyList<string> Targets, int FileCount);

    #endregion

    #region User guide

    /// <summary>Opens the user guide for the current UI culture (UI-5).</summary>
    [RelayCommand]
    private void OpenUserGuide()
    {
        if (!_userGuide.TryOpen())
        {
            StatusMessage = UserMessages.UserGuideUnavailable;
        }
    }

    #endregion

    /// <summary>
    /// Undoes the constructor's subscription and asks any running operation to
    /// stop; each operation disposes its own cancellation source as it ends.
    /// </summary>
    public void Dispose()
    {
        Selection.PropertyChanged -= OnSelectionChanged;
        Stop();
    }
}
