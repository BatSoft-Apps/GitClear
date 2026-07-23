using System.Windows;

namespace GitClear.App.Services;

/// <summary>
/// Confirmation prompt backed by a WPF <see cref="MessageBox"/>.
/// </summary>
public sealed class MessageBoxConfirmationDialog : IConfirmationDialog
{
    public bool Confirm(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel)
            == MessageBoxResult.OK;
}
