using System.Globalization;
using YKanBan.Export;
using YKanBan.Models;

namespace YKanBan.Tests.Export;

/// <summary>
/// Tests for the Markdown exporter. Mutates the global Resources.Culture,
/// therefore marked DoNotParallelize. Asserts real localized output for
/// both supported languages.
/// </summary>
[TestClass]
[DoNotParallelize]
public class MarkdownExporterTests
{
    private static readonly DateTimeOffset ExportedAt = new(2026, 9, 30, 14, 25, 37, TimeSpan.Zero);

    /// <summary>A local wall-clock moment, matching the exporter's conversion path.</summary>
    private static DateTimeOffset Local(int year, int month, int day, int hour, int minute) =>
        new DateTimeOffset(new DateTime(year, month, day, hour, minute, 0)).ToLocalTime();

    /// <summary>Unix seconds for a local wall-clock time.</summary>
    private static long UnixTimeOf(int year, int month, int day, int hour, int minute) =>
        Local(year, month, day, hour, minute).ToUnixTimeSeconds();

    /// <summary>Builds the board fixture: one working column with two cards, one empty column, two tags.</summary>
    private static (IReadOnlyList<ColumnModel> Columns, IReadOnlyList<CardModel> Cards, IReadOnlyList<(TagModel, long)> Tags) CreateBoard()
    {
        long created = UnixTimeOf(2026, 9, 1, 10, 0);
        long updated = UnixTimeOf(2026, 9, 28, 18, 30);

        var columns = new List<ColumnModel>
        {
            new(1, "In Progress", "The work column", 100, 100),
            new(2, "Empty", string.Empty, 100, 100),
        };
        var bug = new TagModel(1, "bug", RgbColor.Parse("#ff0000"), string.Empty);
        var ui = new TagModel(2, "ui", RgbColor.Parse("#00ff00"), "Interface work");
        var cards = new List<CardModel>
        {
            new(61, 1, "Fix login", "Body text", created, updated, [bug, ui]),
            new(62, 1, string.Empty, string.Empty, created, updated, []),
        };
        var tags = new List<(TagModel, long)> { (bug, 1), (ui, 1) };
        return (columns, cards, tags);
    }

    private static string Export(CultureInfo culture)
    {
        CultureInfo? original = Resources.Culture;
        try
        {
            Resources.Culture = culture;
            (IReadOnlyList<ColumnModel> columns, IReadOnlyList<CardModel> cards, IReadOnlyList<(TagModel, long)> tags) = CreateBoard();
            return MarkdownExporter.Export("MyRepo", columns, cards, tags, ExportedAt);
        }
        finally
        {
            Resources.Culture = original;
        }
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n");

    [TestMethod]
    public void EnglishOutputMatchesTemplate()
    {
        string created = Local(2026, 9, 1, 10, 0).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        string updated = Local(2026, 9, 28, 18, 30).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

        string expected = Normalize($"""
            # MyRepo
            > Exported at: 2026-09-30 14:25:37

            ## Column: In Progress (2)
            The work column

            ### #61 Fix login
            Body text
            - Tags: `bug` `ui`
            - Created: {created}  Modified: {updated}

            ### #62
            - Created: {created}  Modified: {updated}

            ## Column: Empty (0)

            ## Tags
            | Name | Color | Description | Usage |
            |---|---|---|---|
            | bug | #ff0000 |  | 1 |
            | ui | #00ff00 | Interface work | 1 |
            """);

        Assert.AreEqual(expected + "\n", Normalize(Export(new CultureInfo("en"))));
    }

    [TestMethod]
    public void ChineseOutputMatchesTemplate()
    {
        var chinese = new CultureInfo("zh-Hans");
        string created = Local(2026, 9, 1, 10, 0).ToString("yyyy年M月d日 HH:mm", chinese);
        string updated = Local(2026, 9, 28, 18, 30).ToString("yyyy年M月d日 HH:mm", chinese);

        string expected = Normalize($"""
            # MyRepo
            > 导出时间：2026年9月30日 14:25

            ## 列：In Progress（2）
            The work column

            ### #61 Fix login
            Body text
            - 标签：`bug` `ui`
            - 创建：{created}　修改：{updated}

            ### #62
            - 创建：{created}　修改：{updated}

            ## 列：Empty（0）

            ## 标签总表
            | 名称 | 颜色 | 描述 | 使用数 |
            |---|---|---|---|
            | bug | #ff0000 |  | 1 |
            | ui | #00ff00 | Interface work | 1 |
            """);

        Assert.AreEqual(expected + "\n", Normalize(Export(new CultureInfo("zh-Hans"))));
    }

    [TestMethod]
    public void FileNameFollowsLanguage()
    {
        CultureInfo? original = Resources.Culture;
        try
        {
            Resources.Culture = new CultureInfo("en");
            Assert.AreEqual("MyRepo-export-20260930-142537.md", MarkdownExporter.BuildFileName("MyRepo", ExportedAt));

            Resources.Culture = new CultureInfo("zh-Hans");
            Assert.AreEqual("MyRepo-导出-20260930-142537.md", MarkdownExporter.BuildFileName("MyRepo", ExportedAt));
        }
        finally
        {
            Resources.Culture = original;
        }
    }

    [TestMethod]
    public void TagTableCellsEscapePipesAndLineBreaks()
    {
        CultureInfo? original = Resources.Culture;
        try
        {
            Resources.Culture = new CultureInfo("en");
            var tag = new TagModel(1, "a|b", RgbColor.Parse("#112233"), "first\nsecond");
            string markdown = MarkdownExporter.Export("W",
                [new ColumnModel(1, "C", string.Empty, 1, 1)],
                [],
                [(tag, 0L)],
                ExportedAt);

            StringAssert.Contains(markdown, "| a\\|b | #112233 | first second | 0 |");
        }
        finally
        {
            Resources.Culture = original;
        }
    }
}
