using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using Bukit.Shared;
using Bukit.Shared.Processes;

namespace Bukit.Cli.Deploy;

/// <summary>
/// Hands the deploy token to git's credential helper over a private local endpoint instead of
/// git's environment (plan section 3). Unix uses a socket in a fresh 0700 directory; Windows
/// a current-user-only named pipe. Every connection is checked by
/// <see cref="GitCredentialPeerPolicy"/> before the token is written.
/// </summary>
internal sealed class GitCredentialBroker : IAsyncDisposable
{
    private const int MaxConcurrentConnections = 4;
    private const int MacSocketPathLimit = 103;
    private const int LinuxSocketPathLimit = 107;
    private const int SolLocal = 0;
    private const int LocalPeerToken = 6;
    private const int AuditTokenSize = 32;
    private const int SolSocket = 1;
    private const int SoPeerCred = 17;
    private const int UcredSize = 12;
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(1);

    private readonly string _token;
    private readonly GitCredentialPeerPolicy _policy;
    private readonly ILogger? _logger;
    private readonly CancellationTokenSource _stop = new();
    private readonly Socket? _listener;
    private readonly ConcurrentDictionary<Task, byte> _connections = new();
    private Task _acceptLoop = Task.CompletedTask;
    private string? _lastDenial;
    private int _active;
    private int _disposed;
    private int _pipeCreated;

    private GitCredentialBroker(string token, GitCredentialPeerPolicy policy, ILogger? logger, string endpoint, string? directory, Socket? listener)
    {
        _token = token;
        _policy = policy;
        _logger = logger;
        Endpoint = endpoint;
        Directory = directory;
        _listener = listener;
    }

    /// <summary>The socket path (Unix) or pipe name (Windows) passed to the helper.</summary>
    internal string Endpoint { get; }

    /// <summary>The private directory holding the socket on Unix; null on Windows.</summary>
    internal string? Directory { get; }

    internal static GitCredentialBroker Start(string token, GitCredentialPeerPolicy policy, ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        ArgumentNullException.ThrowIfNull(policy);
        // A line break would let the token inject extra git credential protocol lines.
        if (token.AsSpan().IndexOfAny('\n', '\r', '\0') >= 0)
        {
            throw new ArgumentException("The deploy token must not contain line breaks or NUL characters.", nameof(token));
        }

        GitCredentialBroker broker;
        if (OperatingSystem.IsWindows())
        {
            broker = new GitCredentialBroker(token, policy, logger, $"bukit-cred-{RandomHex(16)}", directory: null, listener: null);
            broker._acceptLoop = Task.Run(broker.AcceptPipesAsync);
            return broker;
        }

        var directory = CreatePrivateDirectory();
        var socketPath = Path.Combine(directory, "c.sock");
        Socket? listener = null;
        try
        {
            listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            listener.Bind(new UnixDomainSocketEndPoint(socketPath));
            listener.Listen(MaxConcurrentConnections);
        }
        catch
        {
            listener?.Dispose();
            TryDeleteDirectory(directory, logger);
            throw;
        }

        broker = new GitCredentialBroker(token, policy, logger, socketPath, directory, listener);
        broker._acceptLoop = Task.Run(broker.AcceptSocketsAsync);
        return broker;
    }

    /// <summary>The most recent refusal since the last call, for the deploy error (plan Q5).</summary>
    internal string? TakeLastDenial() => Interlocked.Exchange(ref _lastDenial, null);

    /// <summary>Trusts a started git process until the returned handle is disposed.</summary>
    internal IDisposable? RegisterGitProcess(int gitPid) => _policy.Register(gitPid);

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await _stop.CancelAsync().ConfigureAwait(false);
        _listener?.Dispose();
        try
        {
            var work = Task.WhenAll([_acceptLoop, .. _connections.Keys]);
            if (!await GitHubPagesDeployProvider.WaitForTerminationGraceAsync(work, StopTimeout).ConfigureAwait(false))
            {
                _logger?.Warn("event=deploy.credential.broker.stop.timeout");
            }
        }
        catch (Exception ex)
        {
            // The loops log their own failures; stopping must still clean up.
            _logger?.Warn($"event=deploy.credential.broker.stop.failed reason={ex.GetType().Name}");
        }

        if (Directory is not null)
        {
            TryDeleteDirectory(Directory, _logger);
        }

