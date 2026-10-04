using YKanBan.Models;
using YKanBan.Search;
using YKanBan.Storage;
using YKanBan.Storage.AppData;
using YKanBan.Storage.Workspace;
using YKanBan.Tests.TestUtilities;
using YKanBan.ViewModels.Board;
using YKanBan.ViewModels.Dialogs;

namespace YKanBan.Tests.ViewModels;

/// <summary>
/// View-model tests of the board and its dialogs against a real temporary
/// workspace, with a scripted dialog service standing in for the modal windows.
/// </summary>
[TestClass]
public class BoardViewModelTests
{
    private sealed class Fixture : IDisposable
    {
        public TempDirectory Directory { get; } = new();

        public WorkspaceRepository Repository { get; }

        public AppConfig Config { get; } = new();

        public ScriptedDialogService Dialogs { get; } = new();

        public Fixture()
        {
            WorkspaceInitializer.Initialize(Directory.FullPath);
            Repository = new WorkspaceRepository(Directory.FullPath);
        }

        public BoardViewModel CreateBoard() => new(Repository, Config, Dialogs);

        public void Dispose()
        {
            Repository.Dispose();
            Directory.Dispose();
        }
    }

    [TestMethod]
    public void RefreshListsColumnsInIdOrderAndSortsCardsWithinEachColumn()
    {
        using var fixture = new Fixture();
        ColumnModel first = fixture.Repository.AddColumn("A", "");
        ColumnModel second = fixture.Repository.AddColumn("B", "");
        fixture.Repository.AddCard(first.Id, "zeta", "", []);
        fixture.Repository.AddCard(first.Id, "alpha", "", []);
        fixture.Repository.AddCard(second.Id, "x", "", []);

        fixture.Config.Sort.Card = CardSortOption.Title;
        BoardViewModel board = fixture.CreateBoard();

        CollectionAssert.AreEqual(new[] { "A", "B" }, board.Columns.Select(column => column.Title).ToArray());
        CollectionAssert.AreEqual(new[] { "alpha", "zeta" }, board.Columns[0].Cards.Select(card => card.Title).ToArray());
        Assert.AreEqual(1, board.Columns[1].CardCount);
    }

    [TestMethod]
    public void ChangingTheSortPersistsItToTheConfigurationAndResorts()
    {
        using var fixture = new Fixture();
        ColumnModel column = fixture.Repository.AddColumn("A", "");
        fixture.Repository.AddCard(column.Id, "b", "", []);
        fixture.Repository.AddCard(column.Id, "a", "", []);
        BoardViewModel board = fixture.CreateBoard();
        CollectionAssert.AreEqual(new[] { "b", "a" }, board.Columns[0].Cards.Select(card => card.Title).ToArray());

        board.SelectedSort = board.SortOptions.Single(option => (CardSortOption)option.Value == CardSortOption.Title);

        Assert.AreEqual(CardSortOption.Title, fixture.Config.Sort.Card);
        CollectionAssert.AreEqual(new[] { "a", "b" }, board.Columns[0].Cards.Select(card => card.Title).ToArray());
    }

    [TestMethod]
    public async Task WritesRefreshWithTheActiveFilterAndKeepEmptyColumns()
    {
        using var fixture = new Fixture();
        ColumnModel todo = fixture.Repository.AddColumn("Todo", "");
        ColumnModel done = fixture.Repository.AddColumn("Done", "");
        CardModel match = fixture.Repository.AddCard(todo.Id, "match me", "", []);
        fixture.Repository.AddCard(todo.Id, "other", "", []);

        BoardViewModel board = fixture.CreateBoard();
        board.ActiveFilter = SearchSqlCompiler.Compile(SearchQueryParser.Parse("match")!);
        board.Refresh();
        Assert.AreEqual(1, board.Columns[0].CardCount);

        // A move is a write: the board re-queries with the active filter.
        CardViewModel card = board.Columns[0].Cards.Single();
        MoveTargetViewModel target = card.MoveTargets.Single(entry => entry.ColumnId == done.Id);
        Assert.IsFalse(card.MoveTargets.Single(entry => entry.ColumnId == todo.Id).IsEnabled);
        await target.Command.ExecuteAsync(target.ColumnId);

        Assert.AreEqual(2, board.Columns.Count, "Columns emptied by the filter stay on the board.");
        Assert.AreEqual(0, board.Columns[0].CardCount);
        Assert.AreEqual(match.Id, board.Columns[1].Cards.Single().Card.Id);
    }

