using YKanBan.Models;
using YKanBan.Storage;
using YKanBan.Storage.Workspace;
using YKanBan.Tests.TestUtilities;

namespace YKanBan.Tests.Storage.Workspace;

/// <summary>
/// Tests for the workspace session lifecycle: the exclusive lock is held for
/// the whole session and the repository is usable through it.
/// </summary>
[TestClass]
public class WorkspaceSessionTests {
    [TestMethod]
    public void OpenHoldsLockUntilDisposed() {
        using var directory = new TempDirectory();
        WorkspaceInitializer.Initialize(directory.FullPath);

        using WorkspaceSession session = WorkspaceSession.Open(directory.FullPath);

        // A second open while the session is alive must fail on the lock.
        Assert.ThrowsExactly<WorkspaceLockException>(() => WorkspaceSession.Open(directory.FullPath));
    }

    [TestMethod]
    public void DisposeReleasesLockForReopen() {
        using var directory = new TempDirectory();
        WorkspaceInitializer.Initialize(directory.FullPath);

        WorkspaceSession.Open(directory.FullPath).Dispose();

        using WorkspaceSession reopened = WorkspaceSession.Open(directory.FullPath);
        Assert.AreEqual(directory.FullPath, reopened.FolderPath);
    }

    [TestMethod]
    public void OpenUninitializedFolderThrowsNotInitialized() {
        using var directory = new TempDirectory();

        Assert.ThrowsExactly<WorkspaceNotInitializedException>(
            () => WorkspaceSession.Open(directory.FullPath));
    }

    [TestMethod]
    public void OpenWithoutDatabaseThrowsDatabaseMissing() {
        using var directory = new TempDirectory();
        WorkspaceInitializer.Initialize(directory.FullPath);
        File.Delete(WorkspacePaths.Database(directory.FullPath));

        Assert.ThrowsExactly<WorkspaceDatabaseMissingException>(
            () => WorkspaceSession.Open(directory.FullPath));
    }

    [TestMethod]
    public void InitializeHoldsLockAndSeedsPreset() {
        using var directory = new TempDirectory();

        using WorkspaceSession session = WorkspaceSession.Initialize(directory.FullPath);

        // The lock is held for the whole session, so a second initialize cannot interleave.
        Assert.ThrowsExactly<WorkspaceLockException>(() => WorkspaceSession.Initialize(directory.FullPath));

        // The preset columns and tags were written under the same lock.
        Assert.AreEqual(3, session.Repository.GetColumns().Count);
        Assert.AreEqual(8, session.Repository.GetTags().Count);
    }

    [TestMethod]
    public void InitializeExistingWorkspaceThrowsAndReleasesLock() {
        using var directory = new TempDirectory();
        WorkspaceInitializer.Initialize(directory.FullPath);

        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceSession.Initialize(directory.FullPath));

        // The failed initialize released the lock, so the workspace can still be opened.
        using WorkspaceSession session = WorkspaceSession.Open(directory.FullPath);
        Assert.AreEqual(directory.FullPath, session.FolderPath);
    }

    [TestMethod]
    public void RepositoryIsUsableThroughSession() {
        using var directory = new TempDirectory();
        WorkspaceInitializer.Initialize(directory.FullPath);

        using WorkspaceSession session = WorkspaceSession.Open(directory.FullPath);
        TagModel tag = session.Repository.AddTag("bug", RgbColor.Parse("#112233"), "");

        Assert.AreEqual("bug", session.Repository.GetTags().Single().Name);
        Assert.AreEqual(tag.Id, session.Repository.GetTags().Single().Id);
    }
}
