using YKanBan.Platform;
using YKanBan.Storage;
using YKanBan.Storage.AppData;
using YKanBan.Storage.Workspace;
using YKanBan.Tests.TestUtilities;

namespace YKanBan.Tests.Storage.Workspace;

/// <summary>Tests for the fixed-capacity LRU workspace session pool.</summary>
[TestClass]
public class WorkspaceConnectionPoolTests
{
    /// <summary>Owns three initialized workspaces (a, b, c), a registry and the pool under test.</summary>
    private sealed class PoolFixture : IDisposable
    {
        public TempDirectory Directory { get; } = new();

        public AppDatabase AppDb { get; }

        public WorkspaceConnectionPool Pool { get; }

        public PoolFixture(int capacity = 2)
        {
            foreach (string name in new[] { "a", "b", "c" })
            {
                string path = Path.Combine(Directory.FullPath, name);
                System.IO.Directory.CreateDirectory(path);
                WorkspaceInitializer.Initialize(path);
            }
            AppDb = new AppDatabase(Path.Combine(Directory.FullPath, "app.db"));
            Pool = new WorkspaceConnectionPool(capacity, AppDb, new PathComparer(ignoreCase: true));
        }

        /// <summary>Workspace folder path by name.</summary>
        public string Workspace(string name) => Path.Combine(Directory.FullPath, name);

        public void Dispose()
        {
            Pool.Dispose();
            AppDb.Dispose();
            Directory.Dispose();
        }
    }

    [TestMethod]
    public void OpenBeyondCapacityEvictsLeastRecentlyUsed()
    {
        using var fixture = new PoolFixture(capacity: 2);

        fixture.Pool.Open(fixture.Workspace("a"));
        fixture.Pool.Open(fixture.Workspace("b"));
        fixture.Pool.Open(fixture.Workspace("c"));

        Assert.AreEqual(2, fixture.Pool.OpenCount);
        Assert.IsNull(fixture.Pool.Find(fixture.Workspace("a")));
        Assert.IsNotNull(fixture.Pool.Find(fixture.Workspace("b")));
        Assert.IsNotNull(fixture.Pool.Find(fixture.Workspace("c")));
    }

    [TestMethod]
    public void OpenTwiceReturnsSameSession()
    {
        using var fixture = new PoolFixture();

        WorkspaceSession first = fixture.Pool.Open(fixture.Workspace("a"));
        WorkspaceSession second = fixture.Pool.Open(fixture.Workspace("a"));

        Assert.AreSame(first, second);
        Assert.AreEqual(1, fixture.Pool.OpenCount);
    }

    [TestMethod]
    public void TouchPromotesRecency()
    {
        using var fixture = new PoolFixture(capacity: 2);

        fixture.Pool.Open(fixture.Workspace("a"));
        fixture.Pool.Open(fixture.Workspace("b"));
        fixture.Pool.Touch(fixture.Workspace("a"));
        fixture.Pool.Open(fixture.Workspace("c"));

        // "a" was touched after "b" opened, so "b" is the LRU victim now.
        Assert.IsNotNull(fixture.Pool.Find(fixture.Workspace("a")));
        Assert.IsNull(fixture.Pool.Find(fixture.Workspace("b")));
    }

    [TestMethod]
    public void EvictedWorkspaceCanBeReopened()
    {
        using var fixture = new PoolFixture(capacity: 2);

        fixture.Pool.Open(fixture.Workspace("a"));
        fixture.Pool.Open(fixture.Workspace("b"));
        fixture.Pool.Open(fixture.Workspace("c"));

        // "a" was evicted; reopening it works and now evicts the LRU "b".
        WorkspaceSession reopened = fixture.Pool.Open(fixture.Workspace("a"));
        Assert.AreEqual("a", Path.GetFileName(reopened.FolderPath));
        Assert.IsNull(fixture.Pool.Find(fixture.Workspace("b")));
    }

    [TestMethod]
    public void OpenRegistersWorkspaceInSidebarRegistry()
    {
        using var fixture = new PoolFixture();

        fixture.Pool.Open(fixture.Workspace("a"));
        fixture.Pool.Open(fixture.Workspace("b"));

        IReadOnlyList<string> registered = fixture.AppDb.GetWorkspaces().Select(record => record.Path).ToList();
        Assert.AreEqual(2, registered.Count);
        Assert.IsTrue(registered.Contains(new PathComparer(ignoreCase: true).Normalize(fixture.Workspace("a"))));
        Assert.IsTrue(registered.Contains(new PathComparer(ignoreCase: true).Normalize(fixture.Workspace("b"))));
    }

    [TestMethod]
    public void OpenMissingDirectoryThrows()
    {
        using var fixture = new PoolFixture();
        string missing = Path.Combine(fixture.Directory.FullPath, "nowhere");

        Assert.ThrowsExactly<WorkspaceDirectoryMissingException>(() => fixture.Pool.Open(missing));
    }

    [TestMethod]
    public void OpenFolderWithoutYkanbanThrows()
    {
        using var fixture = new PoolFixture();
        string plain = Path.Combine(fixture.Directory.FullPath, "plain");
        System.IO.Directory.CreateDirectory(plain);

        Assert.ThrowsExactly<WorkspaceNotInitializedException>(() => fixture.Pool.Open(plain));
    }

    [TestMethod]
    public void OpenWithLockHeldElsewhereThrowsThenSucceedsAfterRelease()
    {
        using var fixture = new PoolFixture();

        using WorkspaceLock holder = WorkspaceLock.Acquire(fixture.Workspace("a"));
        Assert.ThrowsExactly<WorkspaceLockException>(() => fixture.Pool.Open(fixture.Workspace("a")));

        holder.Dispose();
        WorkspaceSession session = fixture.Pool.Open(fixture.Workspace("a"));
        Assert.AreEqual("a", Path.GetFileName(session.FolderPath));
    }

    [TestMethod]
    public void RemoveReleasesResourcesAndForgetsRegistryEntry()
    {
        using var fixture = new PoolFixture();

        fixture.Pool.Open(fixture.Workspace("a"));
        Assert.IsTrue(fixture.Pool.Remove(fixture.Workspace("a")));

        Assert.IsNull(fixture.Pool.Find(fixture.Workspace("a")));
        Assert.AreEqual(0, fixture.AppDb.GetWorkspaces().Count);

        // Resources are truly released: the lock can be taken again.
        using WorkspaceLock reacquired = WorkspaceLock.Acquire(fixture.Workspace("a"));
    }

    [TestMethod]
    public void PathEquivalentsShareOneSession()
    {
        using var fixture = new PoolFixture();
        string path = fixture.Workspace("a");
        string differentlyCased = Path.Combine(
            Path.GetDirectoryName(path)!,
            Path.GetFileName(path).ToUpperInvariant());

        WorkspaceSession session = fixture.Pool.Open(path);
        WorkspaceSession same = fixture.Pool.Open(differentlyCased);

        Assert.AreSame(session, same);
        Assert.AreEqual(1, fixture.Pool.OpenCount);
    }

    [TestMethod]
    public void DisposeReleasesAllLocks()
    {
        using var fixture = new PoolFixture();

        fixture.Pool.Open(fixture.Workspace("a"));
        fixture.Pool.Open(fixture.Workspace("b"));
        fixture.Pool.Dispose();

        using WorkspaceLock lockA = WorkspaceLock.Acquire(fixture.Workspace("a"));
        using WorkspaceLock lockB = WorkspaceLock.Acquire(fixture.Workspace("b"));
    }
}
