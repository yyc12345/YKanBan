namespace YKanBan.ViewModels;

/// <summary>
/// A selectable option for combo boxes: a backing value plus its localized
/// display label.
/// </summary>
/// <param name="Value">The backing value (an enum value or a language code).</param>
/// <param name="Label">The localized display label.</param>
public sealed record OptionItem(object Value, string Label);
