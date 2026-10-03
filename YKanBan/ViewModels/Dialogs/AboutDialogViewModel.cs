using System.Globalization;

namespace YKanBan.ViewModels.Dialogs;

/// <summary>
/// The about dialog: application name, version, repository link and license.
/// </summary>
public sealed class AboutDialogViewModel : ViewModelBase
{
    /// <summary>
    /// Initializes the view model, reading the version from the assembly.
    /// </summary>
    public AboutDialogViewModel()
    {
        string version = typeof(AboutDialogViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        Version = string.Format(
            Resources.Culture ?? CultureInfo.CurrentCulture, Resources.About_Version, version);
    }

    /// <summary>
    /// Gets the application name.
    /// </summary>
    public string AppName => Resources.App_Name;

    /// <summary>
    /// Gets the localized version line.
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// Gets the repository URL.
    /// </summary>
    public string RepositoryUrl => "https://code.blumia.cn/yyc12345/YKanBan";

    /// <summary>
    /// Gets the license name.
    /// </summary>
    public string License => Resources.About_LicenseValue;
}
