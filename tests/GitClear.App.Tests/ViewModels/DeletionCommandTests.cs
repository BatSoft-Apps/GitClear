using GitClear.App.Tests.TestSupport;
using GitClear.App.ViewModels;
using GitClear.Core.Deletion;
using GitClear.Core.Model;
using GitClear.Core.Scanning;

namespace GitClear.App.Tests.ViewModels;

public sealed class DeletionCommandTests
{
    private static readonly RepositoryInfo Repository = new RepositoryInfo(@"C:\root\a", false);

    [Fact]
    public async Task Delete_is_disabled_until_something_is_selected()
    {
        MainViewModel viewModel = MainViewModelFactory.Create(scanner: new FakeScanner(ScanResults.Of(new IgnoredFileEntry("a.log", 10))));
        viewModel.SelectedRepository = Repository;
        await viewModel.ActiveScan;

        Assert.False(viewModel.DeleteSelectedCommand.CanExecute(null));

        viewModel.RootNodes[0].Files.Single().IsChecked = true;
        Assert.True(viewModel.DeleteSelectedCommand.CanExecute(null));
    }

    [Fact]
    public async Task Confirming_deletes_the_checked_files_and_refreshes_the_tree()
    {
        FakeDeletionService deletion = new();
        // First scan finds two files; the post-delete refresh finds none.
        QueueScanner scanner = new(
            ScanResults.Of(new IgnoredFileEntry("a.log", 10), new IgnoredFileEntry("b.log", 20)),
            ScanResults.Of());
        MainViewModel viewModel = MainViewModelFactory.Create(scanner: scanner, deletion: deletion, confirmation: new StubConfirmation(true));

        viewModel.SelectedRepository = Repository;
        await viewModel.ActiveScan;
        viewModel.RootNodes[0].IsChecked = true; // select everything
        Assert.Equal(2, viewModel.Selection.SelectedFileCount);

        await viewModel.DeleteSelectedCommand.ExecuteAsync(null);

        Assert.Equal(
            new[] { @"C:\repo\a.log", @"C:\repo\b.log" },
            deletion.ReceivedPaths.OrderBy(p => p).ToArray());
        Assert.Empty(viewModel.RootNodes);                       // refreshed to reflect deletion (DEL-3)
        Assert.Equal(0, viewModel.Selection.SelectedFileCount);  // selection reset
        Assert.Contains("Moved 2 files", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Cancelling_the_confirmation_deletes_nothing()
    {
        FakeDeletionService deletion = new();
        MainViewModel viewModel = MainViewModelFactory.Create(
            scanner: new FakeScanner(ScanResults.Of(new IgnoredFileEntry("a.log", 10))),
            deletion: deletion,
            confirmation: new StubConfirmation(false));

        viewModel.SelectedRepository = Repository;
        await viewModel.ActiveScan;
        viewModel.RootNodes[0].Files.Single().IsChecked = true;

        await viewModel.DeleteSelectedCommand.ExecuteAsync(null);

        Assert.Empty(deletion.ReceivedPaths);
        Assert.Single(viewModel.RootNodes); // tree unchanged
    }

    [Fact]
    public async Task Only_checked_files_are_deleted()
    {
        FakeDeletionService deletion = new();
        QueueScanner scanner = new(
            ScanResults.Of(new IgnoredFileEntry("a.log", 10), new IgnoredFileEntry("b.log", 20)),
            ScanResults.Of(new IgnoredFileEntry("b.log", 20)));
        MainViewModel viewModel = MainViewModelFactory.Create(scanner: scanner, deletion: deletion, confirmation: new StubConfirmation(true));

        viewModel.SelectedRepository = Repository;
        await viewModel.ActiveScan;
        viewModel.RootNodes[0].Files.Single(f => f.Name == "a.log").IsChecked = true;

        await viewModel.DeleteSelectedCommand.ExecuteAsync(null);

        Assert.Equal(new[] { @"C:\repo\a.log" }, deletion.ReceivedPaths);
    }

    [Fact]
    public async Task A_fully_ignored_directory_is_deleted_as_a_single_target()
    {
        FakeDeletionService deletion = new();
        QueueScanner scanner = new(
            ScanResults.Of([], [new IgnoredDirectoryEntry("node_modules", 5000, 42)]),
            ScanResults.Of());
        MainViewModel viewModel = MainViewModelFactory.Create(scanner: scanner, deletion: deletion, confirmation: new StubConfirmation(true));

        viewModel.SelectedRepository = Repository;
        await viewModel.ActiveScan;
        viewModel.RootNodes[0].Subfolders.Single(f => f.Name == "node_modules").IsChecked = true;
        Assert.Equal(42, viewModel.Selection.SelectedFileCount);

        await viewModel.DeleteSelectedCommand.ExecuteAsync(null);

        string target = Assert.Single(deletion.ReceivedPaths); // one directory, not 42 files
        Assert.EndsWith("node_modules", target);
        Assert.Contains("Moved 42 files", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Undo_restores_the_last_deletion_and_refreshes()
    {
        FakeDeletionService deletion = new();
        QueueScanner scanner = new(
            ScanResults.Of(new IgnoredFileEntry("a.log", 10)), // initial scan
            ScanResults.Of(),                                  // refresh after delete (gone)
            ScanResults.Of(new IgnoredFileEntry("a.log", 10))); // refresh after undo (back)
        MainViewModel viewModel = MainViewModelFactory.Create(scanner: scanner, deletion: deletion, confirmation: new StubConfirmation(true));

        viewModel.SelectedRepository = Repository;
        await viewModel.ActiveScan;
        viewModel.RootNodes[0].Files.Single().IsChecked = true;
        await viewModel.DeleteSelectedCommand.ExecuteAsync(null);
        Assert.True(viewModel.UndoCommand.CanExecute(null));

        await viewModel.UndoCommand.ExecuteAsync(null);

        Assert.Equal(new[] { @"C:\repo\a.log" }, deletion.RestoredPaths);
        Assert.Single(viewModel.RootNodes);                     // file is back
        Assert.False(viewModel.UndoCommand.CanExecute(null));   // single-level undo is consumed
        Assert.Contains("Restored 1 file", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Undo_is_forgotten_when_the_repository_changes()
    {
        QueueScanner scanner = new(
            ScanResults.Of(new IgnoredFileEntry("a.log", 10)),
            ScanResults.Of());
        MainViewModel viewModel = MainViewModelFactory.Create(scanner: scanner, confirmation: new StubConfirmation(true));

        viewModel.SelectedRepository = Repository;
        await viewModel.ActiveScan;
        viewModel.RootNodes[0].Files.Single().IsChecked = true;
        await viewModel.DeleteSelectedCommand.ExecuteAsync(null);
        Assert.True(viewModel.UndoCommand.CanExecute(null));

        viewModel.SelectedRepository = null;
        await viewModel.ActiveScan;

        Assert.False(viewModel.UndoCommand.CanExecute(null));
    }

    [Fact]
    public async Task Declining_a_permanent_delete_warning_still_refreshes_and_offers_undo()
    {
        FakeDeletionService deletion = FakeDeletionService.Aborting();
        QueueScanner scanner = new(
            ScanResults.Of(new IgnoredFileEntry("a.log", 10)),
            ScanResults.Of());
        MainViewModel viewModel = MainViewModelFactory.Create(
            scanner: scanner, deletion: deletion, confirmation: new StubConfirmation(true));

        viewModel.SelectedRepository = Repository;
        await viewModel.ActiveScan;
        viewModel.RootNodes[0].Files.Single().IsChecked = true;

        await viewModel.DeleteSelectedCommand.ExecuteAsync(null);

        // Anything already recycled before the user backed out must stay undoable.
        Assert.True(viewModel.UndoCommand.CanExecute(null));
        Assert.Contains("nothing was permanently deleted", viewModel.StatusMessage);
        Assert.DoesNotContain("Moved 1 file to the Recycle Bin", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Deletion_failure_is_reported_in_the_status()
    {
        FakeDeletionService deletion = new(throwOnDelete: new DeletionException("locked"));
        MainViewModel viewModel = MainViewModelFactory.Create(
            scanner: new FakeScanner(ScanResults.Of(new IgnoredFileEntry("a.log", 10))),
            deletion: deletion,
            confirmation: new StubConfirmation(true));

        viewModel.SelectedRepository = Repository;
        await viewModel.ActiveScan;
        viewModel.RootNodes[0].Files.Single().IsChecked = true;

        await viewModel.DeleteSelectedCommand.ExecuteAsync(null);

        Assert.Contains("could not be deleted", viewModel.StatusMessage);
        Assert.False(viewModel.IsDeleting);
    }
}
