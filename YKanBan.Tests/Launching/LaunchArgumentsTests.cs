using YKanBan.Launching;

namespace YKanBan.Tests.Launching;

/// <summary>
/// Tests for the <c>ykanban &lt;path&gt;</c> command-line contract across its
/// three branches: no argument, one valid path, and invalid input.
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
    public void SinglePathIsAccepted() {
        bool parsed = LaunchArguments.TryParse([@"C:\Repo\MyProject"], out string? path);
        Assert.IsTrue(parsed);
        Assert.AreEqual(@"C:\Repo\MyProject", path);
    }
}
