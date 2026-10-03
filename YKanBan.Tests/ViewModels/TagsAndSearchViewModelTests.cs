using YKanBan.Models;
using YKanBan.Storage.AppData;
using YKanBan.Storage.Workspace;
using YKanBan.Tests.TestUtilities;
using YKanBan.ViewModels;
using YKanBan.ViewModels.Board;
using YKanBan.ViewModels.Dialogs;
using YKanBan.ViewModels.Tags;

namespace YKanBan.Tests.ViewModels;

/// <summary>
/// View-model tests of the search bar, the tags tab, the cross-tab refresh and
/// the export wiring, against a real temporary workspace with a scripted
/// dialog service.
/// </summary>
[TestClass]
public class TagsAndSearchViewModelTests
{
    private sealed class Fixture : IDisposable
    {
        public TempDirectory Directory { get; } = new();

        public WorkspaceSession Session { get; }

        public WorkspaceRepository Repository => Session.Repository;

        public AppConfig Config { get; } = new();

        public ScriptedDialogService Dialogs { get; } = new();

        public Fixture()
        {
            WorkspaceInitializer.Initialize(Directory.FullPath);
            Session = WorkspaceSession.Open(Directory.FullPath);
        }

        public BoardViewModel CreateBoard() => new(Repository, Config, Dialogs);

        public TagsViewModel CreateTags() => new(Repository, Config, Dialogs);

        public WorkspaceViewModel CreateWorkspace() =>
            new(Session, new Interaction<Unit>(), new Interaction<Unit>(), Config, Dialogs);

        public void Dispose()
        {
            Session.Dispose();
            Directory.Dispose();
        }
    }

    private static string[] VisibleTitles(BoardViewModel board) =>
        board.Columns.SelectMany(column => column.Cards).Select(card => card.Title).Order(StringComparer.Ordinal).ToArray();

    /// <summary>Moves a visible card through its menu command.</summary>
    private static Task MoveAsync(BoardViewModel board, long cardId, long columnId) =>
        board.Columns.SelectMany(column => column.Cards).Single(card => card.Card.Id == cardId)
            .MoveToCommand.ExecuteAsync(columnId);

    #region Search

    [TestMethod]
    public async Task SearchFiltersCardsAndKeepsEmptyColumns()
    {
        using var fixture = new Fixture();
        ColumnModel first = fixture.Repository.AddColumn("A", "");
        ColumnModel second = fixture.Repository.AddColumn("B", "");
        TagModel bug = fixture.Repository.AddTag("bug", new RgbColor(1, 2, 3), "");
        fixture.Repository.AddCard(first.Id, "crash", "", [bug.Id]);
        fixture.Repository.AddCard(second.Id, "idea", "", []);
        BoardViewModel board = fixture.CreateBoard();

        board.SearchText = "tag:BUG";
        await board.SearchCommand.ExecuteAsync(null);

        CollectionAssert.AreEqual(new[] { "crash" }, VisibleTitles(board));
        Assert.AreEqual(2, board.Columns.Count);
        Assert.AreEqual(0, board.Columns[1].CardCount);
        Assert.IsNotNull(board.ActiveFilter);
        Assert.AreEqual(0, fixture.Dialogs.Shown.Count);
    }

    [TestMethod]
    public async Task InvalidSearchShowsTheErrorAndKeepsTextAndPreviousResult()
    {
        using var fixture = new Fixture();
        ColumnModel column = fixture.Repository.AddColumn("A", "");
        fixture.Repository.AddCard(column.Id, "alpha", "", []);
        fixture.Repository.AddCard(column.Id, "beta", "", []);
        BoardViewModel board = fixture.CreateBoard();
        board.SearchText = "alpha";
        await board.SearchCommand.ExecuteAsync(null);

        board.SearchText = "alpha AND";
        await board.SearchCommand.ExecuteAsync(null);

        var error = (MessageDialogViewModel)fixture.Dialogs.Shown.Single();
        Assert.AreEqual(Resources.Search_Error_Title, error.Title);
        Assert.AreEqual("alpha AND", board.SearchText);
        CollectionAssert.AreEqual(new[] { "alpha" }, VisibleTitles(board));
    }

    [TestMethod]
    public async Task WritesRefreshWithTheLastSuccessfulExpressionNotTheBoxText()
    {
        using var fixture = new Fixture();
        ColumnModel first = fixture.Repository.AddColumn("A", "");
        ColumnModel second = fixture.Repository.AddColumn("B", "");
        CardModel alpha = fixture.Repository.AddCard(first.Id, "alpha", "", []);
        fixture.Repository.AddCard(first.Id, "beta", "", []);
        BoardViewModel board = fixture.CreateBoard();
        board.SearchText = "alpha";
        await board.SearchCommand.ExecuteAsync(null);

        board.SearchText = "beta";
        await MoveAsync(board, alpha.Id, second.Id);

        CollectionAssert.AreEqual(new[] { "alpha" }, VisibleTitles(board));
        Assert.AreEqual(1, board.Columns[1].CardCount);
    }

