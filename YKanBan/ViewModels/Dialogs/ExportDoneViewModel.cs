using System.Globalization;

namespace YKanBan.ViewModels.Dialogs;

/// <summary>
/// The confirmation dialog shown after a successful Markdown export, naming the
/// written file.
/// </summary>
public sealed class ExportDoneViewModel : ViewModelBase {
    /// <summary>
    /// Initializes the dialog with the exported file path.
    /// </summary>
    /// <param name="filePath">The path the export was written to.</param>
    public ExportDoneViewModel(string filePath) {
        Message = string.Format(
            Resources.Culture ?? CultureInfo.CurrentCulture, Resources.Export_Done_Message, filePath);
    }

    /// <summary>
    /// Gets the localized confirmation message.
    /// </summary>
    public string Message { get; }
}