    [TestMethod]
    public async Task DeclinedColumnDeletionKeepsTheColumnAndShowsItsCardCount()
    {
        using var fixture = new Fixture();
        ColumnModel column = fixture.Repository.AddColumn("A", "");
        fixture.Repository.AddCard(column.Id, "", "", []);
        fixture.Repository.AddCard(column.Id, "", "", []);
        BoardViewModel board = fixture.CreateBoard();
        fixture.Dialogs.Script = ScriptedDialogService.AnswerConfirmations(false);

        await board.Columns[0].DeleteCommand.ExecuteAsync(null);

        var confirmation = (ConfirmDialogViewModel)fixture.Dialogs.Shown.Single();
        StringAssert.Contains(confirmation.Message, "2");
        Assert.AreEqual(1, fixture.Repository.GetColumns().Count);
    }

    [TestMethod]
    public async Task ColumnDeletionWithoutConfirmationDeletesImmediately()
    {
        using var fixture = new Fixture();
        fixture.Repository.AddColumn("A", "");
        fixture.Config.Confirmations.DeleteColumn = false;
        BoardViewModel board = fixture.CreateBoard();

        await board.Columns[0].DeleteCommand.ExecuteAsync(null);

        Assert.AreEqual(0, fixture.Dialogs.Shown.Count);
        Assert.AreEqual(0, board.Columns.Count);
    }

    [TestMethod]
    public async Task ConfirmedCardDeletionDeletesTheCard()
    {
        using var fixture = new Fixture();
        ColumnModel column = fixture.Repository.AddColumn("A", "");
        fixture.Repository.AddCard(column.Id, "gone", "", []);
        BoardViewModel board = fixture.CreateBoard();
        fixture.Dialogs.Script = ScriptedDialogService.AnswerConfirmations(true);

        await board.Columns[0].Cards[0].DeleteCommand.ExecuteAsync(null);

        Assert.AreEqual(0, board.Columns[0].CardCount);
        Assert.AreEqual(0, fixture.Repository.GetCards().Count);
    }

    [TestMethod]
    public async Task DuplicateColumnTitleIsReportedInsideTheDialog()
    {
        using var fixture = new Fixture();
        fixture.Repository.AddColumn("Todo", "");
        BoardViewModel board = fixture.CreateBoard();
        ColumnEditorDialogViewModel? editor = null;
        bool closed = false;
        fixture.Dialogs.Script = dialog =>
        {
            editor = (ColumnEditorDialogViewModel)dialog;
            editor.CloseRequested += (_, _) => closed = true;
            editor.ColumnTitle = "TODO";
            editor.ConfirmCommand.Execute(null);
            return Task.CompletedTask;
        };

        await board.AddColumnCommand.ExecuteAsync(null);

        Assert.IsNotNull(editor);
        Assert.IsFalse(closed, "The dialog stays open on a constraint violation.");
        Assert.IsTrue(editor.HasError);
        StringAssert.Contains(editor.ErrorMessage, "TODO");
        Assert.AreEqual(1, fixture.Repository.GetColumns().Count);
    }

    [TestMethod]
    public async Task NewCardIsWrittenWithItsTagsOnConfirmation()
    {
        using var fixture = new Fixture();
        ColumnModel column = fixture.Repository.AddColumn("A", "");
        TagModel bug = fixture.Repository.AddTag("bug", new RgbColor(255, 0, 0), "");
        BoardViewModel board = fixture.CreateBoard();
        fixture.Dialogs.Script = async dialog =>
        {
            switch (dialog)
            {
                case CardEditorDialogViewModel editor:
                    editor.CardTitle = "Fix it";
                    editor.Content = "Details";
                    await editor.AddTagCommand.ExecuteAsync(null);
                    editor.ConfirmCommand.Execute(null);
                    break;
                case TagPickerDialogViewModel picker:
                    picker.PickCommand.Execute(picker.FilteredTags.Single());
                    break;
            }
        };

        await board.Columns[0].AddCardCommand.ExecuteAsync(null);

        CardModel card = fixture.Repository.GetCards().Single();
        Assert.AreEqual("Fix it", card.Title);
        Assert.AreEqual("Details", card.Content);
        Assert.AreEqual(bug.Id, card.Tags.Single().Id);
        Assert.AreEqual(card.Id, board.Columns[0].Cards.Single().Card.Id);
    }

