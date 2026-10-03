using System.Globalization;
using System.Text;
using YKanBan.Export;
using YKanBan.Models;

namespace YKanBan.Tests.Export;

/// <summary>
/// Tests for the Markdown exporter. Mutates the global
/// <see cref="Resources.Culture"/>, therefore marked do-not-parallelize.
/// Expected documents are assembled from the same ResX template keys as the
/// exporter (plus fixed fixture values), so no localized text is hard-coded
/// here.
/// </summary>
[TestClass]
[DoNotParallelize]
public class MarkdownExporterTests
{
    private static readonly DateTimeOffset ExportedAt = new(2026, 9, 30, 14, 25, 37, TimeSpan.Zero);

    /// <summary>
    /// Builds a local wall-clock moment, matching the exporter's conversion path.
    /// </summary>
    /// <param name="year">The year.</param>
    /// <param name="month">The month.</param>
    /// <param name="day">The day.</param>
    /// <param name="hour">The hour.</param>
    /// <param name="minute">The minute.</param>
    /// <returns>The local time.</returns>
    private static DateTimeOffset Local(int year, int month, int day, int hour, int minute) =>
        new DateTimeOffset(new DateTime(year, month, day, hour, minute, 0)).ToLocalTime();

    /// <summary>
    /// Converts a local wall-clock time to Unix seconds.
    /// </summary>
    /// <param name="year">The year.</param>
    /// <param name="month">The month.</param>
    /// <param name="day">The day.</param>
    /// <param name="hour">The hour.</param>
    /// <param name="minute">The minute.</param>
    /// <returns>The Unix timestamp.</returns>
    private static long UnixTimeOf(int year, int month, int day, int hour, int minute) =>
        Local(year, month, day, hour, minute).ToUnixTimeSeconds();

    /// <summary>
    /// Builds the board fixture: one working column with two cards, one empty column and two tags.
    /// </summary>
    /// <returns>The columns, cards and tag/usage tuples.</returns>
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

    /// <summary>
    /// Renders the fixture board with the exporter under the current culture.
    /// </summary>
    /// <returns>The produced Markdown document.</returns>
    private static string ExportBoard()
    {
        (IReadOnlyList<ColumnModel> columns, IReadOnlyList<CardModel> cards, IReadOnlyList<(TagModel, long)> tags) = CreateBoard();
        return MarkdownExporter.Export("MyRepo", columns, cards, tags, ExportedAt);
    }

    /// <summary>
    /// Assembles the expected document from the ResX template keys under the current culture.
    /// </summary>
    /// <param name="culture">The UI culture.</param>
    /// <returns>The expected Markdown document.</returns>
    private static string BuildExpected(CultureInfo culture)
    {
        string created = Local(2026, 9, 1, 10, 0).ToString(Resources.Export_TimePattern, culture);
        string updated = Local(2026, 9, 28, 18, 30).ToString(Resources.Export_TimePattern, culture);

        var builder = new StringBuilder();

        // Header: workspace name and the export timestamp.
        builder.AppendLine(string.Format(culture, Resources.Export_WorkspaceHeading, "MyRepo"));
        builder.AppendLine(string.Format(
            culture, Resources.Export_ExportedAtLine, ExportedAt.ToString(Resources.Export_TimePattern, culture)));
        builder.AppendLine();

        // First column with its two cards.
        builder.AppendLine(string.Format(culture, Resources.Export_ColumnHeading, "In Progress", 2));
        builder.AppendLine("The work column");
        builder.AppendLine();
        builder.AppendLine("### #61 Fix login");
        builder.AppendLine("Body text");
        builder.AppendLine(string.Format(culture, Resources.Export_TagsLine, "`bug` `ui`"));
        builder.AppendLine(string.Format(culture, Resources.Export_CreatedModifiedLine, created, updated));
        builder.AppendLine();
        builder.AppendLine("### #62");
        builder.AppendLine(string.Format(culture, Resources.Export_CreatedModifiedLine, created, updated));
        builder.AppendLine();

        // Empty column is kept.
        builder.AppendLine(string.Format(culture, Resources.Export_ColumnHeading, "Empty", 0));
        builder.AppendLine();

        // Tag summary table.
        builder.AppendLine(Resources.Export_TagsSummaryHeading);
        builder.AppendLine(Resources.Export_TagsTableHeader);
        builder.AppendLine("|---|---|---|---|");
        builder.AppendLine("| bug | #ff0000 |  | 1 |");
        builder.AppendLine("| ui | #00ff00 | Interface work | 1 |");

        return builder.ToString();
    }

    /// <summary>
    /// Normalizes the expected document, built with the platform newline, to the exporter's LF.
    /// </summary>
    /// <param name="text">The text to normalize.</param>
    /// <returns>The text with normalized line endings.</returns>
    private static string Normalize(string text) => text.Replace("\r\n", "\n");

