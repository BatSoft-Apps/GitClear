using CommunityToolkit.Mvvm.ComponentModel;
using GitClear.App.Formatting;

namespace GitClear.App.ViewModels;

/// <summary>
/// Running total of the currently-checked files (UI-3). Files report size deltas
/// as they are toggled, so the total is maintained in O(1) per toggle rather
/// than rescanning the whole tree.
/// </summary>
public sealed partial class SelectionTracker : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedSelectedSize))]
    [NotifyPropertyChangedFor(nameof(Summary))]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private long _selectedSize;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private int _selectedFileCount;

    public string FormattedSelectedSize => ByteSize.Format(SelectedSize);

    public bool HasSelection => SelectedFileCount > 0;

    public string Summary => SelectedFileCount == 0
        ? "Nothing selected."
        : $"Selected for deletion: {SelectedFileCount:N0} {(SelectedFileCount == 1 ? "file" : "files")} · {FormattedSelectedSize}";

    public void Add(long size, int fileCount)
    {
        SelectedSize += size;
        SelectedFileCount += fileCount;
    }

    public void Remove(long size, int fileCount)
    {
        SelectedSize -= size;
        SelectedFileCount -= fileCount;
    }

    public void Reset()
    {
        SelectedSize = 0;
        SelectedFileCount = 0;
    }
}
