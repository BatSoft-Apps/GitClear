using System.Diagnostics;

namespace GitClear.Core.Tests.TestSupport;

/// <summary>
/// A real, throwaway git repository under the system temp path for integration
/// tests. Shells out to the installed git so tests exercise the exact ignore
/// semantics the app relies on.
/// </summary>
public sealed class TemporaryGitRepository : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    public TemporaryGitRepository()
    {
        try
        {
            Git("init");
            Git("config", "user.email", "test@example.com");
            Git("config", "user.name", "GitClear Test");
        }
        catch
        {
            // No caller will ever dispose a half-built repository, so clean up here.
            _directory.Dispose();
            throw;
        }
    }

    /// <summary>Absolute path to the repository root.</summary>
    public string Path => _directory.Path;

    /// <summary>Writes a file of the given byte length (creating parent folders).</summary>
    public void WriteFile(string relativePath, int byteLength) => _directory.CreateFile(relativePath, byteLength);

    /// <summary>Writes a text file (creating parent folders).</summary>
    public void WriteText(string relativePath, string contents) => _directory.CreateFile(relativePath, contents);

    /// <summary>Stages paths and commits, so they become tracked (excluded from "others").</summary>
    public void StageAndCommit(params string[] relativePaths)
    {
        Git(["add", .. relativePaths]);
        Git("commit", "-m", "test");
    }

    public void Dispose() => _directory.Dispose();

    private void Git(params string[] arguments)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "git",
            WorkingDirectory = Path,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)!;
        string standardError = process.StandardError.ReadToEnd();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {standardError}");
        }
    }
}
