namespace GitClear.App.Services;

/// <summary>
/// Abstracts the "choose a folder" dialog so view models can be unit-tested
/// without a real Windows dialog.
/// </summary>
public interface IFolderPickerService
{
    /// <summary>
    /// Shows a folder-picker dialog. Returns the chosen folder path, or
    /// <c>null</c> if the user cancelled.
    /// </summary>
    /// <param name="initialFolder">Folder to open at, if it still exists.</param>
    string? PickFolder(string? initialFolder = null);
}
