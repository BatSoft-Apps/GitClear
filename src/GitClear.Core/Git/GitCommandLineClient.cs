using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace GitClear.Core.Git;

/// <summary>
/// <see cref="IGitClient"/> backed by the installed <c>git</c> executable.
/// </summary>
public sealed class GitCommandLineClient : IGitClient
{
    // --directory collapses a wholly-ignored directory to a single entry (with a
    // trailing '/'); files in mixed folders are still listed individually.
    private static readonly string[] IgnoredFilesArguments =
        ["ls-files", "--others", "--ignored", "--exclude-standard", "-z", "--directory"];

    public async Task<IReadOnlyList<string>> GetIgnoredPathsAsync(
        string repositoryPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);

        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = repositoryPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var arg in IgnoredFilesArguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            throw new GitNotFoundException(
                "Git could not be started. Ensure git is installed and on your PATH.", ex);
        }

        // Read stdout as raw bytes: with -z, paths are NUL-separated raw UTF-8.
        using var stdout = new MemoryStream();
        var readStdout = process.StandardOutput.BaseStream.CopyToAsync(stdout, cancellationToken);
        var readStderr = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await Task.WhenAll(readStdout, readStderr, process.WaitForExitAsync(cancellationToken))
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        if (process.ExitCode != 0)
        {
            var stderr = (await readStderr.ConfigureAwait(false)).Trim();
            throw new GitCommandException(
                $"git ls-files failed (exit code {process.ExitCode}): {stderr}", process.ExitCode);
        }

        return ParseNulSeparated(stdout.ToArray());
    }

    private static List<string> ParseNulSeparated(byte[] bytes)
    {
        var result = new List<string>();
        var start = 0;

        for (var i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] != 0)
            {
                continue;
            }

            if (i > start)
            {
                result.Add(Encoding.UTF8.GetString(bytes, start, i - start));
            }

            start = i + 1;
        }

        // Tolerate a final entry without a trailing NUL.
        if (start < bytes.Length)
        {
            result.Add(Encoding.UTF8.GetString(bytes, start, bytes.Length - start));
        }

        return result;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Process already exited.
        }
        catch (Win32Exception)
        {
            // Could not terminate; nothing more we can do.
        }
    }
}
