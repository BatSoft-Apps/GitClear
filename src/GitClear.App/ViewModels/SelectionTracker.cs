using CommunityToolkit.Mvvm.ComponentModel;
using GitClear.App.Formatting;

namespace GitClear.App.ViewModels;

/// <summary>
/// Running total of the currently-checked files (UI-3). Files report size deltas
/// as they are toggled, so the total is maintained in O(1) per toggle rather
/// than rescanning the whole tree. Only the tree's own view models (same
/// namespace) may move the totals, and never below zero, so no caller can
/// corrupt them.
/// </summary>
public sealed class SelectionTracker : ObservableObject
{
    private long _selectedSize;
    private int _selectedFileCount;

    public long SelectedSize
    {
        get => _selectedSize;
        private set
        {
            if (SetProperty(ref _selectedSize, value))
            {
                OnPropertyChanged(nameof(FormattedSelectedSize));
                OnPropertyChanged(nameof(Summary));
                OnPropertyChanged(nameof(HasSelection));
            }
        }
    }

    public int SelectedFileCount
    {
        get => _selectedFileCount;
        private set
        {
            if (SetProperty(ref _selectedFileCount, value))
            {
                OnPropertyChanged(nameof(Summary));
                OnPropertyChanged(nameof(HasSelection));
            }
        }
    }

    public string FormattedSelectedSize => ByteSize.Format(SelectedSize);

    public bool HasSelection => SelectedFileCount > 0;

    public string Summary
        => SelectedFileCount == 0
            ? UserMessages.NothingSelected
            : UserMessages.SelectionSummary(SelectedFileCount, FormattedSelectedSize);

    internal void Add(long size, int fileCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        ArgumentOutOfRangeException.ThrowIfNegative(fileCount);

        SelectedSize += size;
        SelectedFileCount += fileCount;
    }

    internal void Remove(long size, int fileCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        ArgumentOutOfRangeException.ThrowIfNegative(fileCount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(size, SelectedSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(fileCount, SelectedFileCount);

        SelectedSize -= size;
        SelectedFileCount -= fileCount;
    }

    internal void Reset()
    {
        SelectedSize = 0;
        SelectedFileCount = 0;
    }
}
