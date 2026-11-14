using System.Net.Sockets;
using System.Text;
using Bukit.Cli.Deploy;
using Bukit.Shared.Processes;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class GitCredentialBrokerTests
{
    private const string Bukit = "/opt/bukit/bukit";
    private const string RemoteHttp = "/usr/libexec/git-core/git-remote-http";
    private const string Sh = "/bin/sh";
    private const string Git = "/usr/bin/git";
    private const uint Uid = 501;

    private static readonly GitCredentialRequest GitHubRequest = new(1, "https", "github.com");

    // ---- protocol ----

    [Fact]
    public void Protocol_RoundTripsRequestAndResponseAsSingleLines()
    {
        var request = GitCredentialProtocol.Serialize(GitHubRequest);
        Assert.Equal("{\"v\":1,\"protocol\":\"https\",\"host\":\"github.com\"}\n", Encoding.UTF8.GetString(request));
        Assert.Equal(GitHubRequest, GitCredentialProtocol.ParseRequest(request.AsSpan(0, request.Length - 1)));

        var deny = GitCredentialProtocol.Serialize(new GitCredentialResponse(1, Deny: GitCredentialDenial.Peer));
        Assert.Equal("{\"v\":1,\"deny\":\"peer\"}\n", Encoding.UTF8.GetString(deny));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("")]
    public void Protocol_RejectsMalformedRequests(string line)
    {
        Assert.Null(GitCredentialProtocol.ParseRequest(Encoding.UTF8.GetBytes(line)));
    }

    [Fact]
    public async Task Protocol_ReadLineRejectsOversizedAndUnterminatedInput()
    {
        var oversized = new MemoryStream(Encoding.UTF8.GetBytes(new string('a', GitCredentialProtocol.MaxLineBytes + 10) + "\n"));
        Assert.Null(await GitCredentialProtocol.ReadLineAsync(oversized, CancellationToken.None));

        var unterminated = new MemoryStream(Encoding.UTF8.GetBytes("{\"v\":1}"));
        Assert.Null(await GitCredentialProtocol.ReadLineAsync(unterminated, CancellationToken.None));

        var ok = new MemoryStream(Encoding.UTF8.GetBytes("abc\nrest"));
        Assert.Equal("abc", Encoding.UTF8.GetString((await GitCredentialProtocol.ReadLineAsync(ok, CancellationToken.None))!));
    }

    // ---- policy: the normal chain is helper(30) <- remote-http(20) <- git(10) ----

    [Fact]
    public void Policy_GrantsHelperUnderRegisteredGit()
    {
        var (policy, _) = CreateStandardChain();

        Assert.True(policy.Evaluate(Capture(policy, 30), Uid, GitHubRequest).Granted);
    }

    [Fact]
    public void Policy_GrantsHelperBehindShellThatDidNotExec()
    {
        var query = new FakeQuery()
            .Add(10, 1, 100, Git)
            .Add(20, 10, 110, RemoteHttp)
            .Add(25, 20, 115, Sh)
            .Add(30, 25, 120, Bukit);
        var policy = CreatePolicy(query);
        Assert.NotNull(policy.Register(10));

        Assert.True(policy.Evaluate(Capture(policy, 30), Uid, GitHubRequest).Granted);
    }

    [Fact]
    public void Policy_DeniesWhenNothingIsRegistered()
    {
        var (policy, _) = CreateStandardChainWithQuery();
        Assert.Equal(GitCredentialDenial.Unregistered, policy.Evaluate(Capture(policy, 30), Uid, GitHubRequest).Denial);
        policy.Register(10)!.Dispose();

        Assert.Equal(GitCredentialDenial.Unregistered, policy.Evaluate(Capture(policy, 30), Uid, GitHubRequest).Denial);
    }

    [Fact]
    public void Policy_DeniesWrongVersionAndWrongUser()
    {
        var (policy, _) = CreateStandardChain();

        Assert.Equal(GitCredentialDenial.Version, policy.Evaluate(Capture(policy, 30), Uid, GitHubRequest with { V = 2 }).Denial);
        Assert.Equal(GitCredentialDenial.User, policy.Evaluate(Capture(policy, 30), Uid + 1, GitHubRequest).Denial);
        Assert.Equal(GitCredentialDenial.User, policy.Evaluate(Capture(policy, 30), null, GitHubRequest).Denial);
    }

    [Fact]
    public void Policy_DeniesPeerThatIsNotBukit()
    {
        var (policy, query) = CreateStandardChain();
        query.Add(31, 20, 121, "/tmp/evil");

        Assert.Equal(GitCredentialDenial.Executable, policy.Evaluate(Capture(policy, 31), Uid, GitHubRequest).Denial);
    }

    [Fact]
    public void Policy_DeniesBukitStartedByGitHookInsteadOfRemoteHelper()
    {
        // A pre-push hook (child of git, not of git-remote-http) running bukit __git-credential.
        var (policy, query) = CreateStandardChain();
        query.Add(40, 10, 130, Sh).Add(41, 40, 131, Bukit);

        var decision = policy.Evaluate(Capture(policy, 41), Uid, GitHubRequest);

        Assert.Equal(GitCredentialDenial.Executable, decision.Denial);
        Assert.Contains("40:sh", decision.Chain, StringComparison.Ordinal);
    }

    [Fact]
    public void Policy_DeniesRemoteHelperCopyAtAnotherPath()
    {
        // Copying /bin/sh to /tmp/git-remote-https must not pass: paths are compared in full.
        var (policy, query) = CreateStandardChain();
        query.Add(50, 10, 140, "/tmp/git-remote-https").Add(51, 50, 141, Bukit);

        Assert.Equal(GitCredentialDenial.Executable, policy.Evaluate(Capture(policy, 51), Uid, GitHubRequest).Denial);
    }

    [Fact]
    public void Policy_DeniesHelperNotDescendedFromRegisteredGit()
    {
        var (policy, query) = CreateStandardChain();
        query.Add(60, 1, 90, Git).Add(61, 60, 150, RemoteHttp).Add(62, 61, 151, Bukit);

        Assert.Equal(GitCredentialDenial.Peer, policy.Evaluate(Capture(policy, 62), Uid, GitHubRequest).Denial);
    }

    [Fact]
    public void Policy_DeniesWhenRootStartTimeChangedAfterRegistration()
    {
        var (policy, query) = CreateStandardChain();
        query.Add(10, 1, 105, Git); // pid 10 exited and was reused by another git

        Assert.Equal(GitCredentialDenial.Peer, policy.Evaluate(Capture(policy, 30), Uid, GitHubRequest).Denial);
    }

    [Fact]
    public void Policy_DeniesParentThatStartedAfterChild()
    {
        var (policy, query) = CreateStandardChain();
        query.Add(20, 10, 125, RemoteHttp); // parent pid reused after the helper started

        Assert.Equal(GitCredentialDenial.Peer, policy.Evaluate(Capture(policy, 30), Uid, GitHubRequest).Denial);
    }

    [Fact]
    public void Policy_DeniesChainDeeperThanMaxDepth()
    {
        var query = new FakeQuery().Add(10, 1, 100, Git);
        var parent = 10;
        for (var pid = 11; pid < 15; pid++)
        {
            query.Add(pid, parent, 100 + pid, "/usr/bin/wrapper");
            parent = pid;
        }

        query.Add(20, parent, 120, RemoteHttp).Add(30, 20, 130, Bukit);
        var shallow = CreatePolicy(query, maxDepth: 3);
        Assert.NotNull(shallow.Register(10));
        Assert.Equal(GitCredentialDenial.Peer, shallow.Evaluate(Capture(shallow, 30), Uid, GitHubRequest).Denial);

        var deep = CreatePolicy(query);
        Assert.NotNull(deep.Register(10));
        Assert.True(deep.Evaluate(Capture(deep, 30), Uid, GitHubRequest).Granted);
    }

    [Theory]
    [InlineData("http", "github.com", GitCredentialDenial.Protocol)]
    [InlineData("https", "github.com.evil.example", GitCredentialDenial.Host)]
    [InlineData("https", "github.com:443", GitCredentialDenial.Host)]
    [InlineData("https", "user@github.com", GitCredentialDenial.Host)]
    [InlineData("https", null, GitCredentialDenial.Host)]
    public void Policy_DeniesOtherTargets(string protocol, string? host, string denial)
    {
        var (policy, _) = CreateStandardChain();

        Assert.Equal(denial, policy.Evaluate(Capture(policy, 30), Uid, new GitCredentialRequest(1, protocol, host)).Denial);
    }

    [Fact]
    public void Policy_AcceptsHostCaseInsensitively()
    {
        var (policy, _) = CreateStandardChain();

        Assert.True(policy.Evaluate(Capture(policy, 30), Uid, GitHubRequest with { Host = "GitHub.com" }).Granted);
    }

    [Fact]
    public void Policy_LimitsGrantsPerRegisteredGit()
    {
        var (policy, _) = CreateStandardChain();

        for (var i = 0; i < GitCredentialPeerPolicy.DefaultMaxGrants; i++)
        {
            Assert.True(policy.Evaluate(Capture(policy, 30), Uid, GitHubRequest).Granted);
        }

        Assert.Equal(GitCredentialDenial.Limit, policy.Evaluate(Capture(policy, 30), Uid, GitHubRequest).Denial);
    }

    [Fact]
    public async Task Policy_LimitHoldsUnderConcurrentRequests()
    {
        var (policy, _) = CreateStandardChain();

        var decisions = await Task.WhenAll(Enumerable.Range(0, 50)
            .Select(_ => Task.Run(() => policy.Evaluate(Capture(policy, 30), Uid, GitHubRequest))));

        Assert.Equal(GitCredentialPeerPolicy.DefaultMaxGrants, decisions.Count(d => d.Granted));
    }

    [Fact]
    public void Policy_RegisterReturnsNullForExitedGit()
    {
        var policy = CreatePolicy(new FakeQuery());

        Assert.Null(policy.Register(999));
    }

    [Fact]
    public void Policy_DotnetHostRequiresSameAssemblyAndSubcommand()
    {
        const string dotnet = "/usr/share/dotnet/dotnet";
        const string dll = "/opt/bukit/bukit.dll";
        var query = new FakeQuery()
            .Add(10, 1, 100, Git)
            .Add(20, 10, 110, RemoteHttp)
            .Add(30, 20, 120, dotnet, dotnet, dll, GitCredentialPeerPolicy.HelperCommand, "/tmp/s", "get")
            .Add(31, 20, 121, dotnet, dotnet, "/tmp/other.dll", GitCredentialPeerPolicy.HelperCommand, "/tmp/s", "get")
            .Add(32, 20, 122, dotnet, dotnet, dll, "build");
        var policy = new GitCredentialPeerPolicy(
            new GitCredentialPeerExpectations(dotnet, dll, new HashSet<string> { RemoteHttp }, new HashSet<string> { Sh }),
            GitCredentialTarget.GitHub,
            query,
            Uid);
        Assert.NotNull(policy.Register(10));

        Assert.True(policy.Evaluate(Capture(policy, 30), Uid, GitHubRequest).Granted);
        Assert.Equal(GitCredentialDenial.Executable, policy.Evaluate(Capture(policy, 31), Uid, GitHubRequest).Denial);
        Assert.Equal(GitCredentialDenial.Executable, policy.Evaluate(Capture(policy, 32), Uid, GitHubRequest).Denial);
    }

    // ---- broker over a real endpoint ----

    [Fact]
    public async Task Broker_ReportsCurrentProcessAsPeerAndDeniesIt()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var query = new RecordingQuery();
        Assert.True(ProcessInspector.TryGetCurrentUserId(out var uid));
        var policy = new GitCredentialPeerPolicy(
            new GitCredentialPeerExpectations(Bukit, null, new HashSet<string> { RemoteHttp }, new HashSet<string> { Sh }),
            GitCredentialTarget.GitHub,
            query,
            uid);
        Assert.NotNull(policy.Register(Environment.ProcessId));

        string directory;
        await using (var broker = GitCredentialBroker.Start("secret-token", policy))
        {
            directory = broker.Directory!;
            Assert.Equal(
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                File.GetUnixFileMode(directory));

            var response = await ExchangeAsync(broker.Endpoint, GitCredentialProtocol.Serialize(GitHubRequest));

            Assert.Equal(GitCredentialDenial.Executable, response!.Deny);
            Assert.Null(response.Password);
            Assert.Contains(Environment.ProcessId, query.ExecutableQueries);
        }

        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public async Task Broker_DeniesMalformedRequestWithoutToken()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var (policy, _) = CreateStandardChain();
        await using var broker = GitCredentialBroker.Start("secret-token", policy);

        var response = await ExchangeAsync(broker.Endpoint, Encoding.UTF8.GetBytes("garbage\n"));

        Assert.Equal(GitCredentialDenial.Version, response!.Deny);
        Assert.Null(response.Password);
    }

    [Fact]
    public async Task Broker_StopsPromptlyWithOpenIdleConnection()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var (policy, _) = CreateStandardChain();
        var broker = GitCredentialBroker.Start("secret-token", policy);
        using var idle = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await idle.ConnectAsync(new UnixDomainSocketEndPoint(broker.Endpoint));

        var started = System.Diagnostics.Stopwatch.StartNew();
        await broker.DisposeAsync();

        Assert.True(started.Elapsed < TimeSpan.FromSeconds(5));
        Assert.False(Directory.Exists(broker.Directory));
    }

    private static ProcessIdentity Capture(GitCredentialPeerPolicy policy, int pid)
    {
        Assert.True(policy.TryCaptureIdentity(pid, out var identity));
        return identity;
    }

    [Fact]
    public void Policy_DeniesPeerWhosePidWasReusedAfterConnect()
    {
        var (policy, query) = CreateStandardChain();
        var atConnect = Capture(policy, 30);
        query.Add(30, 20, 121, Bukit); // the connecting process exited; a real helper got its pid

        Assert.Equal(GitCredentialDenial.Executable, policy.Evaluate(atConnect, Uid, GitHubRequest).Denial);
    }

    [Fact]
    public void Policy_DeniesShellBranchWhenShellStartedAfterHelper()
    {
        var query = new FakeQuery()
            .Add(10, 1, 100, Git)
            .Add(20, 10, 110, RemoteHttp)
            .Add(25, 20, 125, Sh)
            .Add(30, 25, 120, Bukit);
        var policy = CreatePolicy(query);
        Assert.NotNull(policy.Register(10));

        Assert.Equal(GitCredentialDenial.Peer, policy.Evaluate(Capture(policy, 30), Uid, GitHubRequest).Denial);
    }

    private static async Task<GitCredentialResponse?> ExchangeAsync(string socketPath, byte[] request)
    {
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath));
        await using var stream = new NetworkStream(socket);
        await stream.WriteAsync(request);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var line = await GitCredentialProtocol.ReadLineAsync(stream, cts.Token);
        return line is null ? null : GitCredentialProtocol.ParseResponse(line);
    }

    private static (GitCredentialPeerPolicy Policy, FakeQuery Query) CreateStandardChainWithQuery()
    {
        var query = new FakeQuery()
            .Add(10, 1, 100, Git)
            .Add(20, 10, 110, RemoteHttp)
            .Add(30, 20, 120, Bukit);
        return (CreatePolicy(query), query);
    }

    private static (GitCredentialPeerPolicy Policy, FakeQuery Query) CreateStandardChain()
    {
        var (policy, query) = CreateStandardChainWithQuery();
        Assert.NotNull(policy.Register(10));
        return (policy, query);
    }

    private static GitCredentialPeerPolicy CreatePolicy(FakeQuery query, int maxDepth = GitCredentialPeerPolicy.DefaultMaxDepth) =>
        new(
            new GitCredentialPeerExpectations(Bukit, null, new HashSet<string> { RemoteHttp }, new HashSet<string> { Sh }),
            GitCredentialTarget.GitHub,
            query,
            Uid,
            maxDepth);

    private sealed class FakeQuery : IGitCredentialProcessQuery
    {
        private readonly Dictionary<int, (ProcessIdentity Identity, string Exe, string[] Args)> _processes = [];

        public FakeQuery Add(int pid, int ppid, long start, string exe, params string[] args)
        {
            _processes[pid] = (new ProcessIdentity(pid, ppid, start), exe, args);
            return this;
        }

        public bool TryGetIdentity(int pid, out ProcessIdentity identity)
        {
            var found = _processes.TryGetValue(pid, out var process);
            identity = process.Identity;
            return found;
        }

        public string? TryGetExecutablePath(int pid) => _processes.TryGetValue(pid, out var p) ? p.Exe : null;

        public IReadOnlyList<string>? TryGetArguments(int pid) => _processes.TryGetValue(pid, out var p) ? p.Args : null;

        public string? TryRealPath(string path) => path;
    }

    private sealed class RecordingQuery : IGitCredentialProcessQuery
    {
        public List<int> ExecutableQueries { get; } = [];

        public bool TryGetIdentity(int pid, out ProcessIdentity identity) => ProcessInspector.TryGetIdentity(pid, out identity);

        public string? TryGetExecutablePath(int pid)
        {
            lock (ExecutableQueries)
            {
                ExecutableQueries.Add(pid);
            }

            return ProcessInspector.TryGetExecutablePath(pid);
        }

        public IReadOnlyList<string>? TryGetArguments(int pid) => ProcessInspector.TryGetArguments(pid);

        public string? TryRealPath(string path) => ProcessInspector.TryRealPath(path);
    }
}
