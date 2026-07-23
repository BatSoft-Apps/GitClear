namespace GitClear.Core.Deletion;

/// <summary>
/// Outcome of a delete-to-Recycle-Bin operation.
/// </summary>
/// <param name="FilesDeleted">Number of files actually sent to the Recycle Bin.</param>
/// <param name="FilesSkipped">Requested files that no longer existed and were skipped.</param>
public readonly record struct DeletionResult(int FilesDeleted, int FilesSkipped);
