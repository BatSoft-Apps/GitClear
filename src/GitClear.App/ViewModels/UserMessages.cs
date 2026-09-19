using GitClear.App.Formatting;
using GitClear.Core.Model;

namespace GitClear.App.ViewModels;

/// <summary>
/// The wording of every message the main window shows the user: the status bar,
/// the selection summary and the delete confirmation. The user guide quotes some
/// of these verbatim, so keep it in step when one changes.
/// </summary>
internal static class UserMessages
{
    private const int MaxToolOutputLength = 300;

    #region Discovery

    public const string Welcome = "Select a folder to scan for Git repositories.";

    public const string Searching = "Searching for Git repositories…";

    public const string NoRepositoriesFound = "No Git repositories found under the selected folder.";

    public static string Found(int repositoryCount, bool stillSearching)
    {
        string noun = repositoryCount == 1 ? "repository" : "repositories";
        return stillSearching ? $"Found {repositoryCount} {noun}…" : $"Found {repositoryCount} {noun}.";
    }

    public static string SearchCancelled(int repositoryCount)
        => $"Search cancelled — {Found(repositoryCount, stillSearching: false)}";

    public static string SearchFailed(string reason) => $"Could not search that folder: {reason}";

    #endregion

    #region Scan

    public const string GitNotFound = "Git was not found on your PATH. Install Git to scan repositories.";

    public static string Scanning(string repositoryName) => $"Scanning “{repositoryName}” for ignored files…";

    public static string ScanSummary(IgnoredScanResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.TotalFileCount == 0)
        {
            return "No ignored files found in this repository.";
        }

        string unreadable = result.UnreadableFileCount > 0
            ? $" · {result.UnreadableFileCount} unreadable"
            : string.Empty;
        return $"{result.TotalFileCount:N0} ignored {FileNoun(result.TotalFileCount)} · "
            + $"{ByteSize.Format(result.TotalSize)}{unreadable}";
    }

    /// <summary>Shows git's own diagnostic so the user needn't re-run git themselves (ARCH-3).</summary>
    public static string GitFailed(int exitCode, string standardError)
    {
        string detail = FlattenToOneLine(standardError);
        return detail.Length == 0
            ? $"Git could not scan this repository (git exit code {exitCode})."
            : $"Git could not scan this repository. Git says: {detail}";
    }

    public static string ScanFailed(string reason) => $"Could not scan this repository: {reason}";

    #endregion

    #region Selection and deletion

    public const string NothingSelected = "Nothing selected.";

    public const string ConfirmDeletionTitle = "Move to Recycle Bin";

    public const string DeletionAborted = "Deletion stopped — nothing was permanently deleted. Anything already "
        + "moved to the Recycle Bin can be restored with Undo.";

    public const string DeletionCancelled = "Deletion cancelled.";

    public static string SelectionSummary(int fileCount, string formattedSize)
        => $"Selected for deletion: {fileCount:N0} {FileNoun(fileCount)} · {formattedSize}";

    public static string ConfirmDeletion(int fileCount, string formattedSize)
        => $"Move {fileCount:N0} ignored {FileNoun(fileCount)} ({formattedSize}) to the Recycle Bin?\n\n"
            + "You can restore them with Undo, or from the Recycle Bin.";

    public static string Moving(int fileCount) => $"Moving {fileCount:N0} {FileNoun(fileCount)} to the Recycle Bin…";

    public static string Moved(int fileCount)
        => $"Moved {fileCount:N0} {FileNoun(fileCount)} to the Recycle Bin. Use Undo to restore.";

    public static string DeletionPartlyFailed(string reason) => $"Some items could not be deleted: {reason}";

    public static string DeletionFailed(string reason) => $"Deletion failed: {reason}";

    #endregion

    #region Undo

    public const string Restoring = "Restoring from the Recycle Bin…";

    public const string NothingRestored = "Nothing could be restored automatically — check the Recycle Bin.";

    public static string Restored(int fileCount)
        => $"Restored {fileCount:N0} {FileNoun(fileCount)} from the Recycle Bin.";

    public static string UndoFailed(string reason) => $"Undo failed: {reason}";

    #endregion

    #region User guide

    public const string UserGuideUnavailable = "Could not open the user guide. It may be missing from the "
        + "installation folder, or no PDF viewer is registered.";

    #endregion

    private static string FileNoun(int fileCount)
        => fileCount == 1 ? "file" : "files";

    /// <summary>
    /// Squeezes a multi-line tool diagnostic onto the single-line status bar:
    /// newlines and runs of whitespace become single spaces, and very long output
    /// is clipped so the message stays readable.
    /// </summary>
    private static string FlattenToOneLine(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        string flattened = string.Join(' ', text.Split(
            (char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return flattened.Length <= MaxToolOutputLength
            ? flattened
            : string.Concat(flattened.AsSpan(0, MaxToolOutputLength), "…");
    }
}
