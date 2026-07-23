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
        var sut = new WindowsRecycleBinService();
        var path = Path.Combine(Path.GetTempPath(), $"gitclear-recycle-{Guid.NewGuid():N}.tmp");
        File.WriteAllText(path, "throwaway");

        try
        {
            sut.Recycle([path]);
            Assert.False(File.Exists(path), "file should have been moved to the Recycle Bin");

            var restored = sut.Restore([path]);

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
        var sut = new WindowsRecycleBinService();
        var dir = Path.Combine(Path.GetTempPath(), $"gitclear-recycle-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "a.txt"), "x");
        File.WriteAllText(Path.Combine(dir, "b.txt"), "y");

        try
        {
            sut.Recycle([dir]);
            Assert.False(Directory.Exists(dir), "directory should have been moved to the Recycle Bin");

            var restored = sut.Restore([dir]);

            Assert.Equal(1, restored);
            Assert.True(Directory.Exists(dir), "directory should have been restored");
            Assert.True(File.Exists(Path.Combine(dir, "a.txt")), "directory contents should be restored");
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void Recycling_an_empty_list_is_a_no_op()
    {
        var sut = new WindowsRecycleBinService();

        sut.Recycle([]); // must not throw
    }

    [Fact]
    public void Restoring_an_empty_list_returns_zero()
    {
        var sut = new WindowsRecycleBinService();

        Assert.Equal(0, sut.Restore([]));
    }
}