    [TestMethod]
    public async Task EditorThatWroteNothingLeavesTheColumnsAlone()
    {
        using var fixture = new Fixture();
        ColumnModel column = fixture.Repository.AddColumn("A", "");
        fixture.Repository.AddCard(column.Id, "t", "", []);
        BoardViewModel board = fixture.CreateBoard();
        ColumnViewModel shown = board.Columns.Single();
        int writes = 0;
        board.DataWritten += (_, _) => writes++;

        // Cancel a new card, confirm an unchanged existing card, confirm an empty new card.
        fixture.Dialogs.Script = dialog => dialog is CardEditorDialogViewModel editor
            ? editor.RequestCancelAsync()
            : Task.CompletedTask;
        await shown.AddCardCommand.ExecuteAsync(null);
        fixture.Dialogs.Script = dialog =>
        {
            (dialog as CardEditorDialogViewModel)?.ConfirmCommand.Execute(null);
            return Task.CompletedTask;
        };
        await shown.Cards.Single().EditCommand.ExecuteAsync(null);
        await shown.AddCardCommand.ExecuteAsync(null);

        Assert.AreSame(shown, board.Columns.Single());
        Assert.AreEqual(0, writes);
    }

    [TestMethod]
    public async Task TagCreatedInACancelledEditRefreshesTheBoard()
    {
        using var fixture = new Fixture();
        fixture.Repository.AddColumn("A", "");
        BoardViewModel board = fixture.CreateBoard();
        ColumnViewModel shown = board.Columns.Single();
        int writes = 0;
        board.DataWritten += (_, _) => writes++;
        fixture.Dialogs.Script = async dialog =>
        {
            switch (dialog)
            {
                case CardEditorDialogViewModel editor:
                    await editor.AddTagCommand.ExecuteAsync(null);
                    editor.Tags.Clear();
                    await editor.RequestCancelAsync();
                    break;
                case TagPickerDialogViewModel picker:
                    picker.NewName = "fresh";
                    picker.NewColor = "#123456";
                    picker.CreateCommand.Execute(null);
                    break;
            }
        };

        await shown.AddCardCommand.ExecuteAsync(null);

        Assert.AreNotSame(shown, board.Columns.Single());
        Assert.AreEqual(1, writes);
    }

    [TestMethod]
    public void UnchangedEditConfirmationWritesNothing()
    {
        using var fixture = new Fixture();
        ColumnModel column = fixture.Repository.AddColumn("A", "");
        CardModel card = fixture.Repository.AddCard(column.Id, "t", "c", []);
        using (var connection = SqliteTestHelper.OpenWorkspace(fixture.Directory.FullPath))
        {
            SqliteTestHelper.Exec(connection, "UPDATE cards SET updated_at = 100;");
        }
        card = fixture.Repository.GetCard(card.Id)!;

        var editor = CardEditorDialogViewModel.ForExistingCard(fixture.Repository, fixture.Dialogs, card, confirmDiscard: true);
        editor.CardTitle = "changed";
        editor.CardTitle = "t";
        Assert.IsFalse(editor.IsDirty);
        editor.ConfirmCommand.Execute(null);

        Assert.AreEqual(100, fixture.Repository.GetCard(card.Id)!.UpdatedAt);
    }

    [TestMethod]
    public void ConfirmingAnEmptyNewCardCreatesNothing()
    {
        using var fixture = new Fixture();
        ColumnModel column = fixture.Repository.AddColumn("A", "");
        var editor = CardEditorDialogViewModel.ForNewCard(fixture.Repository, fixture.Dialogs, column.Id, confirmDiscard: true);
        bool? closedWith = null;
        editor.CloseRequested += (_, result) => closedWith = result;
        editor.CardTitle = "draft";
        editor.CardTitle = "";

        editor.ConfirmCommand.Execute(null);

        Assert.IsTrue(closedWith);
        Assert.IsNull(editor.SavedCard);
        Assert.AreEqual(0, fixture.Repository.GetCards().Count);
    }

    [TestMethod]
    public async Task NewCardWithOnlyATagIsCreated()
    {
        using var fixture = new Fixture();
        ColumnModel column = fixture.Repository.AddColumn("A", "");
        TagModel bug = fixture.Repository.AddTag("bug", new RgbColor(255, 0, 0), "");
        var editor = CardEditorDialogViewModel.ForNewCard(fixture.Repository, fixture.Dialogs, column.Id, confirmDiscard: true);
        fixture.Dialogs.Script = dialog =>
        {
            if (dialog is TagPickerDialogViewModel picker)
            {
                picker.PickCommand.Execute(picker.FilteredTags.Single());
            }
            return Task.CompletedTask;
        };

        await editor.AddTagCommand.ExecuteAsync(null);
        editor.ConfirmCommand.Execute(null);

        CardModel card = fixture.Repository.GetCards().Single();
        Assert.AreEqual("", card.Title);
        Assert.AreEqual(bug.Id, card.Tags.Single().Id);
    }

