using Bukit.Shared.Processes;

namespace Bukit.Cli.Deploy;

/// <summary>Process queries the peer policy needs; replaced in tests.</summary>
internal interface IGitCredentialProcessQuery
{
    bool TryGetIdentity(int pid, out ProcessIdentity identity);

    string? TryGetExecutablePath(int pid);

    IReadOnlyList<string>? TryGetArguments(int pid);

    string? TryRealPath(string path);
}

internal sealed class SystemGitCredentialProcessQuery : IGitCredentialProcessQuery
{
    internal static readonly SystemGitCredentialProcessQuery Instance = new();

    public bool TryGetIdentity(int pid, out ProcessIdentity identity) => ProcessInspector.TryGetIdentity(pid, out identity);

    public string? TryGetExecutablePath(int pid) => ProcessInspector.TryGetExecutablePath(pid);

    public IReadOnlyList<string>? TryGetArguments(int pid) => ProcessInspector.TryGetArguments(pid);

    public string? TryRealPath(string path) => ProcessInspector.TryRealPath(path);
}

/// <summary>
/// Who may receive the token. <see cref="SelfExecutable"/> is the canonical path of the
/// running Bukit (or of <c>dotnet</c>, with <see cref="SelfAssembly"/> the canonical
/// <c>bukit.dll</c>); <see cref="RemoteHelpers"/> and <see cref="Shells"/> are canonical
/// paths of git's HTTP remote helper and of the shell git runs the credential helper with.
/// </summary>
internal sealed record GitCredentialPeerExpectations(
    string SelfExecutable,
    string? SelfAssembly,
    IReadOnlySet<string> RemoteHelpers,
    IReadOnlySet<string> Shells);

/// <summary>
/// Where the token may be sent. Production is fixed to https://github.com; tests inject a
/// local target through <c>InternalsVisibleTo</c>, never through configuration.
/// </summary>
internal sealed record GitCredentialTarget(string Protocol, string Host)
{
    internal static readonly GitCredentialTarget GitHub = new("https", "github.com");
}

/// <summary>
/// Decides whether a connected peer gets the token (plan section 3.4): the peer must be this
/// Bukit run as <c>__git-credential</c>, a child of git's HTTP remote helper (directly or
/// through one shell), and a descendant of a git process registered by the deploy, with
/// start times proving no pid on the chain was reused.
/// </summary>
internal sealed class GitCredentialPeerPolicy
{
    internal const int DefaultMaxDepth = 32;
    internal const int DefaultMaxGrants = 3;
    internal const string HelperCommand = "__git-credential";

    private readonly GitCredentialPeerExpectations _expectations;
    private readonly GitCredentialTarget _target;
    private readonly IGitCredentialProcessQuery _query;
    private readonly uint? _currentUserId;
    private readonly int _maxDepth;
    private readonly int _maxGrants;
    private readonly StringComparer _paths = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private readonly Dictionary<int, Root> _roots = [];
    private readonly Lock _gate = new();

