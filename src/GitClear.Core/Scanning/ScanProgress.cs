namespace GitClear.Core.Scanning;

/// <summary>
/// Progress reported while sizing ignored files (SCAN-3). The total is not known
/// up front (git collapses wholly-ignored directories), so only a running count
/// is reported.
/// </summary>
/// <param name="FilesProcessed">Files sized so far.</param>
public readonly record struct ScanProgress(int FilesProcessed);
