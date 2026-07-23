using GitClear.App.Formatting;
using GitClear.Core.Model;

namespace GitClear.App.ViewModels;

/// <summary>
/// Presents one ignored file in the right-hand file list (UI-1) and is the unit
/// of selected size (UI-3): as it is checked/unchecked it reports its size delta
/// to the shared <see cref="SelectionTracker"/>.
/// </summary>
public sealed class FileNodeViewModel : SelectableNodeViewModel
{
    private static readonly IReadOnlyList<SelectableNodeViewModel> NoChildren = [];

    private readonly IgnoredFileNode _model;
    private readonly SelectionTracker _tracker;

    public FileNodeViewModel(IgnoredFileNode model, FolderNodeViewModel parent, SelectionTracker tracker)
        : base(parent)
    {
        _model = model;
        _tracker = tracker;
    }

    public string Name => _model.Name;

    public string FullPath => _model.FullPath;

    public long Size => _model.Size;

    public string FormattedSize => ByteSize.Format(_model.Size);

    protected override IReadOnlyList<SelectableNodeViewModel> CheckableChildren => NoChildren;

    protected override void OnCheckedChanged()
    {
        if (IsChecked == true)
        {
            _tracker.Add(Size, fileCount: 1);
        }
        else
        {
            _tracker.Remove(Size, fileCount: 1);
        }
    }
}
