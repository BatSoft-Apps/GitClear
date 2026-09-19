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

        ProcessStartInfo startInfo = new()
        {
            FileName = "git",
            WorkingDirectory = repositoryPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (string argument in IgnoredFilesArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = new() { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Win32Exception exception)
        {
            throw new GitNotFoundException(
                "Git could not be started. Ensure git is installed and on your PATH.", exception);
        }

        // Read standard output as raw bytes: with -z, paths are NUL-separated raw UTF-8.
        using MemoryStream standardOutput = new();
        Task readStandardOutput = process.StandardOutput.BaseStream.CopyToAsync(standardOutput, cancellationToken);
        Task<string> readStandardError = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await Task.WhenAll(readStandardOutput, readStandardError, process.WaitForExitAsync(cancellationToken))
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        if (process.ExitCode != 0)
        {
            string standardError = (await readStandardError.ConfigureAwait(false)).Trim();
            throw new GitCommandException(
                $"git ls-files failed (exit code {process.ExitCode}): {standardError}",
                process.ExitCode,
                standardError);
        }

        return ParseNulSeparated(standardOutput.ToArray());
    }

    private static List<string> ParseNulSeparated(byte[] bytes)
    {
        List<string> result = new();
        int start = 0;

        for (int i = 0; i < bytes.Length; i++)
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
