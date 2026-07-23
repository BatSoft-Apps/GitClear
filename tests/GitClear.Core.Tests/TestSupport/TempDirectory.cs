namespace GitClear.Core.Tests.TestSupport;

/// <summary>
/// A throwaway directory under the system temp path, deleted on dispose.
/// Provides small helpers for building repo-like trees in tests.
/// </summary>
public sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gitclear-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    /// <summary>Absolute path to the root of this temp directory.</summary>
    public string Path { get; }

    /// <summary>Creates a (possibly nested) subdirectory and returns its full path.</summary>
    public string CreateDir(params string[] segments)
    {
        var full = System.IO.Path.Combine([Path, .. segments]);
        Directory.CreateDirectory(full);
        return full;
    }

    /// <summary>Writes a file (creating parent folders), returns its full path.</summary>
    public string CreateFile(string relativePath, string contents = "")
    {
        var full = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllText(full, contents);
        return full;
    }

    /// <summary>Marks a directory as a Git repo by creating a real <c>.git</c> folder.</summary>
    public string MakeRepo(params string[] segments)
    {
        var dir = CreateDir(segments);
        Directory.CreateDirectory(System.IO.Path.Combine(dir, ".git"));
        return dir;
    }

    /// <summary>Marks a directory as a worktree/submodule by creating a <c>.git</c> file.</summary>
    public string MakeGitFileRepo(params string[] segments)
    {
        var dir = CreateDir(segments);
        File.WriteAllText(System.IO.Path.Combine(dir, ".git"), "gitdir: /elsewhere");
        return dir;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup; a locked file shouldn't fail the test run.
        }
    }
}
