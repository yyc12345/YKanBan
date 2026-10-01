using YKanBan.Storage;
using YKanBan.Storage.Workspace;
using YKanBan.Tests.TestUtilities;

namespace YKanBan.Tests.Storage.Workspace;

/// <summary>Tests for exclusive lock acquisition, diagnostics and release.</summary>
[TestClass]
public class WorkspaceLockTests
{
    [TestMethod]
    public void AcquireThenDisposeThenAcquireAgainSucceeds()
    {
        using var directory = new TempDirectory();
        WorkspaceInitializer.Initialize(directory.FullPath);

        WorkspaceLock.Acquire(directory.FullPath).Dispose();
        // Re-acquiring after release works: no stale lock residue survives.
        WorkspaceLock.Acquire(directory.FullPath).Dispose();
    }

    [TestMethod]
    public void SecondAcquireWhileHeldThrows()
    {
        using var directory = new TempDirectory();
        WorkspaceInitializer.Initialize(directory.FullPath);

        using WorkspaceLock first = WorkspaceLock.Acquire(directory.FullPath);
        WorkspaceLockException ex = Assert.ThrowsExactly<WorkspaceLockException>(
            () => WorkspaceLock.Acquire(directory.FullPath));
        Assert.AreEqual(WorkspacePaths.LockFile(directory.FullPath), ex.LockFilePath);
    }

    [TestMethod]
    public void AcquireWithoutYkanbanThrowsNotInitialized()
    {
        using var directory = new TempDirectory();
        Assert.ThrowsExactly<WorkspaceNotInitializedException>(
            () => WorkspaceLock.Acquire(directory.FullPath));
    }

    [TestMethod]
    public void LockFileContainsHolderDiagnostics()
    {
        using var directory = new TempDirectory();
        WorkspaceInitializer.Initialize(directory.FullPath);

        using (WorkspaceLock.Acquire(directory.FullPath))
        {
        }

        // The file survives release and carries PID / machine name / time for humans.
        string content = File.ReadAllText(WorkspacePaths.LockFile(directory.FullPath));
        StringAssert.Contains(content, "pid=");
        StringAssert.Contains(content, $"machine={Environment.MachineName}");
    }
}
