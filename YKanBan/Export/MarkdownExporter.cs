using System.Globalization;
using System.Text;
using YKanBan.Models;

namespace YKanBan.Export;

/// <summary>
/// Generates the human-readable Markdown snapshot of a whole board. All
/// template text and time formats come from the ResX resources in the
/// current UI language; the export timestamp uses the per-language pattern,
/// card timestamps use the shared minute-precision pattern. The output is
/// meant for reading only — there is no re-import.
/// </summary>
public static class MarkdownExporter
{
    /// <summary>Renders the whole board to Markdown.</summary>
    /// <param name="workspaceName">Workspace display name (the folder name).</param>
    /// <param name="columns">All columns, in id order; empty columns are kept.</param>
    /// <param name="cards">All cards; grouped by column, id order within a column.</param>
    /// <param name="tags">All tags with usage counts, in id order.</param>
    /// <param name="exportedAt">Export moment in local time.</param>
    public static string Export(
        string workspaceName,
        IReadOnlyList<ColumnModel> columns,
        IReadOnlyList<CardModel> cards,
        IReadOnlyList<(TagModel Tag, long UsageCount)> tags,
        DateTimeOffset exportedAt)
    {
        CultureInfo culture = Resources.Culture ?? CultureInfo.CurrentUICulture;

        var builder = new StringBuilder();

        // Header: workspace name and the export timestamp in the UI language's format.
        builder.AppendLine(string.Format(culture, Resources.Export_WorkspaceHeading, workspaceName));
        builder.AppendLine(string.Format(
            culture, Resources.Export_ExportedAtLine, exportedAt.ToString(Resources.Export_TimePattern, culture)));
        builder.AppendLine();

        // Board body: every column in id order, empty ones included.
        foreach (ColumnModel column in columns)
        {
            IReadOnlyList<CardModel> cardsInColumn = cards.Where(card => card.ColumnId == column.Id).ToArray();
            builder.AppendLine(string.Format(culture, Resources.Export_ColumnHeading, column.Title, cardsInColumn.Count));
            if (column.Description.Length > 0)
            {
                builder.AppendLine(SingleLine(column.Description));
            }
            builder.AppendLine();

            foreach (CardModel card in cardsInColumn)
            {
                AppendCard(builder, card, culture);
            }
        }

        AppendTagTable(builder, tags, culture);
        return builder.ToString();
    }

    /// <summary>Builds the default export file name in the UI language, e.g. "MyRepo-export-20260930-142537.md".</summary>
    public static string BuildFileName(string workspaceName, DateTimeOffset exportedAt)
    {
        CultureInfo culture = Resources.Culture ?? CultureInfo.CurrentUICulture;
        return string.Format(culture, Resources.Export_FileNamePattern, workspaceName, exportedAt);
    }

    /// <summary>Appends one card block: heading, body, tags line, timestamps line.</summary>
    private static void AppendCard(StringBuilder builder, CardModel card, CultureInfo culture)
    {
        // Heading: "#id title"; an untitled card shows only the id.
        builder.AppendLine(card.Title.Length == 0
            ? $"### #{card.Id}"
            : $"### #{card.Id} {SingleLine(card.Title)}");

        if (card.Content.Length > 0)
        {
            builder.AppendLine(card.Content);
        }

        if (card.Tags.Count > 0)
        {
            string names = string.Join(" ", card.Tags.Select(tag => $"`{tag.Name}`"));
            builder.AppendLine(string.Format(culture, Resources.Export_TagsLine, names));
        }

        string created = ToLocalTime(card.CreatedAt).ToString(Resources.Export_CardTimePattern, culture);
        string updated = ToLocalTime(card.UpdatedAt).ToString(Resources.Export_CardTimePattern, culture);
        builder.AppendLine(string.Format(culture, Resources.Export_CreatedModifiedLine, created, updated));
        builder.AppendLine();
    }

    /// <summary>Appends the tag summary table with usage counts; tags with zero usage are included.</summary>
    private static void AppendTagTable(
        StringBuilder builder,
        IReadOnlyList<(TagModel Tag, long UsageCount)> tags,
        CultureInfo culture)
    {
        builder.AppendLine(Resources.Export_TagsSummaryHeading);
        builder.AppendLine(Resources.Export_TagsTableHeader);
        builder.AppendLine("|---|---|---|---|");
        foreach ((TagModel tag, long usage) in tags)
        {
            // '|' and line breaks would break the table row; neutralize them.
            builder.AppendLine($"| {Cell(tag.Name)} | {tag.Color} | {Cell(tag.Description)} | {usage} |");
        }
    }

    /// <summary>Formats a table cell: single line with escaped pipe characters.</summary>
    private static string Cell(string text) => SingleLine(text).Replace("|", "\\|");

    /// <summary>Collapses line breaks so the text stays on one Markdown line.</summary>
    private static string SingleLine(string text) => text.Replace("\r", string.Empty).Replace("\n", " ");

    /// <summary>Converts stored Unix seconds to a local-time DateTimeOffset.</summary>
    private static DateTimeOffset ToLocalTime(long unixSeconds) =>
        DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime();
}
