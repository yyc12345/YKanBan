using YKanBan.Platform;
using YKanBan.Storage.AppData;

namespace YKanBan.Storage.Workspace;

/// <summary>
/// Fixed-capacity LRU pool of open workspace sessions (exclusive lock plus
/// repository per workspace). Opening a workspace also registers it in the
/// global app.db sidebar registry. Eviction — by capacity or explicitly —
/// only releases resources: the sidebar entry survives, and the workspace
/// can be reopened at any time (a lock held elsewhere then surfaces as a
/// lock conflict).
/// </summary>
public sealed class WorkspaceConnectionPool : IDisposable
{
    private readonly object _gate = new();
    private readonly int _capacity;
    private readonly AppDatabase _appDatabase;
    private readonly PathComparer _pathComparer;
    private readonly Dictionary<string, LinkedListNode<WorkspaceSession>> _sessions;
    private readonly LinkedList<WorkspaceSession> _recency = new();

    /// <summary>Creates a pool.</summary>
    /// <param name="capacity">Maximum number of simultaneously open workspaces.</param>
    /// <param name="appDatabase">Global registry for sidebar entries and last_edited_at.</param>
    /// <param name="pathComparer">Platform path rules used to identify workspaces.</param>
    public WorkspaceConnectionPool(int capacity, AppDatabase appDatabase, PathComparer pathComparer)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
        _appDatabase = appDatabase;
        _pathComparer = pathComparer;
        _sessions = new Dictionary<string, LinkedListNode<WorkspaceSession>>(pathComparer);
    }

    /// <summary>Number of currently open workspaces.</summary>
    public int OpenCount
    {
        get
        {
            lock (_gate)
            {
                return _sessions.Count;
            }
        }
    }

    /// <summary>Returns the open session and marks it most recently used, or null when the workspace is not open.</summary>
    public WorkspaceSession? Find(string folderPath)
    {
        lock (_gate)
        {
            if (_sessions.TryGetValue(_pathComparer.Normalize(folderPath), out LinkedListNode<WorkspaceSession>? node))
            {
                Promote(node);
                return node.Value;
            }
            return null;
        }
    }

    /// <summary>Records user activity (click/switch) for an open workspace; no-op when it is not open.</summary>
    public void Touch(string folderPath) => Find(folderPath);

    /// <summary>Opens the workspace session, or returns the existing one; evicts LRU entries beyond capacity.</summary>
    /// <exception cref="WorkspaceDirectoryMissingException">The folder does not exist.</exception>
    /// <exception cref="WorkspaceNotInitializedException">The folder has no .ykanban.</exception>
    /// <exception cref="WorkspaceLockException">Another instance holds the lock.</exception>
    public WorkspaceSession Open(string folderPath)
    {
        string canonical = _pathComparer.Normalize(folderPath);

        // Fast path: already open → promote and return.
        lock (_gate)
        {
            if (_sessions.TryGetValue(canonical, out LinkedListNode<WorkspaceSession>? open))
            {
                Promote(open);
                return open.Value;
            }
        }

        // Validate the folder state up front so callers get precise state errors.
        if (!Directory.Exists(canonical))
        {
            throw new WorkspaceDirectoryMissingException(canonical);
        }
        if (!WorkspaceInitializer.IsWorkspace(canonical))
        {
            throw new WorkspaceNotInitializedException(canonical);
        }

        // Acquire the exclusive lock before touching the database.
        WorkspaceLock workspaceLock = WorkspaceLock.Acquire(canonical);
        WorkspaceSession session;
        try
        {
            var repository = new WorkspaceRepository(canonical, _appDatabase);
            session = new WorkspaceSession
            {
                FolderPath = canonical,
                Lock = workspaceLock,
                Repository = repository,
            };
        }
        catch
        {
            workspaceLock.Dispose();
            throw;
        }

        lock (_gate)
        {
            // Re-check: another open may have won the race; the existing session wins.
            if (_sessions.TryGetValue(canonical, out LinkedListNode<WorkspaceSession>? winner))
            {
                session.Dispose();
                Promote(winner);
                return winner.Value;
            }

            _sessions.Add(canonical, _recency.AddFirst(session));

            // Evict least recently used entries beyond capacity; releases resources only.
            while (_sessions.Count > _capacity)
            {
                LinkedListNode<WorkspaceSession> lru = _recency.Last!;
                _recency.RemoveLast();
                _sessions.Remove(lru.Value.FolderPath);
                lru.Value.Dispose();
            }

            // Opening registers the workspace in the persistent sidebar registry.
            _appDatabase.UpsertWorkspace(canonical);
            return session;
        }
    }

    /// <summary>Releases a workspace's resources; the sidebar entry is kept. Returns true when it was open.</summary>
    public bool Close(string folderPath)
    {
        lock (_gate)
        {
            if (!_sessions.Remove(_pathComparer.Normalize(folderPath), out LinkedListNode<WorkspaceSession>? node))
            {
                return false;
            }
            _recency.Remove(node);
            node.Value.Dispose();
            return true;
        }
    }

    /// <summary>Releases the workspace's resources and removes its sidebar entry. Returns true when it was open.</summary>
    public bool Remove(string folderPath)
    {
        string canonical = _pathComparer.Normalize(folderPath);
        bool wasOpen = Close(canonical);
        _appDatabase.RemoveWorkspace(canonical);
        return wasOpen;
    }

    /// <summary>Releases all sessions.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            foreach (WorkspaceSession session in _recency)
            {
                session.Dispose();
            }
            _recency.Clear();
            _sessions.Clear();
        }
    }

    /// <summary>Moves a session node to the most-recently-used position.</summary>
    private void Promote(LinkedListNode<WorkspaceSession> node)
    {
        _recency.Remove(node);
        _recency.AddFirst(node);
    }
}