    internal GitCredentialPeerPolicy(
        GitCredentialPeerExpectations expectations,
        GitCredentialTarget target,
        IGitCredentialProcessQuery query,
        uint? currentUserId,
        int maxDepth = DefaultMaxDepth,
        int maxGrants = DefaultMaxGrants)
    {
        ArgumentNullException.ThrowIfNull(expectations);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDepth, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxGrants, 1);
        _expectations = expectations;
        _target = target;
        _query = query;
        _currentUserId = currentUserId;
        _maxDepth = maxDepth;
        _maxGrants = maxGrants;
    }

    /// <summary>
    /// Records a started git process as a trusted root until the returned handle is disposed.
    /// Returns null when the process is already gone; its helpers are then refused.
    /// </summary>
    internal IDisposable? Register(int gitPid)
    {
        if (!_query.TryGetIdentity(gitPid, out var identity))
        {
            return null;
        }

        lock (_gate)
        {
            _roots[gitPid] = new Root(identity.StartTime);
        }

        return new Registration(this, gitPid, identity.StartTime);
    }

    /// <summary>
    /// The identity of a peer pid read right after accept. The kernel records the pid at
    /// connect(); the start time pins it to that process, so a pid reused before the request
    /// arrives no longer matches.
    /// </summary>
    internal bool TryCaptureIdentity(int peerPid, out ProcessIdentity identity) => _query.TryGetIdentity(peerPid, out identity);

    /// <summary>The refusal reason, or null when the peer gets the token (one grant is consumed).</summary>
    internal GitCredentialDecision Evaluate(ProcessIdentity peerAtConnect, uint? peerUserId, GitCredentialRequest request)
    {
        var peerPid = peerAtConnect.Pid;
        ArgumentNullException.ThrowIfNull(request);
        if (request.V != GitCredentialProtocol.Version)
        {
            return GitCredentialDecision.Deny(GitCredentialDenial.Version);
        }

        if (_currentUserId is { } uid && peerUserId != uid)
        {
            return GitCredentialDecision.Deny(GitCredentialDenial.User);
        }

        lock (_gate)
        {
            if (_roots.Count == 0)
            {
                return GitCredentialDecision.Deny(GitCredentialDenial.Unregistered);
            }
        }

        var chain = new List<string>();
        if (!IsSameProcess(peerAtConnect, out var peer) || !IsSelfHelper(peerPid) || !HasRemoteHelperParent(peer, chain))
        {
            return GitCredentialDecision.Deny(GitCredentialDenial.Executable, Describe(peerPid, chain));
        }

        if (!TryFindRoot(peer, chain, out var rootPid))
        {
            return GitCredentialDecision.Deny(GitCredentialDenial.Peer, Describe(peerPid, chain));
        }

        if (!string.Equals(request.Protocol, _target.Protocol, StringComparison.Ordinal))
        {
            return GitCredentialDecision.Deny(GitCredentialDenial.Protocol);
        }

        if (!string.Equals(request.Host, _target.Host, StringComparison.OrdinalIgnoreCase))
        {
            return GitCredentialDecision.Deny(GitCredentialDenial.Host);
        }

        // Checked again last: the peer must still be the process that connected.
        if (!IsSameProcess(peerAtConnect, out _))
        {
            return GitCredentialDecision.Deny(GitCredentialDenial.Executable, Describe(peerPid, chain));
        }

        lock (_gate)
        {
            if (!_roots.TryGetValue(rootPid, out var root))
            {
                return GitCredentialDecision.Deny(GitCredentialDenial.Unregistered);
            }

            if (root.Grants >= _maxGrants)
            {
                return GitCredentialDecision.Deny(GitCredentialDenial.Limit);
            }

            root.Grants++;
        }

        return GitCredentialDecision.Grant;
    }

    private bool IsSameProcess(ProcessIdentity expected, out ProcessIdentity current) =>
        _query.TryGetIdentity(expected.Pid, out current) && current.StartTime == expected.StartTime;

    private bool IsSelfHelper(int pid)
    {
        if (!SamePath(_query.TryGetExecutablePath(pid), _expectations.SelfExecutable))
        {
            return false;
        }

        // Under `dotnet bukit.dll` the image is dotnet: the dll and the subcommand must match too.
        if (_expectations.SelfAssembly is null)
        {
            return true;
        }

        var args = _query.TryGetArguments(pid);
        return args is { Count: >= 3 } &&
               SamePath(_query.TryRealPath(args[1]), _expectations.SelfAssembly) &&
               string.Equals(args[2], HelperCommand, StringComparison.Ordinal);
    }

    private bool HasRemoteHelperParent(ProcessIdentity peer, List<string> chain)
    {
        var parentExe = _query.TryGetExecutablePath(peer.ParentPid);
        chain.Add($"{peer.ParentPid}:{Path.GetFileName(parentExe)}");
        if (IsIn(parentExe, _expectations.RemoteHelpers))
        {
            return true;
        }

        // git always runs the helper through `sh -c`; the shell usually execs it, but may stay.
        if (!IsIn(parentExe, _expectations.Shells) || !_query.TryGetIdentity(peer.ParentPid, out var shell))
        {
            return false;
        }

        var grandparentExe = _query.TryGetExecutablePath(shell.ParentPid);
        chain.Add($"{shell.ParentPid}:{Path.GetFileName(grandparentExe)}");
        return IsIn(grandparentExe, _expectations.RemoteHelpers);
    }

    private bool TryFindRoot(ProcessIdentity peer, List<string> chain, out int rootPid)
    {
        rootPid = 0;
        var logged = chain.Count;
        var current = peer;
        for (var depth = 0; depth < _maxDepth; depth++)
        {
            var parentPid = current.ParentPid;
            if (parentPid <= 0 || parentPid == current.Pid || !_query.TryGetIdentity(parentPid, out var parent))
            {
                return false;
            }

            // A parent cannot start after its child: a later start means the pid was reused.
            if (parent.StartTime > current.StartTime)
            {
                return false;
            }

            Root? root;
            lock (_gate)
            {
                _roots.TryGetValue(parentPid, out root);
            }

            if (root is not null && root.StartTime == parent.StartTime)
            {
                rootPid = parentPid;
                return true;
            }

            if (depth >= logged)
            {
                chain.Add($"{parentPid}:{Path.GetFileName(_query.TryGetExecutablePath(parentPid))}");
            }

            current = parent;
        }

        return false;
    }

    private bool IsIn(string? path, IReadOnlySet<string> candidates)
    {
        if (path is null)
        {
            return false;
        }

        foreach (var candidate in candidates)
        {
            if (_paths.Equals(path, candidate))
            {
                return true;
            }
        }

        return false;
    }

    private bool SamePath(string? path, string expected) => path is not null && _paths.Equals(path, expected);

    private static string Describe(int peerPid, List<string> chain) =>
        chain.Count == 0 ? $"{peerPid}" : $"{peerPid}<-{string.Join("<-", chain)}";

    private void Unregister(int gitPid, long startTime)
    {
        lock (_gate)
        {
            if (_roots.TryGetValue(gitPid, out var root) && root.StartTime == startTime)
            {
                _roots.Remove(gitPid);
            }
        }
    }

    private sealed class Root(long startTime)
    {
        public long StartTime { get; } = startTime;

        public int Grants { get; set; }
    }

    private sealed class Registration(GitCredentialPeerPolicy owner, int pid, long startTime) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                owner.Unregister(pid, startTime);
            }
        }
    }
}

internal readonly record struct GitCredentialDecision(string? Denial, string? Chain)
{
    internal static GitCredentialDecision Grant => default;

    internal bool Granted => Denial is null;

    internal static GitCredentialDecision Deny(string reason, string? chain = null) => new(reason, chain);
}
