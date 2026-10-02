using System.IO.Pipes;
using System.Text;

namespace YKanBan.Ipc;

/// <summary>
/// Mutex + named pipe implementation of <see cref="ISingleInstanceService"/>.
/// The mutex name is per-user-session (Local\ prefix), so separate users on
/// one machine run independently. A crash releases the mutex with the
/// process — no stale state survives.
/// </summary>
public sealed class SingleInstanceService : ISingleInstanceService
{
    private const string MutexName = @"Local\YKanBan.SingleInstance";
    private const string PipeName = "YKanBan.Ipc";
    private const int ConnectTimeoutMilliseconds = 2000;
    private const int ConnectAttempts = 3;
    private const int RetryDelayMilliseconds = 300;

    private readonly object _gate = new();
    private Mutex? _mutex;
    private CancellationTokenSource? _serverCancellation;
    private bool _isPrimary;

    /// <summary>Attempts to register this process as the single YKanBan instance.</summary>
    /// <exception cref="InvalidOperationException">Acquired a second time.</exception>
    public bool TryAcquireSingleInstance()
    {
        lock (_gate)
        {
            if (_mutex is not null)
            {
                throw new InvalidOperationException("Single instance already acquired.");
            }

            // Ownership is requested up front; createdNew tells primary from secondary.
            _mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
            _isPrimary = createdNew;
            return createdNew;
        }
    }

    /// <summary>Starts serving launch commands on a background task (primary instance only).</summary>
    /// <exception cref="InvalidOperationException">
    /// Not called on a primary instance, or called a second time.
    /// </exception>
    public void StartServer(Action<LaunchCommand> handler)
    {
        CancellationTokenSource cancellation;
        lock (_gate)
        {
            if (!_isPrimary || _mutex is null)
            {
                throw new InvalidOperationException("Only the primary instance can serve IPC.");
            }
            if (_serverCancellation is not null)
            {
                throw new InvalidOperationException("IPC server already started.");
            }
            _serverCancellation = new CancellationTokenSource();
            cancellation = _serverCancellation;
        }

        _ = Task.Run(() => ServerLoopAsync(handler, cancellation.Token));
    }

    /// <summary>Delivers a command to the primary instance and waits for its acknowledgement.</summary>
    public bool TrySend(LaunchCommand command)
    {
        string payload = IpcProtocol.Encode(command);

        // The primary may still be starting its server; retry briefly before giving up.
        for (int attempt = 0; attempt < ConnectAttempts; attempt++)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);
                client.Connect(ConnectTimeoutMilliseconds);

                using (var writer = new StreamWriter(client, Encoding.UTF8, bufferSize: 64, leaveOpen: true)
                {
                    AutoFlush = true,
                })
                {
                    writer.Write(payload);
                }

                using var reader = new StreamReader(client, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 64, leaveOpen: true);
                string acknowledgement = reader.ReadLine() ?? string.Empty;
                return acknowledgement == "ok";
            }
            catch (TimeoutException)
            {
                Thread.Sleep(RetryDelayMilliseconds);
            }
            catch (IOException)
            {
                Thread.Sleep(RetryDelayMilliseconds);
            }
        }
        return false;
    }

    /// <summary>Releases the mutex and stops the IPC server.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _serverCancellation?.Cancel();
            _serverCancellation?.Dispose();
            _serverCancellation = null;
            _mutex?.Dispose();
            _mutex = null;
            _isPrimary = false;
        }
    }

    /// <summary>Accepts one client at a time: read, decode, dispatch, acknowledge, disconnect.</summary>
    private static async Task ServerLoopAsync(Action<LaunchCommand> handler, CancellationToken cancellation)
    {
        while (!cancellation.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(PipeName, PipeDirection.InOut, maxNumberOfServerInstances: 1);
                await server.WaitForConnectionAsync(cancellation);

                string payload = await ReadMessageAsync(server, cancellation);
                LaunchCommand command = IpcProtocol.Decode(payload);

                // Dispatch to the app; handler failures must not kill the server.
                try
                {
                    handler(command);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Swallow handler exceptions: one bad dispatch must not stop serving.
                }

                WriteAcknowledgement(server);
                server.Disconnect();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException)
            {
                // Client vanished mid-conversation: drop the connection and keep serving.
            }
            catch (FormatException)
            {
                // Malformed payload: no acknowledgement is written; keep serving.
            }
        }
    }

    /// <summary>Reads the command line plus, for open, the path line.</summary>
    private static async Task<string> ReadMessageAsync(PipeStream pipe, CancellationToken cancellation)
    {
        using var reader = new StreamReader(pipe, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);

        string firstLine = await reader.ReadLineAsync(cancellation)
            ?? throw new IOException("Connection closed before the command line arrived.");
        if (firstLine != "open")
        {
            return firstLine;
        }

        string? pathLine = await reader.ReadLineAsync(cancellation);
        if (pathLine is null)
        {
            throw new IOException("Connection closed before the path line arrived.");
        }
        return $"open\n{pathLine}";
    }

    /// <summary>Writes the acknowledgement the sender waits for.</summary>
    private static void WriteAcknowledgement(PipeStream pipe)
    {
        using var writer = new StreamWriter(pipe, Encoding.UTF8, bufferSize: 64, leaveOpen: true)
        {
            AutoFlush = true,
        };
        writer.Write("ok\n");
    }
}
