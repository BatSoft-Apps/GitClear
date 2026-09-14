namespace GitClear.Core.Deletion;

/// <summary>
/// Outcome of a delete-to-Recycle-Bin operation.
/// </summary>
/// <param name="FilesDeleted">Number of targets sent to the Recycle Bin.</param>
/// <param name="FilesSkipped">Requested targets that no longer existed and were skipped.</param>
/// <param name="Aborted">
/// True if the user declined a permanent-delete warning (DEL-5) and the operation
/// stopped early. Targets processed before that point may already be recycled, so
/// the caller should still refresh and offer Undo.
/// </param>
public readonly record struct DeletionResult(int FilesDeleted, int FilesSkipped, bool Aborted = false);