    [TestMethod]
    public async Task BlankSearchAndClearShowEveryCard()
    {
        using var fixture = new Fixture();
        ColumnModel column = fixture.Repository.AddColumn("A", "");
        fixture.Repository.AddCard(column.Id, "alpha", "", []);
        fixture.Repository.AddCard(column.Id, "beta", "", []);
        BoardViewModel board = fixture.CreateBoard();

        board.SearchText = "alpha";
        await board.SearchCommand.ExecuteAsync(null);
        board.SearchText = "   ";
        await board.SearchCommand.ExecuteAsync(null);
        Assert.IsNull(board.ActiveFilter);
        CollectionAssert.AreEqual(new[] { "alpha", "beta" }, VisibleTitles(board));

        board.SearchText = "alpha";
        await board.SearchCommand.ExecuteAsync(null);
        Assert.IsTrue(board.HasSearchText);
        await board.ClearSearchCommand.ExecuteAsync(null);

        Assert.AreEqual("", board.SearchText);
        Assert.IsFalse(board.HasSearchText);
        Assert.IsNull(board.ActiveFilter);
        CollectionAssert.AreEqual(new[] { "alpha", "beta" }, VisibleTitles(board));
    }

    [TestMethod]
    public async Task DataWrittenIsRaisedForWritesButNotForSearchOrSort()
    {
        using var fixture = new Fixture();
        ColumnModel first = fixture.Repository.AddColumn("A", "");
        ColumnModel second = fixture.Repository.AddColumn("B", "");
        CardModel card = fixture.Repository.AddCard(first.Id, "x", "", []);
        BoardViewModel board = fixture.CreateBoard();
        int raised = 0;
        board.DataWritten += (_, _) => raised++;

        board.SearchText = "x";
        await board.SearchCommand.ExecuteAsync(null);
        board.SelectedSort = board.SortOptions[1];
        Assert.AreEqual(0, raised);

        await MoveAsync(board, card.Id, second.Id);
        Assert.AreEqual(1, raised);
    }

    #endregion

    #region Tags tab

    [TestMethod]
    public void TagsListInIdOrderWithUsageCounts()
    {
        using var fixture = new Fixture();
        ColumnModel column = fixture.Repository.AddColumn("A", "");
        TagModel bug = fixture.Repository.AddTag("bug", new RgbColor(1, 2, 3), "Broken");
        TagModel unused = fixture.Repository.AddTag("unused", new RgbColor(4, 5, 6), "");
        fixture.Repository.AddCard(column.Id, "a", "", [bug.Id]);
        fixture.Repository.AddCard(column.Id, "b", "", [bug.Id]);

        TagsViewModel tags = fixture.CreateTags();

        CollectionAssert.AreEqual(new[] { bug.Id, unused.Id }, tags.Tags.Select(row => row.Tag.Id).ToArray());
        CollectionAssert.AreEqual(new long[] { 2, 0 }, tags.Tags.Select(row => row.UsageCount).ToArray());
        Assert.IsTrue(tags.Tags[0].HasDescription);
        Assert.AreEqual("#010203", tags.Tags[0].ColorText);
        Assert.IsFalse(tags.IsEmpty);
    }

    [TestMethod]
    public async Task NewTagIsWrittenAndAnnounced()
    {
        using var fixture = new Fixture();
        TagsViewModel tags = fixture.CreateTags();
        int changed = 0;
        tags.TagsChanged += (_, _) => changed++;
        fixture.Dialogs.Script = dialog =>
        {
            var editor = (TagEditorDialogViewModel)dialog;
            editor.Name = "ui";
            editor.PickSwatchCommand.Execute(TagColors.Palette[2].ToString());
            editor.Description = "Looks";
            editor.ConfirmCommand.Execute(null);
            return Task.CompletedTask;
        };

        await tags.AddTagCommand.ExecuteAsync(null);

        TagModel created = fixture.Repository.GetTags().Single();
        Assert.AreEqual("ui", created.Name);
        Assert.AreEqual(TagColors.Palette[2], created.Color);
        Assert.AreEqual("Looks", created.Description);
        Assert.AreEqual(1, tags.Tags.Count);
        Assert.AreEqual(1, changed);
    }

