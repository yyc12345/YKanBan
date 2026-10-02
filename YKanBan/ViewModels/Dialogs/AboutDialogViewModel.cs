using System.Reflection;

namespace YKanBan.ViewModels.Dialogs;

/// <summary>The about dialog data: application identity and version.</summary>
public sealed class AboutDialogViewModel : ViewModelBase
{
    /// <summary>Display name of the application.</summary>
    public string ApplicationName => Resources.App_Name;

    /// <summary>Localized "Version x.y.z" text.</summary>
    public string VersionText => string.Format(
        System.Globalization.CultureInfo.CurrentCulture,
        Resources.About_Version,
        AssemblyVersion);

    /// <summary>Repository URL (from resources).</summary>
    public string RepositoryUrl => Resources.About_RepositoryUrl;

    /// <summary>License name (from resources).</summary>
    public string LicenseValue => Resources.About_LicenseValue;

    /// <summary>The assembly version, formatted as major.minor.patch.</summary>
    public static string AssemblyVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
}
