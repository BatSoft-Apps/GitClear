using GitClear.Core.Deletion;
using GitClear.Core.Tests.TestSupport;

namespace GitClear.Core.Tests.Deletion;

public sealed class RecycleBinDeletionServiceTests
{
    [Fact]
    public async Task Recycles_only_existing_files_and_counts_the_rest_as_skipped()
    {
        using TemporaryDirectory directory = new();
        string present1 = directory.CreateFile("a.txt", "x");
        string present2 = directory.CreateFile("b.txt", "x");
        string missing = Path.Combine(directory.Path, "gone.txt");

        FakeRecycleBin recycleBin = new();
        RecycleBinDeletionService service = new(recycleBin);

        DeletionResult result = await service.DeleteAsync([present1, present2, missing]);

        Assert.Equal(2, result.TargetsRecycled);
        Assert.Equal(1, result.TargetsSkipped);
        Assert.Equal(new[] { present1, present2 }, recycleBin.Recycled.OrderBy(p => p).ToArray());
    }

    [Fact]
    public async Task Does_not_call_the_shell_when_nothing_exists()
    {
        using TemporaryDirectory directory = new();
        string missing = Path.Combine(directory.Path, "gone.txt");

        FakeRecycleBin recycleBin = new();
        RecycleBinDeletionService service = new(recycleBin);

        DeletionResult result = await service.DeleteAsync([missing]);

        Assert.Equal(0, result.TargetsRecycled);
        Assert.Equal(1, result.TargetsSkipped);
        Assert.Empty(recycleBin.Recycled);
    }

    [Fact]
    public async Task Reports_aborted_when_the_user_declines_a_permanent_delete_warning()
    {
        using TemporaryDirectory directory = new();
        string present = directory.CreateFile("big.bin", "x");

        FakeRecycleBin recycleBin = new(completes: false);
        RecycleBinDeletionService service = new(recycleBin);

        DeletionResult result = await service.DeleteAsync([present]);

        Assert.True(result.Aborted);
    }

    [Fact]
    public async Task Is_not_aborted_on_a_normal_completed_delete()
    {
        using TemporaryDirectory directory = new();
        string present = directory.CreateFile("a.txt", "x");

        RecycleBinDeletionService service = new(new FakeRecycleBin());

        DeletionResult result = await service.DeleteAsync([present]);

        Assert.False(result.Aborted);
    }

    /// <param name="completes">False models the user declining a permanent-delete warning.</param>
    private sealed class FakeRecycleBin(bool completes = true) : IRecycleBinService
    {
        public List<string> Recycled { get; } = [];

        public bool Recycle(IReadOnlyList<string> paths)
        {
            Recycled.AddRange(paths);
            return completes;
        }

        public int Restore(IReadOnlyCollection<string> originalPaths) => originalPaths.Count;
    }
}
