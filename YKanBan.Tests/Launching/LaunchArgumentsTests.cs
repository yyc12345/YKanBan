using YKanBan.Launching;
using YKanBan.Tests.TestUtilities;

namespace YKanBan.Tests.Launching;

/// <summary>
/// Tests for the <c>ykanban &lt;path&gt;</c> command-line contract: exactly one
/// existing folder, resolved to an absolute path; everything else is rejected.
/// </summary>
[TestClass]
public class LaunchArgumentsTests {
    [TestMethod]
    public void NoArgumentsIsRejected() {
        Assert.IsFalse(LaunchArguments.TryParse([], out string? path));
        Assert.IsNull(path);
    }

    [TestMethod]
    public void BlankArgumentIsRejected() {
        Assert.IsFalse(LaunchArguments.TryParse(["   "], out string? path));
        Assert.IsNull(path);
    }

    [TestMethod]
    public void MoreThanOneArgumentIsRejected() {
        Assert.IsFalse(LaunchArguments.TryParse(["a", "b"], out string? path));
        Assert.IsNull(path);
    }

    [TestMethod]
    [DataRow("--help")]
    [DataRow("-x")]
    public void OptionLikeArgumentIsRejected(string argument) {
        Assert.IsFalse(LaunchArguments.TryParse([argument], out string? path));
        Assert.IsNull(path);
    }

    [TestMethod]
    public void NonExistentFolderIsRejected() {
        using var directory = new TempDirectory();
        string missing = Path.Combine(directory.FullPath, "nope");

        Assert.IsFalse(LaunchArguments.TryParse([missing], out string? path));
        Assert.IsNull(path);
    }

    [TestMethod]
    public void FileInsteadOfFolderIsRejected() {
        using var directory = new TempDirectory();
        string file = Path.Combine(directory.FullPath, "file.txt");
        File.WriteAllText(file, "x");

        Assert.IsFalse(LaunchArguments.TryParse([file], out string? path));
        Assert.IsNull(path);
    }

    [TestMethod]
    public void ExistingFolderIsAcceptedAsAnAbsolutePath() {
        using var directory = new TempDirectory();

        bool parsed = LaunchArguments.TryParse([directory.FullPath], out string? path);

        Assert.IsTrue(parsed);
        Assert.AreEqual(Path.GetFullPath(directory.FullPath), path);
        Assert.IsTrue(Path.IsPathRooted(path));
    }

    [TestMethod]
    public void RelativePathIsResolvedToAbsolute() {
        using var directory = new TempDirectory();
        string relative = Path.GetRelativePath(Environment.CurrentDirectory, directory.FullPath);

        bool parsed = LaunchArguments.TryParse([relative], out string? path);

        Assert.IsTrue(parsed);
        Assert.IsTrue(Path.IsPathRooted(path));
        Assert.AreEqual(Path.GetFullPath(relative), path);
    }

    [TestMethod]
    public void MalformedPathIsRejected() {
        Assert.IsFalse(LaunchArguments.TryParse(["bad\0path"], out string? path));
        Assert.IsNull(path);
    }
}
