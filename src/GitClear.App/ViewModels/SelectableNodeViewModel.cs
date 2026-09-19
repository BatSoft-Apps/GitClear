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
    private readonly SelectableNodeViewModel? _parent;
    private bool? _isChecked = false;

    /// <param name="parent">The containing node; <c>null</c> for the root.</param>
    protected SelectableNodeViewModel(SelectableNodeViewModel? parent)
    {
        _parent = parent;
    }

    /// <summary>
    /// Tri-state checkbox value. <c>true</c> = all selected, <c>false</c> = none,
    /// <c>null</c> = some (indeterminate). Setting it is a user action and cascades.
    /// </summary>
    public bool? IsChecked
    {
        get => _isChecked;
        set => SetIsChecked(value, updateChildren: true, updateParent: true);
    }

    /// <summary>Nodes that participate in check propagation (subfolders + files); empty for a leaf.</summary>
    protected abstract IReadOnlyList<SelectableNodeViewModel> CheckableChildren { get; }

    /// <summary>Hook called after this node's checked state actually changes.</summary>
    protected virtual void OnCheckedChanged()
    {
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
            foreach (SelectableNodeViewModel child in CheckableChildren)
            {
                child.SetIsChecked(_isChecked, updateChildren: true, updateParent: false);
            }
        }

        if (updateParent)
        {
            _parent?.UpdateFromChildren();
        }

        OnPropertyChanged(nameof(IsChecked));
        OnCheckedChanged();
    }

    /// <summary>Derives this node's state from its children: all alike → that state, else indeterminate.</summary>
    private void UpdateFromChildren()
    {
        bool? state = null;
        IReadOnlyList<SelectableNodeViewModel> children = CheckableChildren;

        for (int i = 0; i < children.Count; i++)
        {
            bool? current = children[i].IsChecked;
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
}
