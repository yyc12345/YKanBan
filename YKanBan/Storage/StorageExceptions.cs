namespace YKanBan.Storage;

/// <summary>
/// Base class for the typed exceptions raised by the storage layer.
/// Exception messages are always English and never localized; the UI maps the
/// exception type to a localized resource key instead.
/// </summary>
public abstract class YKanBanException : Exception
{
    /// <summary>
    /// Initializes the exception with an English diagnostic message.
    /// </summary>
    /// <param name="message">The English diagnostic message.</param>
    /// <param name="innerException">The underlying cause, when any.</param>
    protected YKanBanException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Signals that the folder passed on the command line does not exist on disk.
/// </summary>
public sealed class WorkspaceDirectoryMissingException : YKanBanException
{
    /// <summary>
    /// Gets the folder path that was expected to exist.
    /// </summary>
    public string FolderPath { get; }

    /// <summary>
    /// Initializes the exception for a folder that is absent on disk.
    /// </summary>
    /// <param name="folderPath">The missing folder path.</param>
    public WorkspaceDirectoryMissingException(string folderPath)
        : base($"The folder does not exist: {folderPath}")
    {
        FolderPath = folderPath;
    }
}

/// <summary>
/// Signals that a folder exists but has no .ykanban structure, so it is not
/// (yet) a workspace.
/// </summary>
public sealed class WorkspaceNotInitializedException : YKanBanException
{
    /// <summary>
    /// Gets the folder path that lacks the .ykanban structure.
    /// </summary>
    public string FolderPath { get; }

    /// <summary>
    /// Initializes the exception for a folder without a .ykanban structure.
    /// </summary>
    /// <param name="folderPath">The uninitialized folder path.</param>
    public WorkspaceNotInitializedException(string folderPath)
        : base($"The folder is not a YKanBan workspace (missing .ykanban): {folderPath}")
    {
        FolderPath = folderPath;
    }
}

/// <summary>
/// Signals that .ykanban exists but its database file does not, so the
/// workspace data is missing; a new empty database is never created silently.
/// </summary>
public sealed class WorkspaceDatabaseMissingException : YKanBanException
{
    /// <summary>
    /// Gets the database file that was expected to exist.
    /// </summary>
    public string DatabasePath { get; }

    /// <summary>
    /// Initializes the exception for a missing workspace database.
    /// </summary>
    /// <param name="databasePath">The missing database file path.</param>
    public WorkspaceDatabaseMissingException(string databasePath)
        : base($"The workspace database is missing: {databasePath}")
    {
        DatabasePath = databasePath;
    }
}

/// <summary>
/// Signals that another instance already holds the workspace lock, so this
/// process must not touch the workspace.
/// </summary>
public sealed class WorkspaceLockException : YKanBanException
{
    /// <summary>
    /// Gets the lock file that could not be acquired.
    /// </summary>
    public string LockFilePath { get; }

    /// <summary>
    /// Gets the best-effort holder diagnostics read from the lock file, or
    /// <see langword="null"/> when they could not be read.
    /// </summary>
    public string? HolderDiagnostics { get; }

    /// <summary>
    /// Initializes the exception for a failed lock acquisition.
    /// </summary>
    /// <param name="lockFilePath">The lock file that is already held.</param>
    /// <param name="holderDiagnostics">Diagnostics of the current holder, when readable.</param>
    /// <param name="innerException">The underlying I/O error.</param>
    public WorkspaceLockException(string lockFilePath, string? holderDiagnostics, Exception innerException)
        : base(BuildMessage(lockFilePath, holderDiagnostics), innerException)
    {
        LockFilePath = lockFilePath;
        HolderDiagnostics = holderDiagnostics;
    }

    /// <summary>
    /// Composes the diagnostic message, appending holder details when they were readable.
    /// </summary>
    /// <param name="lockFilePath">The lock file that is already held.</param>
    /// <param name="holderDiagnostics">Diagnostics of the current holder, when readable.</param>
    /// <returns>The English diagnostic message.</returns>
    private static string BuildMessage(string lockFilePath, string? holderDiagnostics)
    {
        string message = $"The workspace is locked by another instance: {lockFilePath}";
        if (!string.IsNullOrEmpty(holderDiagnostics))
        {
            message += $" Lock holder: {holderDiagnostics}";
        }
        return message;
    }
}

/// <summary>
/// Signals that the database was written by a newer build than the one
/// currently running, so its schema cannot be safely managed.
/// </summary>
public sealed class SchemaVersionException : YKanBanException
{
    /// <summary>
    /// Gets the user_version value found in the database.
    /// </summary>
    public long StoredVersion { get; }

    /// <summary>
    /// Gets the newest schema version this build understands.
    /// </summary>
    public int SupportedVersion { get; }

    /// <summary>
    /// Initializes the exception for an unmanageable schema version.
    /// </summary>
    /// <param name="storedVersion">The user_version read from the database.</param>
    /// <param name="supportedVersion">The newest schema version this build supports.</param>
    public SchemaVersionException(long storedVersion, int supportedVersion)
        : base($"Database schema version {storedVersion} is newer than the latest supported version {supportedVersion}.")
    {
        StoredVersion = storedVersion;
        SupportedVersion = supportedVersion;
    }
}

/// <summary>
/// The entities whose name/title is constrained by the schema to be non-empty
/// and table-unique.
/// </summary>
public enum NamedEntity
{
    /// <summary>A board column (constraint on columns.title).</summary>
    Column,

    /// <summary>A tag (constraint on tags.name).</summary>
    Tag,
}

/// <summary>
/// Signals that a column title or tag name was empty, violating the schema's
/// CHECK constraint. Nothing was written.
/// </summary>
public sealed class NameRequiredException : YKanBanException
{
    /// <summary>
    /// Gets the entity whose name was empty.
    /// </summary>
    public NamedEntity Entity { get; }

    /// <summary>
    /// Initializes the exception for an empty name.
    /// </summary>
    /// <param name="entity">The entity whose name was empty.</param>
    /// <param name="innerException">The underlying constraint violation.</param>
    public NameRequiredException(NamedEntity entity, Exception innerException)
        : base($"The {entity.ToString().ToLowerInvariant()} name must not be empty.", innerException)
    {
        Entity = entity;
    }
}

/// <summary>
/// Signals that a column title or tag name already exists (compared with ASCII
/// case folding), violating the schema's UNIQUE constraint. Nothing was written.
/// </summary>
public sealed class DuplicateNameException : YKanBanException
{
    /// <summary>
    /// Gets the entity whose name collided.
    /// </summary>
    public NamedEntity Entity { get; }

    /// <summary>
    /// Gets the rejected name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Initializes the exception for a name collision.
    /// </summary>
    /// <param name="entity">The entity whose name collided.</param>
    /// <param name="name">The rejected name.</param>
    /// <param name="innerException">The underlying constraint violation.</param>
    public DuplicateNameException(NamedEntity entity, string name, Exception innerException)
        : base($"A {entity.ToString().ToLowerInvariant()} named '{name}' already exists.", innerException)
    {
        Entity = entity;
        Name = name;
    }
}