    [TestMethod]
    public async Task TagEditorKeepsTheDialogOpenOnDuplicateNameOrInvalidColor()
    {
        using var fixture = new Fixture();
        fixture.Repository.AddTag("Bug", new RgbColor(1, 2, 3), "");
        TagsViewModel tags = fixture.CreateTags();
        TagEditorDialogViewModel? editor = null;
        bool closed = false;
        fixture.Dialogs.Script = dialog =>
        {
            editor = (TagEditorDialogViewModel)dialog;
            editor.CloseRequested += (_, _) => closed = true;
            editor.Name = "bug";
            editor.ConfirmCommand.Execute(null);
            return Task.CompletedTask;
        };

        await tags.AddTagCommand.ExecuteAsync(null);

        Assert.IsNotNull(editor);
        Assert.IsTrue(editor.HasError);
        Assert.IsFalse(closed);
        Assert.AreEqual("bug", editor.Name);

        editor.Name = "other";
        editor.Color = "red";
        editor.ConfirmCommand.Execute(null);
        Assert.AreEqual(Resources.Error_Tag_InvalidColor, editor.ErrorMessage);
        Assert.IsFalse(closed);
        Assert.AreEqual(1, fixture.Repository.GetTags().Count);
    }

    [TestMethod]
    public async Task EditingATagRenamesRecolorsAndDescribesIt()
    {
        using var fixture = new Fixture();
        TagModel tag = fixture.Repository.AddTag("bug", new RgbColor(1, 2, 3), "");
        TagsViewModel tags = fixture.CreateTags();
        fixture.Dialogs.Script = dialog =>
        {
            var editor = (TagEditorDialogViewModel)dialog;
            Assert.AreEqual("bug", editor.Name);
            Assert.AreEqual("#010203", editor.Color);
            editor.Name = "defect";
            editor.Color = "#ABCDEF";
            editor.Description = "Broken";
            editor.ConfirmCommand.Execute(null);
            return Task.CompletedTask;
        };

        await tags.Tags[0].EditCommand.ExecuteAsync(null);

        TagModel updated = fixture.Repository.GetTags().Single();
        Assert.AreEqual(tag.Id, updated.Id);
        Assert.AreEqual("defect", updated.Name);
        Assert.AreEqual(new RgbColor(0xab, 0xcd, 0xef), updated.Color);
        Assert.AreEqual("Broken", updated.Description);
        Assert.AreEqual("defect", tags.Tags[0].Badge.Name);
    }

    [TestMethod]
    public async Task DeletingATagAsksFirstAndKeepsTheCards()
    {
        using var fixture = new Fixture();
        ColumnModel column = fixture.Repository.AddColumn("A", "");
        TagModel bug = fixture.Repository.AddTag("bug", new RgbColor(1, 2, 3), "");
        CardModel card = fixture.Repository.AddCard(column.Id, "a", "", [bug.Id]);
        TagsViewModel tags = fixture.CreateTags();

        fixture.Dialogs.Script = ScriptedDialogService.AnswerConfirmations(false);
        await tags.Tags[0].DeleteCommand.ExecuteAsync(null);
        Assert.AreEqual(1, fixture.Repository.GetTags().Count);

        fixture.Dialogs.Script = ScriptedDialogService.AnswerConfirmations(true);
        await tags.Tags[0].DeleteCommand.ExecuteAsync(null);

        Assert.AreEqual(0, fixture.Repository.GetTags().Count);
        Assert.IsTrue(tags.IsEmpty);
        CardModel kept = fixture.Repository.GetCard(card.Id)!;
        Assert.AreEqual(0, kept.Tags.Count);
    }

    [TestMethod]
    public async Task DeletingATagWithoutConfirmationShowsNoDialog()
    {
        using var fixture = new Fixture();
        fixture.Repository.AddTag("bug", new RgbColor(1, 2, 3), "");
        fixture.Config.Confirmations.DeleteTag = false;
        TagsViewModel tags = fixture.CreateTags();

        await tags.Tags[0].DeleteCommand.ExecuteAsync(null);

        Assert.AreEqual(0, fixture.Dialogs.Shown.Count);
        Assert.AreEqual(0, fixture.Repository.GetTags().Count);
    }

    [TestMethod]
    public async Task TagReadFailuresAreReportedInsteadOfThrown()
    {
        using var fixture = new Fixture();
        fixture.Repository.AddTag("bug", new RgbColor(1, 2, 3), "");
        fixture.Config.Confirmations.DeleteTag = false;
        TagsViewModel tags = fixture.CreateTags();

        // Every read and write fails from now on.
        fixture.Repository.Dispose();
        await tags.Tags[0].DeleteCommand.ExecuteAsync(null);

        CollectionAssert.AreEqual(
            new[] { Resources.Error_Operation_Title, Resources.Error_Read_Title },
            fixture.Dialogs.Shown.Select(dialog => ((MessageDialogViewModel)dialog).Title).ToArray());
        Assert.AreEqual("bug", tags.Tags.Single().Tag.Name);
    }

    #endregion

    #region Workspace wiring and export

