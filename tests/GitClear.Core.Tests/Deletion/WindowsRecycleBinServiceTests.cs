using GitClear.Core.Deletion;

namespace GitClear.Core.Tests.Deletion;

/// <summary>
/// Exercises the real shell interop (SHFileOperation + Shell.Application restore)
/// to catch marshalling mistakes. Uses a throwaway temp file and restores it, so
/// the round-trip leaves nothing behind in the Recycle Bin.
/// </summary>
public sealed class WindowsRecycleBinServiceTests
{
    [Fact]
    public void Recycle_then_restore_round_trips_a_file()
    {
        WindowsRecycleBinService recycleBin = new();
        string path = Path.Combine(Path.GetTempPath(), $"gitclear-recycle-{Guid.NewGuid():N}.tmp");
        File.WriteAllText(path, "throwaway");

        try
        {
            recycleBin.Recycle([path]);
            Assert.False(File.Exists(path), "file should have been moved to the Recycle Bin");

            int restored = recycleBin.Restore([path]);

            Assert.Equal(1, restored);
            Assert.True(File.Exists(path), "file should have been restored to its original location");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Recycle_then_restore_round_trips_a_whole_directory()
    {
        WindowsRecycleBinService recycleBin = new();
        string directory = Path.Combine(Path.GetTempPath(), $"gitclear-recycle-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "a.txt"), "x");
        File.WriteAllText(Path.Combine(directory, "b.txt"), "y");

        try
        {
            recycleBin.Recycle([directory]);
            Assert.False(Directory.Exists(directory), "directory should have been moved to the Recycle Bin");

            int restored = recycleBin.Restore([directory]);

            Assert.Equal(1, restored);
            Assert.True(Directory.Exists(directory), "directory should have been restored");
            Assert.True(File.Exists(Path.Combine(directory, "a.txt")), "directory contents should be restored");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void Recycling_an_empty_list_is_a_no_op()
    {
        WindowsRecycleBinService recycleBin = new();

        recycleBin.Recycle([]); // must not throw
    }

    [Fact]
    public void Restoring_an_empty_list_returns_zero()
    {
        WindowsRecycleBinService recycleBin = new();

        Assert.Equal(0, recycleBin.Restore([]));
    }
}
