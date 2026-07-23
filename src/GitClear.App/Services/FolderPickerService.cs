using System.IO;
using Microsoft.Win32;

namespace GitClear.App.Services;

/// <summary>
/// Folder picker backed by WPF's native <see cref="OpenFolderDialog"/>
/// (available since .NET 8) — no WinForms dependency.
/// </summary>
public sealed class FolderPickerService : IFolderPickerService
{
    public string? PickFolder(string? initialFolder = null)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select a folder to scan for Git repositories",
            Multiselect = false,
        };

        if (!string.IsNullOrWhiteSpace(initialFolder) && Directory.Exists(initialFolder))
        {
            dialog.InitialDirectory = initialFolder;
        }

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
