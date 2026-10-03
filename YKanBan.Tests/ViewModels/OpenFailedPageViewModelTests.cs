using System.Globalization;
using Microsoft.Data.Sqlite;
using YKanBan.Storage;
using YKanBan.ViewModels.ErrorPages;

namespace YKanBan.Tests.ViewModels;

/// <summary>
/// Tests for the open-failed page's exception-type → localized message mapping
/// and its English details. Mutates the global <see cref="Resources.Culture"/>,
/// therefore marked do-not-parallelize.
/// </summary>
[TestClass]
[DoNotParallelize]
public class OpenFailedPageViewModelTests
{
    private const string Folder = "/work/repo";

    private static void WithCulture(string language, Action action)
    {
        CultureInfo? original = Resources.Culture;
        try
        {
            Resources.Culture = new CultureInfo(language);
            action();
        }
        finally
        {
            Resources.Culture = original;
        }
    }

    private static string Expected(string pattern, params object[] arguments) =>
        string.Format(Resources.Culture, pattern, [Folder, .. arguments]);

    [TestMethod]
    [DataRow("en")]
    [DataRow("zh-Hans")]
    public void EachRecognizedExceptionTypeHasItsOwnMessage(string language)
    {
        WithCulture(language, () =>
        {
            Assert.AreEqual(
                Expected(Resources.Error_OpenFailed_SchemaTooNew, 3L, 1),
                OpenFailedPageViewModel.BuildMessage(Folder, new SchemaVersionException(3, 1)));
            Assert.AreEqual(
                Expected(Resources.Error_OpenFailed_Database),
                OpenFailedPageViewModel.BuildMessage(Folder, new SqliteException("file is not a database", 26)));
            Assert.AreEqual(
                Expected(Resources.Error_OpenFailed_AccessDenied),
                OpenFailedPageViewModel.BuildMessage(Folder, new UnauthorizedAccessException()));
            Assert.AreEqual(
                Expected(Resources.Error_OpenFailed_Io),
                OpenFailedPageViewModel.BuildMessage(Folder, new IOException()));
            Assert.AreEqual(
                Expected(Resources.Error_OpenFailed_Io),
                OpenFailedPageViewModel.BuildMessage(Folder, new DirectoryNotFoundException()));
        });
    }

    [TestMethod]
    public void UnrecognizedExceptionGetsTheGenericMessage()
    {
        WithCulture("en", () =>
        {
            Assert.AreEqual(
                Expected(Resources.Error_OpenFailed_Generic),
                OpenFailedPageViewModel.BuildMessage(Folder, new InvalidOperationException()));
            Assert.AreEqual(
                Expected(Resources.Error_OpenFailed_Generic),
                OpenFailedPageViewModel.BuildMessage(Folder, new WorkspaceDirectoryMissingException(Folder)));
        });
    }

    [TestMethod]
    public void DetailsCarryTheEnglishExceptionTextInAnyLanguage()
    {
        WithCulture("zh-Hans", () =>
        {
            var exception = new SchemaVersionException(3, 1);
            var page = new OpenFailedPageViewModel(Folder, exception);

            Assert.AreEqual(Folder, page.FolderPath);
            StringAssert.Contains(page.Details, typeof(SchemaVersionException).FullName!);
            StringAssert.Contains(page.Details, exception.Message);
        });
    }
}
