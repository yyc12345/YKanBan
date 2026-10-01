namespace YKanBan.Storage;

/// <summary>
/// Typed exceptions thrown by the YKanBan storage and application layers.
/// Exception messages are always English and never localized; the UI maps
/// exception types to localized resource keys instead.
/// </summary>
public abstract class YKanBanException : Exception
{
    /// <summary>Creates an exception with an English diagnostic message.</summary>
    protected YKanBanException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>Signals that the workspace folder does not exist on disk.</summary>
public sealed class WorkspaceDirectoryMissingException : YKanBanException
{
    /// <summary>The folder path that was expected to exist.</summary>
    public string FolderPath { get; }

    /// <summary>Creates an exception for a folder that is absent on disk.</summary>
    public WorkspaceDirectoryMissingException(string folderPath)
        : base($"The folder does not exist: {folderPath}")
    {
        FolderPath = folderPath;
    }
}

/// <summary>Signals that the folder has no .ykanban structure yet.</summary>
public sealed class WorkspaceNotInitializedException : YKanBanException
{
    /// <summary>The folder path that lacks the .ykanban structure.</summary>
    public string FolderPath { get; }

    /// <summary>Creates an exception for a folder without .ykanban.</summary>
    public WorkspaceNotInitializedException(string folderPath)
        : base($"The folder is not a YKanBan workspace (missing .ykanban): {folderPath}")
    {
        FolderPath = folderPath;
    }
}

/// <summary>Signals that another instance holds the workspace lock.</summary>
public sealed class WorkspaceLockException : YKanBanException
{
    /// <summary>The lock file that could not be acquired.</summary>
    public string LockFilePath { get; }

    /// <summary>Best-effort diagnostics (PID/machine/time) read from the lock file, if readable.</summary>
    public string? HolderDiagnostics { get; }

    /// <summary>Creates an exception for a failed lock acquisition.</summary>
    public WorkspaceLockException(string lockFilePath, string? holderDiagnostics, Exception innerException)
        : base(BuildMessage(lockFilePath, holderDiagnostics), innerException)
    {
        LockFilePath = lockFilePath;
        HolderDiagnostics = holderDiagnostics;
    }

    /// <summary>Composes the message, including holder details when they were readable.</summary>
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

/// <summary>Signals that the stored schema version is not manageable by this build.</summary>
public sealed class SchemaVersionException : YKanBanException
{
    /// <summary>The user_version value found in the database.</summary>
    public long StoredVersion { get; }

    /// <summary>The latest schema version this build understands.</summary>
    public int SupportedVersion { get; }

    /// <summary>Creates an exception for an unmanageable schema version.</summary>
    public SchemaVersionException(long storedVersion, int supportedVersion)
        : base($"Database schema version {storedVersion} is newer than the latest supported version {supportedVersion}.")
    {
        StoredVersion = storedVersion;
        SupportedVersion = supportedVersion;
    }
}
