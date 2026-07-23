namespace GitClear.App.Services;

/// <summary>
/// Abstracts a yes/no confirmation prompt so view models can be tested without a
/// real message box (DEL-2).
/// </summary>
public interface IConfirmationDialog
{
    /// <summary>Shows a confirmation prompt; returns true if the user confirmed.</summary>
    bool Confirm(string title, string message);
}