    [TestMethod]
    public async Task DirtyCancelAsksAndStaysOpenWhenDeclined()
    {
        using var fixture = new Fixture();
        ColumnModel column = fixture.Repository.AddColumn("A", "");
        var editor = CardEditorDialogViewModel.ForNewCard(fixture.Repository, fixture.Dialogs, column.Id, confirmDiscard: true);
        bool? closedWith = null;
        editor.CloseRequested += (_, result) => closedWith = result;
        editor.Content = "draft";

        fixture.Dialogs.Script = ScriptedDialogService.AnswerConfirmations(false);
        await editor.RequestCancelAsync();
        Assert.IsNull(closedWith, "Declining the discard keeps the editor open.");

        fixture.Dialogs.Script = ScriptedDialogService.AnswerConfirmations(true);
        await editor.RequestCancelAsync();
        Assert.IsFalse(closedWith);
        Assert.AreEqual(0, fixture.Repository.GetCards().Count);
    }

    [TestMethod]
    public async Task CleanCancelClosesWithoutAsking()
    {
        using var fixture = new Fixture();
        ColumnModel column = fixture.Repository.AddColumn("A", "");
        var editor = CardEditorDialogViewModel.ForNewCard(fixture.Repository, fixture.Dialogs, column.Id, confirmDiscard: true);
        bool? closedWith = null;
        editor.CloseRequested += (_, result) => closedWith = result;

        await editor.RequestCancelAsync();

        Assert.IsFalse(closedWith);
        Assert.AreEqual(0, fixture.Dialogs.Shown.Count);
    }

    [TestMethod]
    public async Task TagCreatedInThePickerSurvivesACancelledCardEdit()
    {
        using var fixture = new Fixture();
        ColumnModel column = fixture.Repository.AddColumn("A", "");
        var editor = CardEditorDialogViewModel.ForNewCard(fixture.Repository, fixture.Dialogs, column.Id, confirmDiscard: false);
        fixture.Dialogs.Script = dialog =>
        {
            if (dialog is TagPickerDialogViewModel picker)
            {
                picker.NewName = "fresh";
                picker.NewColor = "#123456";
                picker.CreateCommand.Execute(null);
            }
            return Task.CompletedTask;
        };

        await editor.AddTagCommand.ExecuteAsync(null);
        Assert.AreEqual("fresh", editor.Tags.Single().Name);
        await editor.RequestCancelAsync();

        Assert.AreEqual("fresh", fixture.Repository.GetTags().Single().Name);
        Assert.AreEqual(0, fixture.Repository.GetCards().Count);
    }

    [TestMethod]
    public async Task FailedSearchQueryIsReportedAndKeepsThePreviousResultAndFilter()
    {
        using var fixture = new Fixture();
        ColumnModel column = fixture.Repository.AddColumn("A", "");
        fixture.Repository.AddCard(column.Id, "alpha", "", []);
        fixture.Repository.AddCard(column.Id, "beta", "", []);
        BoardViewModel board = fixture.CreateBoard();
        board.SearchText = "alpha";
        await board.SearchCommand.ExecuteAsync(null);
        CompiledSearch? alphaFilter = board.ActiveFilter;

        // Every read fails from now on.
        fixture.Repository.Dispose();
        board.SearchText = "beta";
        await board.SearchCommand.ExecuteAsync(null);

        var error = (MessageDialogViewModel)fixture.Dialogs.Shown.Single();
        Assert.AreEqual(Resources.Error_Read_Title, error.Title);
        Assert.AreSame(alphaFilter, board.ActiveFilter);
        CollectionAssert.AreEqual(new[] { "alpha" }, board.Columns.Single().Cards.Select(card => card.Title).ToArray());
    }

    [TestMethod]
    public async Task ReadFailuresOnBoardCommandsAreReportedInsteadOfThrown()
    {
        using var fixture = new Fixture();
        ColumnModel column = fixture.Repository.AddColumn("A", "");
        CardModel card = fixture.Repository.AddCard(column.Id, "a", "", []);
        BoardViewModel board = fixture.CreateBoard();
        var editor = CardEditorDialogViewModel.ForExistingCard(fixture.Repository, fixture.Dialogs, card, confirmDiscard: false);
        fixture.Repository.Dispose();

        board.SelectedSort = board.SortOptions.Single(option => (CardSortOption)option.Value == CardSortOption.Title);
        await board.ClearSearchCommand.ExecuteAsync(null);
        await board.Columns.Single().Cards.Single().EditCommand.ExecuteAsync(null);
        await board.Columns.Single().Cards.Single().MoveToCommand.ExecuteAsync(column.Id);
        await editor.AddTagCommand.ExecuteAsync(null);

        // Sort, clear, edit, the failed move with its refresh, and the tag picker.
        string read = Resources.Error_Read_Title;
        CollectionAssert.AreEqual(
            new[] { read, read, read, Resources.Error_Operation_Title, read, read },
            fixture.Dialogs.Shown.Select(dialog => ((MessageDialogViewModel)dialog).Title).ToArray());
    }

