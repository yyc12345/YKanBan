using YKanBan.Storage;
using YKanBan.Storage.Workspace;
using YKanBan.Tests.TestUtilities;

namespace YKanBan.Tests.Storage.Workspace;

/// <summary>
/// Tests for exclusive lock acquisition and release.
/// </summary>
[TestClass]
public class WorkspaceLockTests {
    [TestMethod]
    public void AcquireThenDisposeThenAcquireAgainSucceeds() {
        using var directory = new TempDirectory();
        WorkspaceInitializer.Initialize(directory.FullPath);

        WorkspaceLock.Acquire(directory.FullPath).Dispose();
        // Re-acquiring after release works: no stale lock residue survives.
        WorkspaceLock.Acquire(directory.FullPath).Dispose();
    }

    [TestMethod]
    public void SecondAcquireWhileHeldThrows() {
        using var directory = new TempDirectory();
        WorkspaceInitializer.Initialize(directory.FullPath);

        using WorkspaceLock first = WorkspaceLock.Acquire(directory.FullPath);
        WorkspaceLockException exception = Assert.ThrowsExactly<WorkspaceLockException>(
            () => WorkspaceLock.Acquire(directory.FullPath));
        Assert.AreEqual(WorkspacePaths.LockFile(directory.FullPath), exception.LockFilePath);
    }

    [TestMethod]
    public void AcquireWithoutYkanbanThrowsNotInitialized() {
        using var directory = new TempDirectory();
        Assert.ThrowsExactly<WorkspaceNotInitializedException>(
            () => WorkspaceLock.Acquire(directory.FullPath));
    }

    [TestMethod]
    public void LockFileIsEmptyAfterRelease() {
        using var directory = new TempDirectory();
        WorkspaceInitializer.Initialize(directory.FullPath);

        using (WorkspaceLock.Acquire(directory.FullPath)) {
        }

        // The lock file survives release but carries no content; only the OS handle matters.
        Assert.AreEqual(0L, new FileInfo(WorkspacePaths.LockFile(directory.FullPath)).Length);
    }
}
