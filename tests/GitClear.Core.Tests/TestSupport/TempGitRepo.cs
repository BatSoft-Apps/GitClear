using System.Diagnostics;

namespace GitClear.Core.Tests.TestSupport;

/// <summary>
/// A real, throwaway git repository under the system temp path for integration
/// tests. Shells out to the installed git so tests exercise the exact ignore
/// semantics the app relies on.
/// </summary>
public sealed class TempGitRepo : IDisposable
{
    public TempGitRepo()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "gitclear-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);

        Git("init");
        Git("config", "user.email", "test@example.com");
        Git("config", "user.name", "GitClear Test");
    }

    /// <summary>Absolute path to the repository root.</summary>
    public string Path { get; }

    /// <summary>Writes a file of the given byte length (creating parent folders).</summary>
    public void WriteFile(string relativePath, int byteLength)
    {
        var full = System.IO.Path.Combine(Path, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, new byte[byteLength]);
    }

    /// <summary>Writes a text file (creating parent folders).</summary>
    public void WriteText(string relativePath, string contents)
    {
        var full = System.IO.Path.Combine(Path, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllText(full, contents);
    }

    /// <summary>Stages paths and commits, so they become tracked (excluded from "others").</summary>
    public void StageAndCommit(params string[] relativePaths)
    {
        Git(["add", .. relativePaths]);
        Git("commit", "-m", "test");
    }

    public void Git(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = Path,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var arg in arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo)!;
        var stderr = process.StandardError.ReadToEnd();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {stderr}");
        }
    }

    public void Dispose() => ForceDelete(Path);

    /// <summary>Deletes a tree, clearing read-only attributes git sets on pack files.</summary>
    private static void ForceDelete(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort cleanup; a locked file must not fail the test run.
        }
    }
}
