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

    public GitIgnoredFileScanner(IGitClient git) => _git = git;

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

        var entries = await _git.GetIgnoredPathsAsync(repositoryPath, cancellationToken).ConfigureAwait(false);

        // Sizing is blocking I/O — keep it off the caller's thread.
        return await Task.Run(
            () => SizeAndBuild(repositoryPath, entries, progress, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private static IgnoredScanResult SizeAndBuild(
        string repositoryPath,
        IReadOnlyList<string> entries,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var files = new List<IgnoredFileEntry>();
        var directories = new List<IgnoredDirectoryEntry>();
        var unreadable = 0;
        var processed = 0;

        void ReportProgress()
        {
            processed++;
            if (progress is not null && processed % ProgressReportInterval == 0)
            {
                progress.Report(new ScanProgress(processed));
            }
        }

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (entry.EndsWith('/'))
            {
                var relative = entry.TrimEnd('/');
                var fullPath = ToFullPath(repositoryPath, relative);
                var (size, count) = MeasureDirectory(fullPath, ReportProgress, ref unreadable, cancellationToken);
                directories.Add(new IgnoredDirectoryEntry(relative, size, count));
            }
            else
            {
                files.Add(new IgnoredFileEntry(entry, SizeOf(ToFullPath(repositoryPath, entry), ref unreadable)));
                ReportProgress();
            }
        }

        if (progress is not null)
        {
            progress.Report(new ScanProgress(processed));
        }

        var root = IgnoredTreeBuilder.Build(repositoryPath, files, directories);

        return new IgnoredScanResult
        {
            Root = root,
            UnreadableFileCount = unreadable,
        };
    }

    private static (long Size, int Count) MeasureDirectory(
        string directoryPath,
        Action onFileProcessed,
        ref int unreadable,
        CancellationToken cancellationToken)
    {
        long size = 0;
        var count = 0;

        IEnumerable<string> filePaths;
        try
        {
            // A wholly-ignored directory: every file under it is ignored, so it
            // is safe to enumerate the whole subtree.
            filePaths = Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            unreadable++;
            return (0, 0);
        }

        using var enumerator = filePaths.GetEnumerator();
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
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A directory became unreadable mid-walk — stop counting this subtree.
                unreadable++;
                break;
            }

            size += SizeOf(filePath, ref unreadable);
            count++;
            onFileProcessed();
        }

        return (size, count);
    }

    private static long SizeOf(string fullPath, ref int unreadable)
    {
        try
        {
            return new FileInfo(fullPath).Length;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            unreadable++;
            return 0;
        }
    }

    private static string ToFullPath(string repositoryPath, string relativePath) =>
        Path.Combine(repositoryPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
}
