using System.Runtime.CompilerServices;
using System.Threading.Channels;
using GitClear.Core.Model;

namespace GitClear.Core.Discovery;

/// <summary>
/// Iterative (non-recursive) filesystem walk that finds Git repositories.
/// The blocking walk runs on a thread-pool thread and publishes results through
/// a channel, so callers can <c>await foreach</c> without blocking their thread.
/// </summary>
public sealed class RepositoryDiscoveryService : IRepositoryDiscoveryService
{
    private const string GitEntryName = ".git";

    public async IAsyncEnumerable<RepositoryInfo> DiscoverAsync(
        string rootPath,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        if (!Directory.Exists(rootPath))
        {
            throw new DirectoryNotFoundException($"Folder not found: {rootPath}");
        }

        var channel = Channel.CreateUnbounded<RepositoryInfo>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true,
        });

        // Produce on a background thread; the try/catch guarantees the channel is
        // always completed so the reader below can never hang.
        var walk = Task.Run(() =>
        {
            try
            {
                Walk(rootPath, channel.Writer, cancellationToken);
                channel.Writer.Complete();
            }
            catch (Exception ex)
            {
                channel.Writer.Complete(ex);
            }
        }, cancellationToken);

        await foreach (var repo in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return repo;
        }

        // Surfaces any non-cancellation fault captured by Complete(ex).
        await walk.ConfigureAwait(false);
    }

    private static void Walk(
        string rootPath,
        ChannelWriter<RepositoryInfo> writer,
        CancellationToken cancellationToken)
    {
        var stack = new Stack<DirectoryInfo>();
        stack.Push(new DirectoryInfo(rootPath));

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dir = stack.Pop();

            if (TryGetGitEntry(dir, out var gitIsFile))
            {
                writer.TryWrite(RepositoryInfo.Create(dir.FullName, gitIsFile));
                continue; // DISC-2: do not descend into a discovered repository.
            }

            try
            {
                foreach (var sub in dir.EnumerateDirectories())
                {
                    // Skip symlinks/junctions to avoid cycles and escaping the tree.
                    if ((sub.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }

                    stack.Push(sub);
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or DirectoryNotFoundException)
            {
                // Unreadable directory (permissions, deleted mid-walk) — skip it.
            }
        }
    }

    private static bool TryGetGitEntry(DirectoryInfo dir, out bool isFile)
    {
        var gitPath = Path.Combine(dir.FullName, GitEntryName);

        if (Directory.Exists(gitPath))
        {
            isFile = false;
            return true;
        }

        if (File.Exists(gitPath))
        {
            isFile = true;
            return true;
        }

        isFile = false;
        return false;
    }
}
