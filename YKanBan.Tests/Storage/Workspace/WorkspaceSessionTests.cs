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
public class WorkspaceSessionTests
{
    [TestMethod]
    public void OpenHoldsLockUntilDisposed()
    {
        using var directory = new TempDirectory();
        WorkspaceInitializer.Initialize(directory.FullPath);

        using WorkspaceSession session = WorkspaceSession.Open(directory.FullPath);

        // A second open while the session is alive must fail on the lock.
        Assert.ThrowsExactly<WorkspaceLockException>(() => WorkspaceSession.Open(directory.FullPath));
    }

    [TestMethod]
    public void DisposeReleasesLockForReopen()
    {
        using var directory = new TempDirectory();
        WorkspaceInitializer.Initialize(directory.FullPath);

        WorkspaceSession.Open(directory.FullPath).Dispose();

        using WorkspaceSession reopened = WorkspaceSession.Open(directory.FullPath);
        Assert.AreEqual(directory.FullPath, reopened.FolderPath);
    }

    [TestMethod]
    public void OpenUninitializedFolderThrowsNotInitialized()
    {
        using var directory = new TempDirectory();

        Assert.ThrowsExactly<WorkspaceNotInitializedException>(
            () => WorkspaceSession.Open(directory.FullPath));
    }

    [TestMethod]
    public void RepositoryIsUsableThroughSession()
    {
        using var directory = new TempDirectory();
        WorkspaceInitializer.Initialize(directory.FullPath);

        using WorkspaceSession session = WorkspaceSession.Open(directory.FullPath);
        TagModel tag = session.Repository.AddTag("bug", RgbColor.Parse("#112233"), "");

        Assert.AreEqual("bug", session.Repository.GetTags().Single().Name);
        Assert.AreEqual(tag.Id, session.Repository.GetTags().Single().Id);
    }

    [TestMethod]
    public void InitializeAddsPresetColumnsAndHoldsTheLock()
    {
        using var directory = new TempDirectory();

        using WorkspaceSession session = WorkspaceSession.Initialize(directory.FullPath);
        Assert.AreEqual(3, session.Repository.GetColumns().Count);
        Assert.ThrowsExactly<WorkspaceLockException>(() => WorkspaceSession.Open(directory.FullPath));
    }

    [TestMethod]
    public void InitializeWhileAnotherInstanceHoldsTheLockThrowsLockConflict()
    {
        using var directory = new TempDirectory();
        Directory.CreateDirectory(WorkspacePaths.Root(directory.FullPath));
        using WorkspaceLock held = WorkspaceLock.Acquire(directory.FullPath);

        Assert.ThrowsExactly<WorkspaceLockException>(() => WorkspaceSession.Initialize(directory.FullPath));
        Assert.IsFalse(File.Exists(WorkspacePaths.Database(directory.FullPath)));
    }

    [TestMethod]
    public void FailedInitializeReleasesTheLock()
    {
        using var directory = new TempDirectory();
        WorkspaceInitializer.Initialize(directory.FullPath);

        // The database already exists, so initialization fails after taking the lock.
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceSession.Initialize(directory.FullPath));
        using WorkspaceSession reopened = WorkspaceSession.Open(directory.FullPath);
    }

    [TestMethod]
    public void OpenWithMissingDatabaseThrowsAndCreatesNothing()
    {
        using var directory = new TempDirectory();
        WorkspaceInitializer.Initialize(directory.FullPath);
        string databasePath = WorkspacePaths.Database(directory.FullPath);
        File.Delete(databasePath);

        Assert.ThrowsExactly<WorkspaceDatabaseMissingException>(() => WorkspaceSession.Open(directory.FullPath));
        Assert.IsFalse(File.Exists(databasePath));

        // The lock was released again: the next attempt fails the same way, not with a lock conflict.
        Assert.ThrowsExactly<WorkspaceDatabaseMissingException>(() => WorkspaceSession.Open(directory.FullPath));
    }
}
