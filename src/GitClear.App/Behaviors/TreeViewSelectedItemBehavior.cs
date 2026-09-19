using System.Windows;
using System.Windows.Controls;

namespace GitClear.App.Behaviors;

/// <summary>
/// Makes <see cref="TreeView.SelectedItem"/> (which is read-only and not
/// bindable) available as a two-way bindable attached property, so the selected
/// folder can be surfaced to a view model without code-behind.
/// </summary>
public static class TreeViewSelectedItemBehavior
{
    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.RegisterAttached(
            "SelectedItem",
            typeof(object),
            typeof(TreeViewSelectedItemBehavior),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnSelectedItemChanged));

    public static object? GetSelectedItem(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);

        return element.GetValue(SelectedItemProperty);
    }

    public static void SetSelectedItem(DependencyObject element, object? value)
    {
        ArgumentNullException.ThrowIfNull(element);

        element.SetValue(SelectedItemProperty, value);
    }

    private static void OnSelectedItemChanged(DependencyObject element, DependencyPropertyChangedEventArgs eventArgs)
    {
        if (element is TreeView treeView)
        {
            // Idempotent: unhook before hooking so we never subscribe twice. The
            // handler is static and the event belongs to the tree view, so the
            // subscription ends with the tree view and cannot keep it alive.
            treeView.SelectedItemChanged -= OnTreeViewSelectedItemChanged;
            treeView.SelectedItemChanged += OnTreeViewSelectedItemChanged;
        }
    }

    private static void OnTreeViewSelectedItemChanged(object? sender, RoutedPropertyChangedEventArgs<object> eventArgs)
    {
        if (sender is DependencyObject element)
        {
            SetSelectedItem(element, eventArgs.NewValue);
        }
    }
}
