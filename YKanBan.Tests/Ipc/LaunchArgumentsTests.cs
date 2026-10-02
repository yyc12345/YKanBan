using YKanBan.Ipc;

namespace YKanBan.Tests.Ipc;

/// <summary>Tests for command-line argument parsing.</summary>
[TestClass]
public class LaunchArgumentsTests
{
    [TestMethod]
    public void NoArgumentsYieldShowMain()
    {
        Assert.AreEqual(new LaunchCommand.ShowMain(), LaunchArguments.Parse([]));
    }

    [TestMethod]
    public void BlankArgumentsOnlyYieldShowMain()
    {
        Assert.AreEqual(new LaunchCommand.ShowMain(), LaunchArguments.Parse(["", "  "]));
    }

    [TestMethod]
    public void SingleArgumentBecomesWorkspacePath()
    {
        Assert.AreEqual(
            new LaunchCommand.OpenWorkspace(@"D:\repo"),
            LaunchArguments.Parse([@"D:\repo"]));
    }

    [TestMethod]
    public void PathWithSpacesIsPreserved()
    {
        Assert.AreEqual(
            new LaunchCommand.OpenWorkspace(@"D:\my repo\ws"),
            LaunchArguments.Parse([@"D:\my repo\ws"]));
    }

    [TestMethod]
    public void FirstMeaningfulArgumentWins()
    {
        Assert.AreEqual(
            new LaunchCommand.OpenWorkspace(@"D:\a"),
            LaunchArguments.Parse([@"D:\a", @"D:\b"]));
        Assert.AreEqual(
            new LaunchCommand.OpenWorkspace(@"D:\b"),
            LaunchArguments.Parse(["", @"D:\b", @"D:\c"]));
    }
}
