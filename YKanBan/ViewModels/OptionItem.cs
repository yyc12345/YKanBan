namespace YKanBan.ViewModels;

/// <summary>A selectable option item for combo boxes: a value plus its localized label.</summary>
/// <param name="Value">The backing value (enum value or language code).</param>
/// <param name="Label">The localized display label.</param>
public sealed record OptionItem(object Value, string Label);
