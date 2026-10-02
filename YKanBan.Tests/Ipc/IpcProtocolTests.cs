using YKanBan.Ipc;

namespace YKanBan.Tests.Ipc;

/// <summary>Tests for the IPC wire protocol codec.</summary>
[TestClass]
public class IpcProtocolTests
{
    [TestMethod]
    public void ShowMainEncodesAsSingleLine()
    {
        Assert.AreEqual("show\n", IpcProtocol.Encode(new LaunchCommand.ShowMain()));
    }

    [TestMethod]
    public void OpenWorkspaceEncodesCommandAndPathLines()
    {
        Assert.AreEqual(
            "open\nD:\\my repo\n",
            IpcProtocol.Encode(new LaunchCommand.OpenWorkspace(@"D:\my repo")));
    }

    [TestMethod]
    public void RoundTripPreservesCommands()
    {
        LaunchCommand[] commands =
        [
            new LaunchCommand.ShowMain(),
            new LaunchCommand.OpenWorkspace(@"D:\my repo"),
            new LaunchCommand.OpenWorkspace("/home/user/仓库"),
        ];

        foreach (LaunchCommand command in commands)
        {
            Assert.AreEqual(command, IpcProtocol.Decode(IpcProtocol.Encode(command)));
        }
    }

    [TestMethod]
    public void DecodeToleratesMissingTrailingNewline()
    {
        Assert.AreEqual(new LaunchCommand.ShowMain(), IpcProtocol.Decode("show"));
        Assert.AreEqual(
            new LaunchCommand.OpenWorkspace(@"C:\x"),
            IpcProtocol.Decode("open\nC:\\x"));
    }

    [TestMethod]
    public void DecodePreservesPathsWithSpacesAndUnicode()
    {
        LaunchCommand command = IpcProtocol.Decode("open\nD:\\我的 仓库\n");
        Assert.AreEqual(new LaunchCommand.OpenWorkspace(@"D:\我的 仓库"), command);
    }

    [TestMethod]
    public void DecodeRejectsEmptyPayload()
    {
        Assert.ThrowsExactly<FormatException>(() => IpcProtocol.Decode(string.Empty));
        Assert.ThrowsExactly<FormatException>(() => IpcProtocol.Decode("\n"));
    }

    [TestMethod]
    public void DecodeRejectsUnknownCommand()
    {
        Assert.ThrowsExactly<FormatException>(() => IpcProtocol.Decode("frobnicate\n"));
    }

    [TestMethod]
    public void DecodeRejectsOpenWithoutPath()
    {
        Assert.ThrowsExactly<FormatException>(() => IpcProtocol.Decode("open\n"));
        Assert.ThrowsExactly<FormatException>(() => IpcProtocol.Decode("open"));
        Assert.ThrowsExactly<FormatException>(() => IpcProtocol.Decode("open\n\n"));
    }

    [TestMethod]
    public void DecodeRejectsContentAfterShow()
    {
        Assert.ThrowsExactly<FormatException>(() => IpcProtocol.Decode("show\nextra\n"));
    }

    [TestMethod]
    public void EncodeRejectsNullCommand()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => IpcProtocol.Encode(null!));
    }
}