    /// <summary>
    /// Asserts that the exporter output matches the resource-built expected document.
    /// </summary>
    /// <param name="culture">The UI culture to test.</param>
    private static void AssertOutputMatches(CultureInfo culture)
    {
        CultureInfo? original = Resources.Culture;
        try
        {
            Resources.Culture = culture;
            Assert.AreEqual(Normalize(BuildExpected(culture)), ExportBoard());
        }
        finally
        {
            Resources.Culture = original;
        }
    }

    [TestMethod]
    public void EnglishOutputMatchesTemplate()
    {
        AssertOutputMatches(new CultureInfo("en"));
    }

    [TestMethod]
    public void ChineseOutputMatchesTemplate()
    {
        AssertOutputMatches(new CultureInfo("zh-Hans"));
    }

    [TestMethod]
    public void FileNameFollowsLanguage()
    {
        CultureInfo? original = Resources.Culture;
        try
        {
            Resources.Culture = new CultureInfo("en");
            Assert.AreEqual(
                string.Format(Resources.Culture, Resources.Export_FileNamePattern, "MyRepo", ExportedAt),
                MarkdownExporter.BuildFileName("MyRepo", ExportedAt));

            Resources.Culture = new CultureInfo("zh-Hans");
            Assert.AreEqual(
                string.Format(Resources.Culture, Resources.Export_FileNamePattern, "MyRepo", ExportedAt),
                MarkdownExporter.BuildFileName("MyRepo", ExportedAt));
        }
        finally
        {
            Resources.Culture = original;
        }
    }

    [TestMethod]
    [DataRow("D:\\", "D")]
    [DataRow("a<b>c|d?e*f\"g", "abcdefg")]
    [DataRow("/", null)]
    public void FileNameDropsCharactersThatAreInvalidInFileNames(string workspaceName, string? expectedName)
    {
        string fileName = MarkdownExporter.BuildFileName(workspaceName, ExportedAt);

        CultureInfo culture = Resources.Culture ?? CultureInfo.CurrentUICulture;
        Assert.AreEqual(
            string.Format(culture, Resources.Export_FileNamePattern, expectedName ?? Resources.App_Name, ExportedAt),
            fileName);
        Assert.AreEqual(-1, fileName.IndexOfAny(Path.GetInvalidFileNameChars()));
    }

    [TestMethod]
    public void TagTableCellsEscapePipesAndLineBreaks()
    {
        CultureInfo? original = Resources.Culture;
        try
        {
            Resources.Culture = new CultureInfo("en");
            var tag = new TagModel(1, "a|b", RgbColor.Parse("#112233"), "first\nsecond");
            string markdown = MarkdownExporter.Export(
                "W",
                [new ColumnModel(1, "C", string.Empty, 1, 1)],
                [],
                [(tag, 0L)],
                ExportedAt);

            StringAssert.Contains(markdown, "| a\\|b | #112233 | first<br>second | 0 |");
        }
        finally
        {
            Resources.Culture = original;
        }
    }

