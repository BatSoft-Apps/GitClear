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

        Channel<RepositoryInfo> channel = Channel.CreateUnbounded<RepositoryInfo>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true,
        });

        // Produce on a background thread; the try/catch guarantees the channel is
        // always completed so the reader below can never hang.
        Task walk = Task.Run(() =>
        {
            try
            {
                Walk(rootPath, channel.Writer, cancellationToken);
                channel.Writer.Complete();
            }
            catch (Exception exception)
            {
                channel.Writer.Complete(exception);
            }
        }, cancellationToken);

        await foreach (RepositoryInfo repository in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return repository;
        }

        // Surfaces any non-cancellation fault captured by Complete(exception).
        await walk.ConfigureAwait(false);
    }

    private static void Walk(
        string rootPath,
        ChannelWriter<RepositoryInfo> writer,
        CancellationToken cancellationToken)
    {
        Stack<DirectoryInfo> stack = new();
        stack.Push(new DirectoryInfo(rootPath));

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DirectoryInfo directory = stack.Pop();

            if (TryGetGitEntry(directory, out bool gitIsFile))
            {
                writer.TryWrite(new RepositoryInfo(directory.FullName, gitIsFile));
                continue; // DISC-2: do not descend into a discovered repository.
            }

            try
            {
                foreach (DirectoryInfo subdirectory in directory.EnumerateDirectories())
                {
                    // Skip symlinks/junctions to avoid cycles and escaping the tree.
                    if ((subdirectory.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }

                    stack.Push(subdirectory);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Unreadable directory (permissions, deleted mid-walk) — skip it.
            }
        }
    }

    private static bool TryGetGitEntry(DirectoryInfo directory, out bool isFile)
    {
        string gitPath = Path.Combine(directory.FullName, GitEntryName);

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
