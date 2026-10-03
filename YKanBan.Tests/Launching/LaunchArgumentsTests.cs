using YKanBan.Launching;

namespace YKanBan.Tests.Launching;

/// <summary>
/// Tests for the <c>ykanban &lt;path&gt;</c> command-line contract across its
/// three branches: no argument, one valid path, and invalid input.
/// </summary>
[TestClass]
public class LaunchArgumentsTests
{
    [TestMethod]
    public void NoArgumentsIsRejected()
    {
        Assert.IsFalse(LaunchArguments.TryParse([], out string? path));
        Assert.IsNull(path);
    }

    [TestMethod]
    public void BlankArgumentIsRejected()
    {
        Assert.IsFalse(LaunchArguments.TryParse(["   "], out string? path));
        Assert.IsNull(path);
    }

    [TestMethod]
    public void MoreThanOneArgumentIsRejected()
    {
        Assert.IsFalse(LaunchArguments.TryParse(["a", "b"], out string? path));
        Assert.IsNull(path);
    }

    [TestMethod]
    public void SingleAbsolutePathIsAccepted()
    {
        string absolute = Path.Combine(Path.GetTempPath(), "Repo", "MyProject");
        bool parsed = LaunchArguments.TryParse([absolute], out string? path);
        Assert.IsTrue(parsed);
        Assert.AreEqual(absolute, path);
    }

    [TestMethod]
    public void RelativePathResolvesAgainstCurrentDirectory()
    {
        Assert.IsTrue(LaunchArguments.TryParse(["MyProject"], out string? path));
        Assert.AreEqual(Path.Combine(Environment.CurrentDirectory, "MyProject"), path);

        Assert.IsTrue(LaunchArguments.TryParse(["."], out path));
        Assert.AreEqual(Path.GetFullPath(Environment.CurrentDirectory), path);
    }

    [TestMethod]
    [DataRow("-")]
    [DataRow("-x")]
    [DataRow("--help")]
    [DataRow("--path=foo")]
    public void OptionLikeArgumentIsRejected(string argument)
    {
        Assert.IsFalse(LaunchArguments.TryParse([argument], out string? path));
        Assert.IsNull(path);
    }

    [TestMethod]
    public void OptionAlongsidePathIsRejected()
    {
        Assert.IsFalse(LaunchArguments.TryParse(["--help", "MyProject"], out string? path));
        Assert.IsNull(path);
    }

    [TestMethod]
    public void MalformedPathIsRejected()
    {
        Assert.IsFalse(LaunchArguments.TryParse(["a\0b"], out string? path));
        Assert.IsNull(path);
    }
}