    [TestMethod]
    public void TagTableCellsEscapeBackslashesSoAPipeAfterOneStaysEscaped()
    {
        CultureInfo? original = Resources.Culture;
        try
        {
            Resources.Culture = new CultureInfo("en");
            var tag = new TagModel(1, @"a\|b", RgbColor.Parse("#112233"), @"C:\temp\");
            string markdown = MarkdownExporter.Export(
                "W",
                [new ColumnModel(1, "C", string.Empty, 1, 1)],
                [],
                [(tag, 0L)],
                ExportedAt);

            StringAssert.Contains(markdown, @"| a\\\|b | #112233 | C:\\temp\\ | 0 |");
        }
        finally
        {
            Resources.Culture = original;
        }
    }

    [TestMethod]
    [DataRow("plain", "`plain`")]
    [DataRow("a`b", "``a`b``")]
    [DataRow("a``b`c", "```a``b`c```")]
    [DataRow("`x", "`` `x ``")]
    [DataRow("x`", "`` x` ``")]
    [DataRow("`", "`` ` ``")]
    public void TagNamesUseLongEnoughBacktickFence(string name, string expected)
    {
        CultureInfo? original = Resources.Culture;
        try
        {
            Resources.Culture = new CultureInfo("en");
            var tag = new TagModel(1, name, RgbColor.Parse("#112233"), string.Empty);
            string markdown = MarkdownExporter.Export(
                "W",
                [new ColumnModel(1, "C", string.Empty, 1, 1)],
                [new CardModel(1, 1, "T", string.Empty, 1, 1, [tag])],
                [(tag, 1L)],
                ExportedAt);

            StringAssert.Contains(markdown, string.Format(Resources.Culture, Resources.Export_TagsLine, expected) + "\n");
        }
        finally
        {
            Resources.Culture = original;
        }
    }

    [TestMethod]
    public void CardBodyAndColumnDescriptionAreVerbatim()
    {
        CultureInfo? original = Resources.Culture;
        try
        {
            Resources.Culture = new CultureInfo("en");
            string markdown = MarkdownExporter.Export(
                "W",
                [new ColumnModel(1, "C", "line one\n# not a heading | pipe", 1, 1)],
                [new CardModel(1, 1, "T", "body\n\n## raw | `md`", 1, 1, [])],
                [],
                ExportedAt);

            StringAssert.Contains(markdown, "\nline one\n# not a heading | pipe\n");
            StringAssert.Contains(markdown, "\nbody\n\n## raw | `md`\n");
        }
        finally
        {
            Resources.Culture = original;
        }
    }

    [TestMethod]
    public void LineEndingsAreLfOnEveryPlatform()
    {
        CultureInfo? original = Resources.Culture;
        try
        {
            Resources.Culture = new CultureInfo("en");
            string markdown = MarkdownExporter.Export(
                "W",
                [new ColumnModel(1, "C", "one\r\ntwo\rthree", 1, 1)],
                [new CardModel(1, 1, "T", "a\r\nb", 1, 1, [])],
                [],
                ExportedAt);

            Assert.IsFalse(markdown.Contains('\r'));
            StringAssert.Contains(markdown, "\none\ntwo\nthree\n");
            StringAssert.Contains(markdown, "\na\nb\n");
        }
        finally
        {
            Resources.Culture = original;
        }
    }

    [TestMethod]
    public void ColumnTitleIsCollapsedToOneLine()
    {
        CultureInfo? original = Resources.Culture;
        try
        {
            Resources.Culture = new CultureInfo("en");
            string markdown = MarkdownExporter.Export(
                "W",
                [new ColumnModel(1, "To\r\nDo\nNow", string.Empty, 1, 1)],
                [],
                [],
                ExportedAt);

            string heading = string.Format(Resources.Culture, Resources.Export_ColumnHeading, "To Do Now", 0);
            StringAssert.Contains(markdown, "\n" + heading + "\n");
        }
        finally
        {
            Resources.Culture = original;
        }
    }

    [TestMethod]
    public void ColumnsAndCardsAreExportedInIdOrder()
    {
        CultureInfo? original = Resources.Culture;
        try
        {
            Resources.Culture = new CultureInfo("en");
            string markdown = MarkdownExporter.Export(
                "W",
                [new ColumnModel(2, "Second", string.Empty, 1, 1), new ColumnModel(1, "First", string.Empty, 1, 1)],
                [
                    new CardModel(9, 1, "Nine", string.Empty, 1, 1, []),
                    new CardModel(3, 2, "Three", string.Empty, 1, 1, []),
                    new CardModel(4, 1, "Four", string.Empty, 1, 1, []),
                ],
                [],
                ExportedAt);

            int first = markdown.IndexOf(string.Format(Resources.Culture, Resources.Export_ColumnHeading, "First", 2), StringComparison.Ordinal);
            int second = markdown.IndexOf(string.Format(Resources.Culture, Resources.Export_ColumnHeading, "Second", 1), StringComparison.Ordinal);
            int four = markdown.IndexOf("### #4 Four", StringComparison.Ordinal);
            int nine = markdown.IndexOf("### #9 Nine", StringComparison.Ordinal);
            int three = markdown.IndexOf("### #3 Three", StringComparison.Ordinal);
            Assert.IsTrue(first >= 0 && first < four && four < nine && nine < second && second < three, markdown);
        }
        finally
        {
            Resources.Culture = original;
        }
    }

    [TestMethod]
    [DataRow("en", "2026-09-30 14:25:37", "2026-09-01 10:00:05")]
    [DataRow("zh-Hans", "2026年9月30日 14:25", "2026年9月1日 10:00")]
    public void TimestampsUseThePlannedPerLanguageFormat(string language, string exportedAt, string cardTime)
    {
        CultureInfo? original = Resources.Culture;
        try
        {
            Resources.Culture = new CultureInfo(language);
            long cardSeconds = new DateTimeOffset(new DateTime(2026, 9, 1, 10, 0, 5)).ToUnixTimeSeconds();
            var exportMoment = new DateTimeOffset(new DateTime(2026, 9, 30, 14, 25, 37));
            string markdown = MarkdownExporter.Export(
                "W",
                [new ColumnModel(1, "C", string.Empty, 1, 1)],
                [new CardModel(1, 1, "T", string.Empty, cardSeconds, cardSeconds, [])],
                [],
                exportMoment);

            StringAssert.Contains(markdown, string.Format(Resources.Culture, Resources.Export_ExportedAtLine, exportedAt));
            StringAssert.Contains(markdown, string.Format(Resources.Culture, Resources.Export_CreatedModifiedLine, cardTime, cardTime));
        }
        finally
        {
            Resources.Culture = original;
        }
    }
}
