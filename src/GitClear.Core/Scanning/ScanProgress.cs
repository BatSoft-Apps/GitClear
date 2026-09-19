namespace GitClear.Core.Scanning;

/// <summary>
/// Progress reported while sizing ignored files (SCAN-3). The total is not known
/// up front (git collapses wholly-ignored directories), so only a running count
/// is reported.
/// </summary>
public readonly record struct ScanProgress
{
    /// <param name="filesProcessed">Files sized so far.</param>
    public ScanProgress(int filesProcessed)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(filesProcessed);

        FilesProcessed = filesProcessed;
    }

    public int FilesProcessed { get; }
}