    [TestMethod]
    public async Task TagWritesRefreshTheBoardAndBoardWritesRefreshTheTags()
    {
        using var fixture = new Fixture();
        ColumnModel first = fixture.Repository.AddColumn("A", "");
        ColumnModel second = fixture.Repository.AddColumn("B", "");
        TagModel bug = fixture.Repository.AddTag("bug", new RgbColor(1, 2, 3), "");
        CardModel card = fixture.Repository.AddCard(first.Id, "a", "", [bug.Id]);
        WorkspaceViewModel workspace = fixture.CreateWorkspace();

        // A filtered board drops the card once the tag is gone.
        workspace.Board.SearchText = "tag:bug";
        await workspace.Board.SearchCommand.ExecuteAsync(null);
        fixture.Dialogs.Script = ScriptedDialogService.AnswerConfirmations(true);
        await workspace.Tags.Tags[0].DeleteCommand.ExecuteAsync(null);
        Assert.AreEqual(0, workspace.Board.Columns.Sum(column => column.CardCount));

        // Re-tag through the repository, then a board write updates the usage count.
        await workspace.Board.ClearSearchCommand.ExecuteAsync(null);
        TagModel ui = fixture.Repository.AddTag("ui", new RgbColor(4, 5, 6), "");
        fixture.Repository.UpdateCard(card.Id, "a", "", [ui.Id]);
        Assert.AreEqual(0, workspace.Tags.Tags.Count);
        await MoveAsync(workspace.Board, card.Id, second.Id);
        Assert.AreEqual(1, workspace.Tags.Tags.Single().UsageCount);
    }

    [TestMethod]
    public async Task ExportWritesTheWholeWorkspaceIgnoringTheSearch()
    {
        using var fixture = new Fixture();
        ColumnModel column = fixture.Repository.AddColumn("A", "");
        fixture.Repository.AddCard(column.Id, "alpha", "", []);
        fixture.Repository.AddCard(column.Id, "beta", "", []);
        WorkspaceViewModel workspace = fixture.CreateWorkspace();
        workspace.Board.SearchText = "alpha";
        await workspace.Board.SearchCommand.ExecuteAsync(null);

        string path = Path.Combine(fixture.Directory.FullPath, "out.md");
        fixture.Dialogs.SaveFilePath = path;
        await workspace.ExportCommand.ExecuteAsync(null);

        string suggested = fixture.Dialogs.SuggestedFileNames.Single();
        StringAssert.StartsWith(suggested, workspace.WorkspaceName);
        StringAssert.EndsWith(suggested, ".md");
        string markdown = File.ReadAllText(path);
        StringAssert.Contains(markdown, "alpha");
        StringAssert.Contains(markdown, "beta");
        byte[] bytes = File.ReadAllBytes(path);
        Assert.AreNotEqual(0xEF, bytes[0], "The export must not start with a UTF-8 BOM.");
        Assert.AreEqual(0, fixture.Dialogs.Shown.Count);
    }

    [TestMethod]
    public async Task CancelledExportWritesNothing()
    {
        using var fixture = new Fixture();
        WorkspaceViewModel workspace = fixture.CreateWorkspace();
        fixture.Dialogs.SaveFilePath = null;

        await workspace.ExportCommand.ExecuteAsync(null);

        Assert.AreEqual(1, fixture.Dialogs.SuggestedFileNames.Count);
        Assert.AreEqual(0, fixture.Dialogs.Shown.Count);
        Assert.AreEqual(0, System.IO.Directory.GetFiles(fixture.Directory.FullPath, "*.md").Length);
    }

    [TestMethod]
    public async Task ExportWriteFailureIsReportedInAMessageDialog()
    {
        using var fixture = new Fixture();
        WorkspaceViewModel workspace = fixture.CreateWorkspace();
        fixture.Dialogs.SaveFilePath = Path.Combine(fixture.Directory.FullPath, "missing-folder", "out.md");

        await workspace.ExportCommand.ExecuteAsync(null);

        var message = (MessageDialogViewModel)fixture.Dialogs.Shown.Single();
        Assert.AreEqual(Resources.Export_Failed_Title, message.Title);
        Assert.IsTrue(message.HasDetails);
    }

    [TestMethod]
    public async Task ExportReadFailureIsReportedAndLeavesTheTargetUntouched()
    {
        using var fixture = new Fixture();
        WorkspaceViewModel workspace = fixture.CreateWorkspace();
        string path = Path.Combine(fixture.Directory.FullPath, "out.md");
        File.WriteAllText(path, "previous");
        fixture.Dialogs.SaveFilePath = path;

        fixture.Repository.Dispose();
        await workspace.ExportCommand.ExecuteAsync(null);

        var message = (MessageDialogViewModel)fixture.Dialogs.Shown.Single();
        Assert.AreEqual(Resources.Export_Failed_Title, message.Title);
        Assert.AreEqual("previous", File.ReadAllText(path));
    }

    #endregion
}
