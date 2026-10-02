namespace YKanBan.Ipc;

/// <summary>
/// Single-instance detection and inter-instance messaging, built on a named
/// mutex (detection) and a named pipe (command forwarding). The primary
/// instance holds the mutex for its lifetime and serves the pipe; secondary
/// instances forward their launch command to the primary and exit.
/// </summary>
public interface ISingleInstanceService : IDisposable
{
    /// <summary>Attempts to register this process as the single YKanBan instance.</summary>
    /// <returns>True when this process is the primary instance; false when another one exists.</returns>
    bool TryAcquireSingleInstance();

    /// <summary>Starts serving launch commands on a background task (primary instance only).</summary>
    /// <param name="handler">Invoked for each decoded command; runs on a background thread.</param>
    /// <exception cref="InvalidOperationException">
    /// Not called on a primary instance, or called a second time.
    /// </exception>
    void StartServer(Action<LaunchCommand> handler);

    /// <summary>Delivers a command to the primary instance and waits for its acknowledgement.</summary>
    /// <returns>True when the primary instance acknowledged the command.</returns>
    bool TrySend(LaunchCommand command);
}
