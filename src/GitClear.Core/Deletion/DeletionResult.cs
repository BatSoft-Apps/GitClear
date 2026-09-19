namespace GitClear.Core.Deletion;

/// <summary>
/// Outcome of a delete-to-Recycle-Bin operation. A target is an individual file
/// or a wholly-ignored directory (DEL-1), so these count targets, not files.
/// </summary>
public readonly record struct DeletionResult
{
    /// <param name="targetsRecycled">Targets sent to the Recycle Bin.</param>
    /// <param name="targetsSkipped">Requested targets that no longer existed and were skipped.</param>
    /// <param name="aborted">
    /// True if the user declined a permanent-delete warning (DEL-5) and the operation
    /// stopped early. Targets processed before that point may already be recycled, so
    /// the caller should still refresh and offer Undo.
    /// </param>
    public DeletionResult(int targetsRecycled, int targetsSkipped, bool aborted = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(targetsRecycled);
        ArgumentOutOfRangeException.ThrowIfNegative(targetsSkipped);

        TargetsRecycled = targetsRecycled;
        TargetsSkipped = targetsSkipped;
        Aborted = aborted;
    }

    public int TargetsRecycled { get; }

    public int TargetsSkipped { get; }

    public bool Aborted { get; }
}