        _stop.Dispose();
    }

    private async Task AcceptSocketsAsync()
    {
        var ct = _stop.Token;
        while (!ct.IsCancellationRequested)
        {
            Socket client;
            try
            {
                client = await _listener!.AcceptAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException || ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger?.Warn($"event=deploy.credential.broker.accept.failed reason={ex.GetType().Name}");
                await PauseAsync(ct).ConfigureAwait(false);
                continue;
            }

            if (!TryTakeSlot())
            {
                client.Dispose();
                continue;
            }

            Track(HandleSocketAsync(client));
        }
    }

    private async Task HandleSocketAsync(Socket client)
    {
        try
        {
            using (client)
            await using (var stream = new NetworkStream(client, ownsSocket: false))
            {
                var (pid, uid) = ReadSocketPeer(client);
                await ServeAsync(stream, pid, uid).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
        {
            _logger?.Debug($"event=deploy.credential.broker.connection.failed reason={ex.GetType().Name}");
        }
        finally
        {
            ReleaseSlot();
        }
    }

    [SupportedOSPlatform("windows")]
    private async Task AcceptPipesAsync()
    {
        var ct = _stop.Token;
        while (!ct.IsCancellationRequested)
        {
            if (!TryTakeSlot())
            {
                await PauseAsync(ct).ConfigureAwait(false);
                continue;
            }

            // Each instance owns its pipe; the loop opens the next one once a client connects.
            var connected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Track(ListenOnceAsync(connected, ct));
            try
            {
                await connected.Task.WaitAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    [SupportedOSPlatform("windows")]
    private async Task ListenOnceAsync(TaskCompletionSource connected, CancellationToken ct)
    {
        try
        {
            await using var pipe = CreatePipe();
            await pipe.WaitForConnectionAsync(ct).ConfigureAwait(false);
            connected.TrySetResult();
            await ServePipeAsync(pipe).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException || ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            // e.g. "All pipe instances are busy" while another program holds instances.
            _logger?.Warn($"event=deploy.credential.broker.connection.failed reason={ex.GetType().Name}");
            await PauseAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            connected.TrySetResult();
            ReleaseSlot();
        }
    }

    /// <summary>
    /// The ACL names only the current user's SID. <see cref="PipeOptions.CurrentUserOnly"/> is not
    /// used: it grants the token owner, which is BUILTIN\Administrators in an elevated process.
    /// .NET rejects any other option bit (there is no PIPE_REJECT_REMOTE_CLIENTS), so remote
    /// clients are refused after connect.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private NamedPipeServerStream CreatePipe()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var user = identity.User ?? throw new InvalidOperationException("The current Windows user has no SID.");
        var security = new PipeSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        // CreateNewInstance lets the accept loop open the next instance while one is in use.
        security.AddAccessRule(new PipeAccessRule(user, PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance, AccessControlType.Allow));
        security.SetOwner(user);
        // The first instance must create the name: an existing pipe of that name is not ours.
        // The accept loop opens instances one at a time, so the flag needs no lock.
        var first = Volatile.Read(ref _pipeCreated) == 0;
        var pipe = NamedPipeServerStreamAcl.Create(
            Endpoint,
            PipeDirection.InOut,
            MaxConcurrentConnections,
            PipeTransmissionMode.Byte,
            first ? PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance : PipeOptions.Asynchronous,
            inBufferSize: 0,
            outBufferSize: 0,
            security);
        Volatile.Write(ref _pipeCreated, 1);
        return pipe;
    }

    private async Task ServePipeAsync(NamedPipeServerStream pipe)
    {
        var handle = pipe.SafePipeHandle;
        var added = false;
        int pid;
        bool isRemote;
        handle.DangerousAddRef(ref added);
        try
        {
            if (!ProcessInspector.TryGetNamedPipeClient(handle.DangerousGetHandle(), out pid, out isRemote))
            {
                return;
            }
        }
        finally
        {
            if (added)
            {
                handle.DangerousRelease();
            }
        }

        if (isRemote)
        {
            _logger?.Warn($"event=deploy.credential.denied reason={GitCredentialDenial.User} pid={pid} remote=true");
            return;
        }

        // The pipe ACL admits only the current user, so there is no separate user check.
        await ServeAsync(pipe, pid, peerUid: null).ConfigureAwait(false);
    }

    private static async Task PauseAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(50, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void Track(Task connection)
    {
        _connections[connection] = 0;
        _ = connection.ContinueWith(
            static (task, state) => ((ConcurrentDictionary<Task, byte>)state!).TryRemove(task, out _),
            _connections,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private bool TryTakeSlot()
    {
        if (Interlocked.Increment(ref _active) <= MaxConcurrentConnections)
        {
            return true;
        }

        ReleaseSlot();
        return false;
    }

    private void ReleaseSlot() => Interlocked.Decrement(ref _active);

    private async Task ServeAsync(Stream stream, int peerPid, uint? peerUid)
    {
        // Pin the connecting process before waiting for its request (pid reuse, plan 3.4).
        var peerKnown = _policy.TryCaptureIdentity(peerPid, out var peer);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
        timeout.CancelAfter(ConnectionTimeout);
        var line = await GitCredentialProtocol.ReadLineAsync(stream, timeout.Token).ConfigureAwait(false);
        var request = line is null ? null : GitCredentialProtocol.ParseRequest(line);
        var decision = request is null ? GitCredentialDecision.Deny(GitCredentialDenial.Version)
            : !peerKnown ? GitCredentialDecision.Deny(GitCredentialDenial.Executable, $"{peerPid}")
            : _policy.Evaluate(peer, peerUid, request);

        // A grant decided while stopping is withheld: the deploy no longer waits for it.
        var denial = decision.Granted && _stop.IsCancellationRequested ? GitCredentialDenial.Unregistered : decision.Denial;
        GitCredentialResponse response;
        if (denial is null)
        {
            // A refusal before this grant (e.g. the helper's retry while git was not yet
            // registered) is not why a later git failure happened.
            Volatile.Write(ref _lastDenial, null);
            response = new GitCredentialResponse(GitCredentialProtocol.Version, GitCredentialProtocol.Username, _token);
        }
        else
        {
            Volatile.Write(ref _lastDenial, denial);
            if (denial != GitCredentialDenial.Unregistered)
            {
                _logger?.Warn($"event=deploy.credential.denied reason={denial} pid={peerPid} chain={decision.Chain ?? "-"}");
            }

            response = new GitCredentialResponse(GitCredentialProtocol.Version, Deny: denial);
        }

        await stream.WriteAsync(GitCredentialProtocol.Serialize(response), timeout.Token).ConfigureAwait(false);
        await stream.FlushAsync(timeout.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// The pid and user that connected, recorded by the kernel at connect(): passing the
    /// descriptor to another process does not change them.
    /// </summary>
    private static (int Pid, uint? Uid) ReadSocketPeer(Socket client)
    {
        if (OperatingSystem.IsMacOS())
        {
            // audit_token_t: val[1] = euid, val[5] = pid.
            Span<byte> token = stackalloc byte[AuditTokenSize];
            var length = client.GetRawSocketOption(SolLocal, LocalPeerToken, token);
            if (length != AuditTokenSize)
            {
                throw new SocketException((int)SocketError.ProtocolOption);
            }

            return (BinaryPrimitives.ReadInt32LittleEndian(token[20..]), BinaryPrimitives.ReadUInt32LittleEndian(token[4..]));
        }

        // struct ucred { pid_t pid; uid_t uid; gid_t gid; }
        Span<byte> cred = stackalloc byte[UcredSize];
        if (client.GetRawSocketOption(SolSocket, SoPeerCred, cred) != UcredSize)
        {
            throw new SocketException((int)SocketError.ProtocolOption);
        }

        return (BinaryPrimitives.ReadInt32LittleEndian(cred), BinaryPrimitives.ReadUInt32LittleEndian(cred[4..]));
    }

    [UnsupportedOSPlatform("windows")]
    private static string CreatePrivateDirectory()
    {
        var limit = OperatingSystem.IsMacOS() ? MacSocketPathLimit : LinuxSocketPathLimit;
        foreach (var root in new[] { Path.GetTempPath(), "/tmp" })
        {
            var directory = Path.Combine(root, $"bukit-cred-{RandomHex(6)}");
            if (System.Text.Encoding.UTF8.GetByteCount(Path.Combine(directory, "c.sock")) > limit)
            {
                continue;
            }

            // A fresh random name; refuse anything that already exists, links, or is not 0700.
            // A fresh random name. Another user's directory at this path is either not 0700
            // (refused here) or not writable by us (bind fails), so a usable one is ours.
            if (System.IO.Directory.Exists(directory) || File.Exists(directory))
            {
                throw new IOException("Credential broker directory already exists.");
            }

            const UnixFileMode Private = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
            var info = System.IO.Directory.CreateDirectory(directory, Private);
            if (info.LinkTarget is not null || File.GetUnixFileMode(directory) != Private)
            {
                throw new IOException("Credential broker directory is not private.");
            }

            return directory;
        }

        throw new IOException("No temporary directory is short enough for a Unix domain socket path.");
    }

    private static void TryDeleteDirectory(string directory, ILogger? logger)
    {
        try
        {
            System.IO.Directory.Delete(directory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger?.Warn($"event=deploy.credential.broker.cleanup.failed reason={ex.GetType().Name}");
        }
    }

    private static string RandomHex(int bytes) => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(bytes));
}
