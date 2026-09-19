namespace GitClear.Core.Tests.TestSupport;

/// <summary>
/// A throwaway directory under the system temp path, deleted on dispose.
/// Provides small helpers for building repository-like trees in tests.
/// </summary>
public sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gitclear-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    /// <summary>Absolute path to the root of this temporary directory.</summary>
    public string Path { get; }

    /// <summary>Creates a (possibly nested) subdirectory and returns its full path.</summary>
    public string CreateDirectory(params string[] segments)
    {
        string fullPath = System.IO.Path.Combine([Path, .. segments]);
        Directory.CreateDirectory(fullPath);
        return fullPath;
    }

    /// <summary>Writes a text file (creating parent folders) and returns its full path.</summary>
    /// <param name="relativePath">Path below this directory; '/' or '\' separators.</param>
    public string CreateFile(string relativePath, string contents = "")
    {
        string fullPath = PrepareFile(relativePath);
        File.WriteAllText(fullPath, contents);
        return fullPath;
    }

    /// <summary>Writes a file of the given byte length (creating parent folders) and returns its full path.</summary>
    public string CreateFile(string relativePath, int byteLength)
    {
        string fullPath = PrepareFile(relativePath);
        File.WriteAllBytes(fullPath, new byte[byteLength]);
        return fullPath;
    }

    /// <summary>Marks a directory as a Git repository by creating a real <c>.git</c> folder.</summary>
    public string MakeRepository(params string[] segments)
    {
        string repositoryPath = CreateDirectory(segments);
        Directory.CreateDirectory(System.IO.Path.Combine(repositoryPath, ".git"));
        return repositoryPath;
    }

    /// <summary>Marks a directory as a worktree/submodule by creating a <c>.git</c> file.</summary>
    public string MakeGitFileRepository(params string[] segments)
    {
        string repositoryPath = CreateDirectory(segments);
        File.WriteAllText(System.IO.Path.Combine(repositoryPath, ".git"), "gitdir: /elsewhere");
        return repositoryPath;
    }

    /// <summary>Deletes the tree, clearing the read-only attributes git sets on pack files.</summary>
    public void Dispose()
    {
        if (!Directory.Exists(Path))
        {
            return;
        }

        try
        {
            foreach (string file in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(Path, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best-effort cleanup; a locked file must not fail the test run.
        }
    }

    private string PrepareFile(string relativePath)
    {
        string fullPath = System.IO.Path.Combine(Path, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
        return fullPath;
    }
}