    [TestMethod]
    public void TagPickerReportsInvalidColorAndDuplicateNameWithoutClosing()
    {
        using var fixture = new Fixture();
        fixture.Repository.AddTag("bug", new RgbColor(1, 2, 3), "");
        var picker = new TagPickerDialogViewModel(fixture.Repository, []);
        bool closed = false;
        picker.CloseRequested += (_, _) => closed = true;

        picker.NewName = "new";
        picker.NewColor = "red";
        picker.CreateCommand.Execute(null);
        Assert.AreEqual(YKanBan.Resources.Error_Tag_InvalidColor, picker.ErrorMessage);

        picker.NewName = "BUG";
        picker.NewColor = "#ff0000";
        picker.CreateCommand.Execute(null);
        StringAssert.Contains(picker.ErrorMessage, "BUG");

        Assert.IsFalse(closed);
        Assert.AreEqual(1, fixture.Repository.GetTags().Count);
    }

    [TestMethod]
    public void TagBadgeShowsNoTooltipForAnEmptyDescription()
    {
        Assert.IsNull(new TagBadgeViewModel(new TagModel(1, "bug", new RgbColor(1, 2, 3), "")).ToolTip);
        Assert.AreEqual("why", new TagBadgeViewModel(new TagModel(1, "bug", new RgbColor(1, 2, 3), "why")).ToolTip);
    }

    [TestMethod]
    public void TagPickerHidesAssignedTagsAndFiltersCaseInsensitively()
    {
        using var fixture = new Fixture();
        TagModel bug = fixture.Repository.AddTag("Bug", new RgbColor(1, 2, 3), "");
        fixture.Repository.AddTag("UI", new RgbColor(1, 2, 3), "");
        fixture.Repository.AddTag("Build", new RgbColor(1, 2, 3), "");
        var picker = new TagPickerDialogViewModel(fixture.Repository, [bug.Id]);

        picker.FilterText = "bu";
        CollectionAssert.AreEqual(new[] { "Build" }, picker.FilteredTags.Select(tag => tag.Name).ToArray());

        picker.FilterText = "zzz";
        Assert.IsTrue(picker.NoMatches);
    }

    [TestMethod]
    public void ColumnWidthDialogCommitsPresetsAndClampsCustomPixels()
    {
        var settings = new ColumnWidthSettings();
        var dialog = new ColumnWidthDialogViewModel(settings) { IsStandard = false, IsCustom = true, CustomPixels = 5000 };
        dialog.ConfirmCommand.Execute(null);
        Assert.AreEqual(ColumnWidthPreset.Custom, settings.Preset);
        Assert.AreEqual(AppConfig.MaxColumnPixels, settings.CustomPixels);
        Assert.AreEqual(AppConfig.MaxColumnPixels, ColumnWidths.Resolve(settings));

        dialog = new ColumnWidthDialogViewModel(settings) { IsCustom = false, IsWide = true };
        dialog.ConfirmCommand.Execute(null);
        Assert.AreEqual(ColumnWidthPreset.Wide, settings.Preset);
        Assert.AreEqual(ColumnWidths.Wide, ColumnWidths.Resolve(settings));
    }

    [TestMethod]
    public void CardHeaderAndExcerptFollowTheDisplayRules()
    {
        var untitled = new CardModel(61, 1, "", "", 0, 0, []);
        var titled = untitled with { Title = "Foo bar" };
        Assert.AreEqual("#61", CardViewModel.FormatHeader(untitled));
        Assert.AreEqual("#61 Foo bar", CardViewModel.FormatHeader(titled));

        Assert.AreEqual("short", CardViewModel.MakeExcerpt("  short \n"));
        string longContent = string.Concat(Enumerable.Repeat("😀", CardViewModel.ExcerptLength + 5));
        string excerpt = CardViewModel.MakeExcerpt(longContent);
        Assert.IsTrue(excerpt.EndsWith('…'));
        Assert.AreEqual((CardViewModel.ExcerptLength * 2) + 1, excerpt.Length, "Surrogate pairs are kept whole.");
    }
}
