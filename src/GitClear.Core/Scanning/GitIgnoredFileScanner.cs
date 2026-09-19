using GitClear.Core.Git;
using GitClear.Core.Model;

namespace GitClear.Core.Scanning;

/// <summary>
/// Default scanner: gets ignored entries from git (<c>--directory</c>), sizes
/// them on a background thread, and builds the tree (SCAN-1/2/3). Individual
/// files are statted; a wholly-ignored directory is walked once to sum its size
/// and file count and becomes a single deletable node.
/// </summary>
public sealed class GitIgnoredFileScanner : IIgnoredFileScanner
{
    private const int ProgressReportInterval = 512;

    private readonly IGitClient _git;

    public GitIgnoredFileScanner(IGitClient git)
    {
        ArgumentNullException.ThrowIfNull(git);

        _git = git;
    }

    public async Task<IgnoredScanResult> ScanAsync(
        string repositoryPath,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        if (!Directory.Exists(repositoryPath))
        {
            throw new DirectoryNotFoundException($"Repository folder not found: {repositoryPath}");
        }

        IReadOnlyList<string> entries =
            await _git.GetIgnoredPathsAsync(repositoryPath, cancellationToken).ConfigureAwait(false);

        // Sizing is blocking I/O — keep it off the caller's thread.
        return await Task.Run(
            () => SizeAndBuild(repositoryPath, entries, new SizingTally(progress), cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private static IgnoredScanResult SizeAndBuild(
        string repositoryPath,
        IReadOnlyList<string> entries,
        SizingTally tally,
        CancellationToken cancellationToken)
    {
        List<IgnoredFileEntry> files = [];
        List<IgnoredDirectoryEntry> directories = [];

        foreach (string entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (entry.EndsWith('/'))
            {
                string relativePath = entry.TrimEnd('/');
                string fullPath = IgnoredTreeBuilder.ToFullPath(repositoryPath, relativePath);
                (long size, int fileCount) = MeasureDirectory(fullPath, tally, cancellationToken);
                directories.Add(new IgnoredDirectoryEntry(relativePath, size, fileCount));
            }
            else
            {
                string fullPath = IgnoredTreeBuilder.ToFullPath(repositoryPath, entry);
                files.Add(new IgnoredFileEntry(entry, SizeOf(fullPath, tally)));
                tally.CountProcessed();
            }
        }

        tally.ReportFinal();

        return new IgnoredScanResult(
            IgnoredTreeBuilder.Build(repositoryPath, files, directories),
            tally.Unreadable);
    }

    private static (long Size, int FileCount) MeasureDirectory(
        string directoryPath,
        SizingTally tally,
        CancellationToken cancellationToken)
    {
        long size = 0;
        int fileCount = 0;

        IEnumerable<string> filePaths;
        try
        {
            // A wholly-ignored directory: every file under it is ignored, so it
            // is safe to enumerate the whole subtree.
            filePaths = Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            tally.CountUnreadable();
            return (0, 0);
        }

        using IEnumerator<string> enumerator = filePaths.GetEnumerator();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string filePath;
            try
            {
                if (!enumerator.MoveNext())
                {
                    break;
                }

                filePath = enumerator.Current;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A directory became unreadable mid-walk — stop counting this subtree.
                tally.CountUnreadable();
                break;
            }

            size += SizeOf(filePath, tally);
            fileCount++;
            tally.CountProcessed();
        }

        return (size, fileCount);
    }

    private static long SizeOf(string fullPath, SizingTally tally)
    {
        try
        {
            return new FileInfo(fullPath).Length;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            tally.CountUnreadable();
            return 0;
        }
    }

    /// <summary>
    /// Counts sized and unreadable files during one scan, reporting progress every
    /// <see cref="ProgressReportInterval"/> files and once more at the end.
    /// </summary>
    private sealed class SizingTally(IProgress<ScanProgress>? progress)
    {
        private int _processed;

        public int Unreadable { get; private set; }

        public void CountProcessed()
        {
            _processed++;
            if (_processed % ProgressReportInterval == 0)
            {
                progress?.Report(new ScanProgress(_processed));
            }
        }

        public void CountUnreadable()
        {
            Unreadable++;
        }

        public void ReportFinal()
        {
            progress?.Report(new ScanProgress(_processed));
        }
    }
}
