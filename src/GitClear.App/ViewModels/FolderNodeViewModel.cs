using CommunityToolkit.Mvvm.ComponentModel;
using GitClear.App.Formatting;
using GitClear.Core.Model;

namespace GitClear.App.ViewModels;

/// <summary>
/// Presents one folder in the left-hand tree (UI-1). Adds view state (expanded /
/// selected) and participates in tri-state selection (UI-2). The tree shows
/// folders only; a folder's files appear in the right pane when it is selected.
///
/// A <see cref="IgnoredFolderNode.IsFullyIgnored"/> directory has no children and
/// behaves like a selection leaf: checking it selects the whole directory (its
/// entire size / file count) and it is deleted as a single unit (DEL-1).
/// </summary>
public sealed partial class FolderNodeViewModel : SelectableNodeViewModel
{
    private readonly IgnoredFolderNode _model;
    private readonly SelectionTracker _tracker;
    private readonly IReadOnlyList<SelectableNodeViewModel> _checkableChildren;

    public FolderNodeViewModel(IgnoredFolderNode model, SelectionTracker tracker)
        : this(model, parent: null, tracker)
    {
    }

    private FolderNodeViewModel(IgnoredFolderNode model, FolderNodeViewModel? parent, SelectionTracker tracker)
        : base(parent)
    {
        _model = model;
        _tracker = tracker;
        Subfolders = model.Subfolders.Select(f => new FolderNodeViewModel(f, this, tracker)).ToList();
        Files = model.Files.Select(f => new FileNodeViewModel(f, this, tracker)).ToList();
        _checkableChildren = [.. Subfolders, .. Files];
    }

    public string Name => _model.Name;

    public string FullPath => _model.FullPath;

    public bool IsFullyIgnored => _model.IsFullyIgnored;

    public int TotalFileCount => _model.TotalFileCount;

    public string FormattedSize => ByteSize.Format(_model.TotalSize);

    /// <summary>Immediate subfolders (the tree shows folders only; files go in the right pane).</summary>
    public IReadOnlyList<FolderNodeViewModel> Subfolders { get; }

    /// <summary>Ignored files directly in this folder.</summary>
    public IReadOnlyList<FileNodeViewModel> Files { get; }

    protected override IReadOnlyList<SelectableNodeViewModel> CheckableChildren => _checkableChildren;

    protected override void OnCheckedChanged()
    {
        // A wholly-ignored directory is a selection leaf: it contributes its
        // entire subtree to the running total. Ordinary folders contribute
        // nothing themselves — their file descendants report individually.
        if (!IsFullyIgnored)
        {
            return;
        }

        if (IsChecked == true)
        {
            _tracker.Add(_model.TotalSize, _model.TotalFileCount);
        }
        else
        {
            _tracker.Remove(_model.TotalSize, _model.TotalFileCount);
        }
    }

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Paths to recycle for the current selection: each checked wholly-ignored
    /// directory as a single unit, plus each individually-checked file (DEL-1).
    /// </summary>
    public IEnumerable<string> EnumerateDeletionTargets()
    {
        if (IsFullyIgnored)
        {
            if (IsChecked == true)
            {
                yield return FullPath;
            }

            yield break;
        }

        foreach (var file in Files)
        {
            if (file.IsChecked == true)
            {
                yield return file.FullPath;
            }
        }

        foreach (var subfolder in Subfolders)
        {
            foreach (var target in subfolder.EnumerateDeletionTargets())
            {
                yield return target;
            }
        }
    }
}
