namespace YKanBan.Models;

/// <summary>A board column as stored in the workspace database.</summary>
/// <param name="Id">Row id.</param>
/// <param name="Title">Non-empty unique title.</param>
/// <param name="Description">Free-form description, possibly empty.</param>
/// <param name="CreatedAt">Unix seconds of creation.</param>
/// <param name="UpdatedAt">Unix seconds of the last title/description change.</param>
public sealed record ColumnModel(long Id, string Title, string Description, long CreatedAt, long UpdatedAt);
