using GitClear.Core.Deletion;
using GitClear.Core.Tests.TestSupport;

namespace GitClear.Core.Tests.Deletion;

public sealed class RecycleBinDeletionServiceTests
{
    [Fact]
    public async Task Recycles_only_existing_files_and_counts_the_rest_as_skipped()
    {
        using var temp = new TempDirectory();
        var present1 = temp.CreateFile("a.txt", "x");
        var present2 = temp.CreateFile("b.txt", "x");
        var missing = Path.Combine(temp.Path, "gone.txt");

        var recycleBin = new FakeRecycleBin();
        var sut = new RecycleBinDeletionService(recycleBin);

        var result = await sut.DeleteAsync([present1, present2, missing]);

        Assert.Equal(2, result.FilesDeleted);
        Assert.Equal(1, result.FilesSkipped);
        Assert.Equal(new[] { present1, present2 }, recycleBin.Recycled.OrderBy(p => p).ToArray());
    }

    [Fact]
    public async Task Does_not_call_the_shell_when_nothing_exists()
    {
        using var temp = new TempDirectory();
        var missing = Path.Combine(temp.Path, "gone.txt");

        var recycleBin = new FakeRecycleBin();
        var sut = new RecycleBinDeletionService(recycleBin);

        var result = await sut.DeleteAsync([missing]);

        Assert.Equal(0, result.FilesDeleted);
        Assert.Equal(1, result.FilesSkipped);
        Assert.Empty(recycleBin.Recycled);
    }

    [Fact]
    public async Task Reports_aborted_when_the_user_declines_a_permanent_delete_warning()
    {
        using TempDirectory temp = new();
        string present = temp.CreateFile("big.bin", "x");

        FakeRecycleBin recycleBin = new() { Completes = false };
        RecycleBinDeletionService sut = new(recycleBin);

        DeletionResult result = await sut.DeleteAsync([present]);

        Assert.True(result.Aborted);
    }

    [Fact]
    public async Task Is_not_aborted_on_a_normal_completed_delete()
    {
        using TempDirectory temp = new();
        string present = temp.CreateFile("a.txt", "x");

        RecycleBinDeletionService sut = new(new FakeRecycleBin());

        DeletionResult result = await sut.DeleteAsync([present]);

        Assert.False(result.Aborted);
    }

    private sealed class FakeRecycleBin : IRecycleBinService
    {
        public List<string> Recycled { get; } = [];

        public bool Completes { get; set; } = true;

        public bool Recycle(IReadOnlyList<string> paths)
        {
            Recycled.AddRange(paths);
            return Completes;
        }

        public int Restore(IReadOnlyCollection<string> originalPaths) => originalPaths.Count;
    }
}
