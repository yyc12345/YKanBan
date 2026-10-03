using System.Globalization;
using YKanBan.Storage;

namespace YKanBan.ViewModels.Dialogs;

/// <summary>
/// Maps a failed dialog write to the localized message shown inside the dialog
/// that attempted it (the dialog stays open and keeps the user's input). The
/// typed schema-constraint exceptions get dedicated messages; anything else
/// gets a generic one carrying the untranslated exception message.
/// </summary>
public static class WriteErrorMessages
{
    /// <summary>
    /// Returns the localized message for a failed write.
    /// </summary>
    /// <param name="exception">The exception thrown by the write.</param>
    /// <returns>The localized message.</returns>
    public static string Describe(Exception exception)
    {
        CultureInfo? culture = Resources.Culture;
        return exception switch
        {
            NameRequiredException { Entity: NamedEntity.Column } => Resources.Error_Column_TitleRequired,
            NameRequiredException { Entity: NamedEntity.Tag } => Resources.Error_Tag_NameRequired,
            DuplicateNameException { Entity: NamedEntity.Column } duplicate =>
                string.Format(culture, Resources.Error_Column_Duplicate, duplicate.Name),
            DuplicateNameException { Entity: NamedEntity.Tag } duplicate =>
                string.Format(culture, Resources.Error_Tag_Duplicate, duplicate.Name),
            _ => string.Format(culture, Resources.Error_SaveFailed, exception.Message),
        };
    }
}
