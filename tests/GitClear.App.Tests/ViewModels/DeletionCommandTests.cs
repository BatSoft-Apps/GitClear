using GitClear.App.Tests.TestSupport;
using GitClear.Core.Deletion;
using GitClear.Core.Model;
using GitClear.Core.Scanning;

namespace GitClear.App.Tests.ViewModels;

public sealed class DeletionCommandTests
{
    private static readonly RepositoryInfo Repo = RepositoryInfo.Create(@"C:\root\a", false);

    [Fact]
    public async Task Delete_is_disabled_until_something_is_selected()
    {
        var sut = Sut.Create(scanner: new FakeScanner(Sut.Result(new IgnoredFileEntry("a.log", 10))));
        sut.SelectedRepository = Repo;
        await sut.ActiveScan;

        Assert.False(sut.DeleteSelectedCommand.CanExecute(null));

        sut.RootNodes[0].Files.Single().IsChecked = true;
        Assert.True(sut.DeleteSelectedCommand.CanExecute(null));
    }

    [Fact]
    public async Task Confirming_deletes_the_checked_files_and_refreshes_the_tree()
    {
        var deletion = new FakeDeletionService();
        // First scan finds two files; the post-delete refresh finds none.
        var scanner = new QueueScanner(
            Sut.Result(new IgnoredFileEntry("a.log", 10), new IgnoredFileEntry("b.log", 20)),
            Sut.Result());
        var sut = Sut.Create(scanner: scanner, deletion: deletion, confirmation: new ConfirmationStub(true));

        sut.SelectedRepository = Repo;
        await sut.ActiveScan;
        sut.RootNodes[0].IsChecked = true; // select everything
        Assert.Equal(2, sut.Selection.SelectedFileCount);

        await sut.DeleteSelectedCommand.ExecuteAsync(null);

        Assert.Equal(
            new[] { @"C:\repo\a.log", @"C:\repo\b.log" },
            deletion.ReceivedPaths.OrderBy(p => p).ToArray());
        Assert.Empty(sut.RootNodes);                       // refreshed to reflect deletion (DEL-3)
        Assert.Equal(0, sut.Selection.SelectedFileCount);  // selection reset
        Assert.Contains("Moved 2 files", sut.StatusMessage);
    }

    [Fact]
    public async Task Cancelling_the_confirmation_deletes_nothing()
    {
        var deletion = new FakeDeletionService();
        var sut = Sut.Create(
            scanner: new FakeScanner(Sut.Result(new IgnoredFileEntry("a.log", 10))),
            deletion: deletion,
            confirmation: new ConfirmationStub(false));

        sut.SelectedRepository = Repo;
        await sut.ActiveScan;
        sut.RootNodes[0].Files.Single().IsChecked = true;

        await sut.DeleteSelectedCommand.ExecuteAsync(null);

        Assert.Empty(deletion.ReceivedPaths);
        Assert.Single(sut.RootNodes); // tree unchanged
    }

    [Fact]
    public async Task Only_checked_files_are_deleted()
    {
        var deletion = new FakeDeletionService();
        var scanner = new QueueScanner(
            Sut.Result(new IgnoredFileEntry("a.log", 10), new IgnoredFileEntry("b.log", 20)),
            Sut.Result(new IgnoredFileEntry("b.log", 20)));
        var sut = Sut.Create(scanner: scanner, deletion: deletion, confirmation: new ConfirmationStub(true));

        sut.SelectedRepository = Repo;
        await sut.ActiveScan;
        sut.RootNodes[0].Files.Single(f => f.Name == "a.log").IsChecked = true;

        await sut.DeleteSelectedCommand.ExecuteAsync(null);

        Assert.Equal(new[] { @"C:\repo\a.log" }, deletion.ReceivedPaths);
    }

    [Fact]
    public async Task A_fully_ignored_directory_is_deleted_as_a_single_target()
    {
        var deletion = new FakeDeletionService();
        var scanner = new QueueScanner(
            Sut.ResultWith([], [new IgnoredDirectoryEntry("node_modules", 5000, 42)]),
            Sut.Result());
        var sut = Sut.Create(scanner: scanner, deletion: deletion, confirmation: new ConfirmationStub(true));

        sut.SelectedRepository = Repo;
        await sut.ActiveScan;
        sut.RootNodes[0].Subfolders.Single(f => f.Name == "node_modules").IsChecked = true;
        Assert.Equal(42, sut.Selection.SelectedFileCount);

        await sut.DeleteSelectedCommand.ExecuteAsync(null);

        var target = Assert.Single(deletion.ReceivedPaths); // one directory, not 42 files
        Assert.EndsWith("node_modules", target);
        Assert.Contains("Moved 42 files", sut.StatusMessage);
    }

    [Fact]
    public async Task Undo_restores_the_last_deletion_and_refreshes()
    {
        var deletion = new FakeDeletionService();
        var scanner = new QueueScanner(
            Sut.Result(new IgnoredFileEntry("a.log", 10)), // initial scan
            Sut.Result(),                                  // refresh after delete (gone)
            Sut.Result(new IgnoredFileEntry("a.log", 10))); // refresh after undo (back)
        var sut = Sut.Create(scanner: scanner, deletion: deletion, confirmation: new ConfirmationStub(true));

        sut.SelectedRepository = Repo;
        await sut.ActiveScan;
        sut.RootNodes[0].Files.Single().IsChecked = true;
        await sut.DeleteSelectedCommand.ExecuteAsync(null);
        Assert.True(sut.UndoCommand.CanExecute(null));

        await sut.UndoCommand.ExecuteAsync(null);

        Assert.Equal(new[] { @"C:\repo\a.log" }, deletion.RestoredPaths);
        Assert.Single(sut.RootNodes);                     // file is back
        Assert.False(sut.UndoCommand.CanExecute(null));   // single-level undo is consumed
        Assert.Contains("Restored 1 file", sut.StatusMessage);
    }

    [Fact]
    public async Task Undo_is_forgotten_when_the_repository_changes()
    {
        var scanner = new QueueScanner(
            Sut.Result(new IgnoredFileEntry("a.log", 10)),
            Sut.Result());
        var sut = Sut.Create(scanner: scanner, confirmation: new ConfirmationStub(true));

        sut.SelectedRepository = Repo;
        await sut.ActiveScan;
        sut.RootNodes[0].Files.Single().IsChecked = true;
        await sut.DeleteSelectedCommand.ExecuteAsync(null);
        Assert.True(sut.UndoCommand.CanExecute(null));

        sut.SelectedRepository = null;
        await sut.ActiveScan;

        Assert.False(sut.UndoCommand.CanExecute(null));
    }

    [Fact]
    public async Task Deletion_failure_is_reported_in_the_status()
    {
        var deletion = new FakeDeletionService(throwOnDelete: new DeletionException("locked"));
        var sut = Sut.Create(
            scanner: new FakeScanner(Sut.Result(new IgnoredFileEntry("a.log", 10))),
            deletion: deletion,
            confirmation: new ConfirmationStub(true));

        sut.SelectedRepository = Repo;
        await sut.ActiveScan;
        sut.RootNodes[0].Files.Single().IsChecked = true;

        await sut.DeleteSelectedCommand.ExecuteAsync(null);

        Assert.Contains("could not be deleted", sut.StatusMessage);
        Assert.False(sut.IsDeleting);
    }
}
