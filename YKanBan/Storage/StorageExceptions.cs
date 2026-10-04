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
    /// Initializes the exception for a failed lock acquisition.
    /// </summary>
    /// <param name="lockFilePath">The lock file that is already held.</param>
    /// <param name="innerException">The underlying I/O error.</param>
    public WorkspaceLockException(string lockFilePath, Exception innerException)
        : base($"The workspace is locked by another instance: {lockFilePath}", innerException)
    {
        LockFilePath = lockFilePath;
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
