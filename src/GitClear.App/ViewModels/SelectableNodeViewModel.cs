using CommunityToolkit.Mvvm.ComponentModel;

namespace GitClear.App.ViewModels;

/// <summary>
/// Base for tree nodes that carry a tri-state selection checkbox (UI-2).
/// Checking a node cascades down to all descendants; a node's state is derived
/// upward from its children (all checked → checked, none → unchecked, mixed →
/// indeterminate). This is the classic tri-state tree algorithm.
/// </summary>
public abstract class SelectableNodeViewModel : ObservableObject
{
    private bool? _isChecked = false;

    protected SelectableNodeViewModel(SelectableNodeViewModel? parent) => Parent = parent;

    private SelectableNodeViewModel? Parent { get; }

    /// <summary>Nodes that participate in check propagation (subfolders + files); empty for a leaf.</summary>
    protected abstract IReadOnlyList<SelectableNodeViewModel> CheckableChildren { get; }

    /// <summary>
    /// Tri-state checkbox value. <c>true</c> = all selected, <c>false</c> = none,
    /// <c>null</c> = some (indeterminate). Setting it is a user action and cascades.
    /// </summary>
    public bool? IsChecked
    {
        get => _isChecked;
        set => SetIsChecked(value, updateChildren: true, updateParent: true);
    }

    private void SetIsChecked(bool? value, bool updateChildren, bool updateParent)
    {
        if (value == _isChecked)
        {
            return;
        }

        _isChecked = value;

        if (updateChildren && _isChecked.HasValue)
        {
            foreach (var child in CheckableChildren)
            {
                child.SetIsChecked(_isChecked, updateChildren: true, updateParent: false);
            }
        }

        if (updateParent)
        {
            Parent?.VerifyCheckState();
        }

        OnPropertyChanged(nameof(IsChecked));
        OnCheckedChanged();
    }

    private void VerifyCheckState()
    {
        bool? state = null;
        var children = CheckableChildren;

        for (var i = 0; i < children.Count; i++)
        {
            var current = children[i].IsChecked;
            if (i == 0)
            {
                state = current;
            }
            else if (state != current)
            {
                state = null;
                break;
            }
        }

        SetIsChecked(state, updateChildren: false, updateParent: true);
    }

    /// <summary>Hook called after this node's checked state actually changes.</summary>
    protected virtual void OnCheckedChanged()
    {
    }
}
