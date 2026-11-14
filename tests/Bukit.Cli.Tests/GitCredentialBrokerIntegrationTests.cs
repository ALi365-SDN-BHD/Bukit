using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Bukit.Cli.Deploy;
using Bukit.Shared;
using Bukit.Shared.Processes;
using Xunit;

namespace Bukit.Cli.Tests;

/// <summary>
/// Plan 3.7 integration tests 5-11 and 13: real git, the real broker and the real
/// <c>__git-credential</c> helper against a local <c>git http-backend</c> behind Basic auth
/// (localhost only). The transport policy is replaced only through
/// <see cref="GitHubPagesDeployProvider.TransportPolicyOverride"/>; the peer checks are the
/// production ones. A <c>/bin/sh</c> wrapper named <c>git</c> records each call's arguments and
/// environment, maps the deploy-private remote name to the local URL and then <c>exec</c>s the
/// real git, so the process chain the broker checks is still the real one. Unix only: the
/// wrapper is a shell script; Windows needs a native wrapper (plan 3.7 test 12).
/// </summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class GitCredentialBrokerIntegrationTests
{
    private const string Token = "ghp_test_broker_integration_good_0001";
    private const string WrongToken = "ghp_test_broker_integration_wrong_0002";
    private const string TokenEnv = "BUKIT_IT_BROKER_DEPLOY_TOKEN";

    // ---- test 10: real authenticated push ----

    [UnixFact]
    public async Task RealPush_CorrectTokenPushesThroughBroker()
    {
        await using var scope = await BrokerDeployScope.CreateAsync();

        var result = await scope.DeployAsync();

        Assert.True(result.Success, result.Error);
        Assert.Equal("new page", await scope.ReadRemoteFileAsync("index.html"));
        Assert.True(scope.Server.Unauthorized >= 1, "git must first be challenged with 401.");
        Assert.True(scope.Server.Authorized >= 3, "ls-remote, fetch and push must all authenticate.");
        Assert.Empty(scope.Server.WrongPasswords);
    }

    [UnixFact]
    public async Task RealPush_WrongTokenReportsAuthenticationFailure()
    {
        await using var scope = await BrokerDeployScope.CreateAsync(token: WrongToken);

        var result = await scope.DeployAsync();

        Assert.False(result.Success);
        Assert.Contains("Authentication failed", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(WrongToken, result.Error, StringComparison.Ordinal);
        Assert.Contains(WrongToken, scope.Server.WrongPasswords);
        Assert.Equal(0, scope.Server.Authorized);
        Assert.Equal("old page", await scope.ReadRemoteFileAsync("old.html"));
    }

    /// <summary>
    /// The local server answers 401 at once; registration of the git pid is held back until the
    /// helper has been refused as "unregistered" and has connected again (plan 3.4).
    /// </summary>
    [UnixFact]
    public async Task RealPush_HelperRetryCoversRegistrationRace()
    {
        await using var scope = await BrokerDeployScope.CreateAsync();
        scope.Query.HoldFirstRegistration();

        var result = await scope.DeployAsync();

        Assert.True(result.Success, result.Error);
        Assert.True(scope.Query.RaceObserved, "The helper must have connected twice before git was registered.");
        Assert.Equal("new page", await scope.ReadRemoteFileAsync("index.html"));
    }

    // ---- test 6: the real process chain ----

    /// <summary>
    /// Observed on macOS 15 (Apple Git 2.39.5, Bukit run as <c>dotnet bukit.dll</c>):
    /// <code>
    /// /usr/local/share/dotnet/dotnet (helper)
    ///   &lt;- /Library/Developer/CommandLineTools/usr/libexec/git-core/git-remote-http
    ///   &lt;- /Library/Developer/CommandLineTools/usr/bin/git
    ///   &lt;- registered root: /usr/bin/git (the xcrun trampoline; here the wrapper pid, first seen as /bin/sh before its exec)
    /// </code>
    /// git's <c>sh -c</c> execs the helper, so no shell stays on the chain; the trampoline adds
    /// one level. Three levels above the helper, far below
    /// <see cref="GitCredentialPeerPolicy.DefaultMaxDepth"/> (32).
    /// </summary>
    [UnixFact]
    public async Task RealPush_RecordsHelperToRootChainWithinMaxDepth()
    {
        await using var scope = await BrokerDeployScope.CreateAsync();

        var result = await scope.DeployAsync();

        Assert.True(result.Success, result.Error);
        var chain = scope.Query.HelperChain();
        var described = string.Join(" <- ", chain);
        Assert.True(chain.Count >= 3, described);
        Assert.Equal(scope.SelfExecutable, chain[0]);
        Assert.True(Path.GetFileName(chain[1]) is "git-remote-http" or "git-remote-https" or "sh" or "bash" or "dash", described);
        Assert.Contains(chain, exe => Path.GetFileName(exe) is "git-remote-http" or "git-remote-https");
        // The root is the pid the deploy registered: the git wrapper, recorded as sh or git.
        Assert.True(Path.GetFileName(chain[^1]) is "git" or "sh", described);
        Assert.All(chain[1..^1], exe => Assert.True(
            Path.GetFileName(exe) is "git-remote-http" or "git-remote-https" or "sh" or "bash" or "dash" or "git",
            described));
        // Unix: helper <- [sh <-] git-remote-http(s) <- [git <-] root. Keep a wide margin to MaxDepth.
        Assert.True(chain.Count - 1 <= 5, described);
        Assert.True((chain.Count - 1) * 4 < GitCredentialPeerPolicy.DefaultMaxDepth, described);
    }

    // ---- test 5: attacker-controlled git config ----

    [UnixFact]
    public async Task AttackerGitConfig_NeverRunsAndNeverChangesAuthenticatedCalls()
    {
        await using var scope = await BrokerDeployScope.CreateAsync();
        scope.WriteAttackerConfig(rewriteGitHub: false);

        var result = await scope.DeployAsync();

        // The global http.proxy points at a closed port: success proves it was not read either.
        Assert.True(result.Success, result.Error);
        Assert.Equal("new page", await scope.ReadRemoteFileAsync("index.html"));
        Assert.True(scope.AttackerMarkers().Length == 0, scope.DescribeMarkers());
        Assert.DoesNotContain(Token, scope.ReadAllRecordedText(), StringComparison.Ordinal);
    }

    [UnixFact]
    public async Task AttackerGitConfig_RewriteOfGitHubTargetFailsClosedBeforeAnyTransport()
    {
        await using var scope = await BrokerDeployScope.CreateAsync();
        scope.WriteAttackerConfig(rewriteGitHub: true);

        var result = await scope.DeployAsync();

        Assert.False(result.Success);
        Assert.Contains("URL rewrite", result.Error, StringComparison.Ordinal);
        Assert.Empty(scope.AttackerMarkers());
        Assert.Equal(0, scope.Server.Unauthorized + scope.Server.Authorized);
    }

    // ---- test 7: no token in any environment ----

    [UnixFact]
    public async Task Environment_GitAndHelperNeverSeeToken()
    {
        await using var scope = await BrokerDeployScope.CreateAsync();

        var result = await scope.DeployAsync();

        Assert.True(result.Success, result.Error);
        var gitEnvironments = scope.ReadShimCalls().Select(call => call.Environment).ToList();
        Assert.NotEmpty(gitEnvironments);
        Assert.All(gitEnvironments, env =>
        {
            Assert.DoesNotContain(Token, env, StringComparison.Ordinal);
            Assert.DoesNotContain("GIT_ASKPASS=", env, StringComparison.Ordinal);
            Assert.DoesNotContain("BUKIT_GITHUB_TOKEN", env, StringComparison.Ordinal);
        });

        // The helper (and every other process on its chain) as the broker saw it.
        var observed = scope.Query.Environments();
        Assert.NotEmpty(scope.Query.HelperPids());
        // The snapshot is the helper's real environment (it carries the wrapper's HOME), not an empty read.
        Assert.All(scope.Query.HelperPids(), pid => Assert.Contains("HOME=" + scope.Home, observed[pid], StringComparison.Ordinal));
        Assert.All(observed.Values, env =>
        {
            Assert.DoesNotContain(Token, env, StringComparison.Ordinal);
            Assert.DoesNotContain("GIT_ASKPASS=", env, StringComparison.Ordinal);
        });
    }

    // ---- test 8: impersonation ----

    [UnixFact]
    public async Task Impersonation_NonDescendantConnectingDuringPushGetsNothing()
    {
        await using var scope = await BrokerDeployScope.CreateAsync();
        var pushHeld = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        scope.Server.BeforeReceivePack = async () =>
        {
            pushHeld.TrySetResult();
            await release.Task;
        };

        var deployment = scope.DeployAsync();
        GitCredentialResponse? response;
        try
        {
            await pushHeld.Task.WaitAsync(TimeSpan.FromSeconds(60));
            // git push is running and registered right now; the test process is not its descendant.
            response = await ExchangeAsync(scope.ReadEndpoint(), new GitCredentialRequest(GitCredentialProtocol.Version, "http", scope.Server.Authority));
        }
        finally
        {
            release.TrySetResult();
        }

        var result = await deployment;
        Assert.True(result.Success, result.Error);
        Assert.NotNull(response);
        Assert.Equal(GitCredentialDenial.Executable, response!.Deny);
        Assert.Null(response.Password);
    }

    // ---- test 9: the user's own credential helper ----

    [UnixFact]
    public async Task UserCredentialHelper_IsNeverAskedAndStoresNothing()
    {
        await using var scope = await BrokerDeployScope.CreateAsync();
        scope.WriteUserCredentialHelper();

        var result = await scope.DeployAsync();

        Assert.True(result.Success, result.Error);
        Assert.False(File.Exists(scope.UserHelperLog), "The user's credential helper must not run.");
    }

    // ---- test 11: cleanup ----

    [UnixFact]
    public async Task Cleanup_AfterSuccessRemovesTempDirectoryAndSocket()
    {
        await using var scope = await BrokerDeployScope.CreateAsync();

        Assert.True((await scope.DeployAsync()).Success);

        scope.AssertDeploymentCleanedUp();
    }

    [UnixFact]
    public async Task Cleanup_AfterAuthenticationFailureRemovesTempDirectoryAndSocket()
    {
        await using var scope = await BrokerDeployScope.CreateAsync(token: WrongToken);

        Assert.False((await scope.DeployAsync()).Success);

        scope.AssertDeploymentCleanedUp();
    }

    [UnixFact]
    public async Task Cleanup_AfterCancellationDuringPushRemovesTempDirectoryAndSocket()
    {
        await using var scope = await BrokerDeployScope.CreateAsync();
        var pushHeld = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        scope.Server.BeforeReceivePack = async () =>
        {
            pushHeld.TrySetResult();
            await release.Task;
        };
        using var cancellation = new CancellationTokenSource();

        var deployment = scope.DeployAsync(cancellation.Token);
        try
        {
            await pushHeld.Task.WaitAsync(TimeSpan.FromSeconds(60));
            await cancellation.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => deployment.WaitAsync(TimeSpan.FromSeconds(30)));
        }
        finally
        {
            release.TrySetResult();
        }

        scope.AssertDeploymentCleanedUp();
    }

    // ---- test 13: helper command value when Bukit runs from its dll ----

    [UnixFact]
    public async Task HelperCommand_MatchesHowBukitRuns()
    {
        await using var scope = await BrokerDeployScope.CreateAsync();

        Assert.True((await scope.DeployAsync()).Success);

        var helper = scope.ReadHelperValue();
        var self = scope.SelfExecutable;
        if (string.Equals(Path.GetFileNameWithoutExtension(self), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            var dll = ProcessInspector.TryRealPath(Path.Combine(AppContext.BaseDirectory, "bukit.dll"))!;
            Assert.StartsWith(
                $"!{GitHubPagesDeployProvider.ShellQuote(self)} {GitHubPagesDeployProvider.ShellQuote(dll)} {GitCredentialPeerPolicy.HelperCommand} ",
                helper,
                StringComparison.Ordinal);
        }
        else
        {
            Assert.StartsWith($"!{GitHubPagesDeployProvider.ShellQuote(self)} {GitCredentialPeerPolicy.HelperCommand} ", helper, StringComparison.Ordinal);
        }
    }

    private static async Task<GitCredentialResponse?> ExchangeAsync(string socketPath, GitCredentialRequest request)
    {
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath));
        await using var stream = new NetworkStream(socket);
        await stream.WriteAsync(GitCredentialProtocol.Serialize(request));
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var line = await GitCredentialProtocol.ReadLineAsync(stream, cts.Token);
        return line is null ? null : GitCredentialProtocol.ParseResponse(line);
    }

    /// <summary>One deployment against a local authenticated repository, in an isolated environment.</summary>
    private sealed class BrokerDeployScope : IAsyncDisposable
    {
        private static readonly string[] ClearedVariables =
        [
            "GIT_DIR", "GIT_WORK_TREE", "GIT_COMMON_DIR", "GIT_INDEX_FILE", "GIT_OBJECT_DIRECTORY",
            "GIT_ALTERNATE_OBJECT_DIRECTORIES", "GIT_CONFIG", "GIT_CONFIG_PARAMETERS", "GIT_CONFIG_COUNT",
            "GIT_CONFIG_NOSYSTEM", "GIT_ALLOW_PROTOCOL", "GIT_ASKPASS", "SSH_ASKPASS", "GIT_TERMINAL_PROMPT",
            "GITHUB_TOKEN", "BUKIT_TEST_GIT_SCRIPT",
            "HTTP_PROXY", "http_proxy", "HTTPS_PROXY", "https_proxy", "ALL_PROXY", "all_proxy",
        ];

        private readonly Dictionary<string, string?> _originalEnvironment = new(StringComparer.Ordinal);
        private readonly string _realGit;
        private readonly string _project;
        private readonly string _output;
        private readonly string _shimDirectory;
        private readonly string _shimLog;
        private readonly string _markers;
        private readonly string _globalConfig;
        private readonly string _systemConfig;
        private readonly string _remote;

        private BrokerDeployScope(string token)
        {
            Root = Directory.CreateTempSubdirectory("bukit-broker-it-").FullName;
            _realGit = ResolveRealGit();
            _project = Directory.CreateDirectory(Path.Combine(Root, "project")).FullName;
            _output = Directory.CreateDirectory(Path.Combine(Root, "output")).FullName;
            _shimDirectory = Directory.CreateDirectory(Path.Combine(Root, "bin")).FullName;
            _markers = Directory.CreateDirectory(Path.Combine(Root, "markers")).FullName;
            var home = Home = Directory.CreateDirectory(Path.Combine(Root, "home")).FullName;
            var serverRoot = Directory.CreateDirectory(Path.Combine(Root, "srv")).FullName;
            _remote = Path.Combine(serverRoot, "site.git");
            _shimLog = Path.Combine(Root, "shim.log");
            _globalConfig = Path.Combine(home, ".gitconfig");
            _systemConfig = Path.Combine(Root, "system.gitconfig");
            File.WriteAllText(_globalConfig, string.Empty);
            File.WriteAllText(_systemConfig, string.Empty);
            UserHelperLog = Path.Combine(_markers, "user-helper.log");

            foreach (var name in ClearedVariables)
            {
                SetEnvironment(name, null);
            }

            SetEnvironment("HOME", home);
            SetEnvironment("XDG_CONFIG_HOME", Path.Combine(home, ".config"));
            SetEnvironment("GIT_CONFIG_GLOBAL", _globalConfig);
            SetEnvironment("GIT_CONFIG_SYSTEM", _systemConfig);
            SetEnvironment(TokenEnv, null);
            SetEnvironment("BUKIT_DEPLOY_GIT_TIMEOUT_SECONDS", "60");
            // As BukitJalil does: the token arrives through --secrets-stdin, never the environment.
            SecretStore.Install(new Dictionary<string, string>(StringComparer.Ordinal) { [TokenEnv] = token });

            var serverConfig = Path.Combine(Root, "server.gitconfig");
            File.WriteAllText(serverConfig, string.Empty);
            Server = new LocalGitHttpServer(_realGit, serverRoot, serverConfig, Token);
            Query = new ObservingProcessQuery();
            SelfExecutable = ProcessInspector.TryRealPath(Environment.ProcessPath!)!;
        }

        public string Root { get; }

        public LocalGitHttpServer Server { get; }

        public ObservingProcessQuery Query { get; }

        public string SelfExecutable { get; }

        public string UserHelperLog { get; }

        public string Home { get; }

        private string RemoteUrl => Server.BaseUrl + "site.git";

        public static async Task<BrokerDeployScope> CreateAsync(string token = Token)
        {
            var scope = new BrokerDeployScope(token);
            try
            {
                await scope.SeedAsync();
                scope.WriteShim();
                scope.SetEnvironment("PATH", scope._shimDirectory + Path.PathSeparator + scope._originalEnvironment.GetValueOrDefault("PATH", Environment.GetEnvironmentVariable("PATH")));
                return scope;
            }
            catch
            {
                await scope.DisposeAsync();
                throw;
            }
        }

        public async Task<DeployResult> DeployAsync(CancellationToken ct = default)
        {
            var context = new DeployContext
            {
                OutputDir = _output,
                SiteUrl = "https://example.com",
                BaseUrl = "/",
                Branch = "pages",
                Message = "broker integration",
                Cname = null,
                TokenEnv = TokenEnv,
                KeepHistory = true,
                Logger = new NullLogger(),
            };
            GitHubPagesDeployProvider.TransportPolicyOverride.Value = new GitTransportPolicy(
                new GitCredentialTarget("http", Server.Authority),
                ["http"],
                Query);
            try
            {
                return await GitHubPagesDeployProvider.DeployAsync(context, _project, ct);
            }
            finally
            {
                GitHubPagesDeployProvider.TransportPolicyOverride.Value = null;
            }
        }

        public async Task<string> ReadRemoteFileAsync(string path) =>
            (await RunRealGitAsync(Root, "--git-dir", _remote, "show", $"pages:{path}")).Trim();

        /// <summary>Global and system config that tries every way plan 3.5 names to reach the token.</summary>
        public void WriteAttackerConfig(bool rewriteGitHub)
        {
            var hooks = Directory.CreateDirectory(Path.Combine(Root, "evil-hooks")).FullName;
            var template = Directory.CreateDirectory(Path.Combine(Root, "evil-template")).FullName;
            var templateHooks = Directory.CreateDirectory(Path.Combine(template, "hooks")).FullName;
            // A hook that ran would plant an interception proxy and CA into the temporary repo.
            foreach (var dir in new[] { hooks, templateHooks })
            {
                foreach (var hook in new[] { "post-checkout", "pre-commit", "post-commit", "pre-push", "reference-transaction", "pre-auto-gc" })
                {
                    WriteExecutable(dir, hook, $$"""
                        printf '%s %s\n' {{Quote(hook)}} "$PWD" >> {{Quote(Path.Combine(_markers, "hook.log"))}}
                        env >> {{Quote(Path.Combine(_markers, "hook.env"))}}
                        dir=$(git rev-parse --git-dir) && printf '[http]\n\tproxy = http://127.0.0.1:9\n\tsslVerify = false\n\tsslCAInfo = /tmp/evil-ca.pem\n' >> "$dir/config"
                        """);
                }
            }

            File.WriteAllText(Path.Combine(template, "config"), "[http]\n\tproxy = http://127.0.0.1:9\n\tsslVerify = false\n");
            var fsmonitor = WriteExecutable(Root, "evil-fsmonitor", $"echo fsmonitor >> {Quote(Path.Combine(_markers, "fsmonitor.log"))}\nexit 1");
            var filter = WriteExecutable(Root, "evil-filter", $"echo \"filter $1\" >> {Quote(Path.Combine(_markers, "filter.log"))}\ncat");
            WriteExecutable(_shimDirectory, "git-remote-foo", $"echo \"remote-foo $*\" >> {Quote(Path.Combine(_markers, "remote-foo.log"))}\nenv >> {Quote(Path.Combine(_markers, "remote-foo.env"))}\nexit 1");
            var attributes = Path.Combine(Root, "evil.gitattributes");
            File.WriteAllText(attributes, "* filter=evil\n");
            var rewriteBase = rewriteGitHub ? "https://github.com/" : Server.BaseUrl;
            var config = $"""
                [core]
                	hooksPath = {hooks}
                	fsmonitor = {fsmonitor}
                	attributesFile = {attributes}
                [init]
                	templateDir = {template}
                [http]
                	sslVerify = false
                	proxy = http://127.0.0.1:9
                [url "foo::{rewriteBase}"]
                	insteadOf = {rewriteBase}
                [filter "evil"]
                	clean = {filter} clean
                	smudge = {filter} smudge
                	required = true

                """;
            File.WriteAllText(_globalConfig, config);
            File.WriteAllText(_systemConfig, config);
        }

        /// <summary>A credential helper in the user's global config that records any call.</summary>
        public void WriteUserCredentialHelper()
        {
            var helper = WriteExecutable(Root, "user-helper", $"echo \"$1\" >> {Quote(UserHelperLog)}\ncat >> {Quote(UserHelperLog)}");
            var config = $"[credential]\n\thelper = {helper}\n[credential \"{Server.BaseUrl}\"]\n\thelper = {helper}\n";
            File.WriteAllText(_globalConfig, config);
            File.WriteAllText(_systemConfig, config);
        }

        public string[] AttackerMarkers() => Directory.GetFiles(_markers);

        public string DescribeMarkers() =>
            string.Join("\n", Directory.GetFiles(_markers).Where(f => !f.EndsWith(".env", StringComparison.Ordinal)).Select(f => Path.GetFileName(f) + ": " + File.ReadAllText(f)));

        public string ReadAllRecordedText() =>
            string.Concat(Directory.GetFiles(_markers).Select(File.ReadAllText)) + (File.Exists(_shimLog) ? File.ReadAllText(_shimLog) : string.Empty);

        public IReadOnlyList<(IReadOnlyList<string> Arguments, string Directory, string Environment)> ReadShimCalls()
        {
            var calls = new List<(IReadOnlyList<string>, string, string)>();
            foreach (var block in File.ReadAllText(_shimLog).Split("\u001eCALL\n", StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = block.Split("\u001eENV\n", 2);
                var header = parts[0].Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var arguments = header.Where(l => l.StartsWith("ARG ", StringComparison.Ordinal)).Select(l => l[4..]).ToList();
                var directory = header.Single(l => l.StartsWith("PWD ", StringComparison.Ordinal))[4..];
                calls.Add((arguments, directory, parts.Length > 1 ? parts[1] : string.Empty));
            }

            return calls;
        }

        public string ReadHelperValue() =>
            ReadShimCalls().SelectMany(call => call.Arguments)
                .Select(arg => arg.StartsWith("credential.helper=!", StringComparison.Ordinal) ? arg["credential.helper=".Length..] : null)
                .First(value => value is not null)!;

        public string ReadEndpoint()
        {
            var deadline = Stopwatch.StartNew();
            while (deadline.Elapsed < TimeSpan.FromSeconds(30))
            {
                if (File.Exists(_shimLog))
                {
                    var match = Regex.Match(File.ReadAllText(_shimLog), "credential\\.helper=!.* '([^']*)'\\n");
                    if (match.Success)
                    {
                        return match.Groups[1].Value;
                    }
                }

                Thread.Sleep(25);
            }

            throw new TimeoutException("No authenticated git call was recorded.");
        }

        public void AssertDeploymentCleanedUp()
        {
            var endpoint = ReadEndpoint();
            Assert.False(File.Exists(endpoint), "The broker socket must be removed.");
            Assert.False(Directory.Exists(Path.GetDirectoryName(endpoint)), "The broker's private directory must be removed.");
            var checkouts = ReadShimCalls()
                .Select(call => call.Directory)
                .Where(dir => Path.GetFileName(dir) == "checkout")
                .Distinct()
                .ToList();
            Assert.NotEmpty(checkouts);
            Assert.All(checkouts, checkout => Assert.False(Directory.Exists(Path.GetDirectoryName(checkout)), "The deploy temporary directory must be removed."));
        }

        public async ValueTask DisposeAsync()
        {
            await Server.DisposeAsync();
            SecretStore.Reset();
            foreach (var (name, value) in _originalEnvironment)
            {
                Environment.SetEnvironmentVariable(name, value);
            }

            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
                // A git process killed by cancellation may still be releasing files.
                await Task.Delay(500);
                Directory.Delete(Root, recursive: true);
            }
        }

        private async Task SeedAsync()
        {
            await RunRealGitAsync(Root, "init", "--bare", _remote);
            await RunRealGitAsync(_project, "init");
            await RunRealGitAsync(_project, "config", "user.name", "test");
            await RunRealGitAsync(_project, "config", "user.email", "test@example.invalid");
            await RunRealGitAsync(_project, "checkout", "-b", "pages");
            File.WriteAllText(Path.Combine(_project, "old.html"), "old page");
            await RunRealGitAsync(_project, "add", "old.html");
            await RunRealGitAsync(_project, "commit", "-m", "seed");
            await RunRealGitAsync(_project, "push", _remote, "pages");
            await RunRealGitAsync(_project, "remote", "add", "origin", "https://github.com/owner/site.git");

            File.WriteAllText(Path.Combine(_output, ".bukit-build-state.json"), "{\"version\":1,\"status\":\"completed\"}");
            Directory.CreateDirectory(Path.Combine(_output, ".bukit"));
            File.WriteAllText(Path.Combine(_output, ".bukit", "publish-audit-report.json"), "{\"schema\":\"https://bukit.dev/schemas/publish-audit-report.v1.json\",\"documents\":[]}");
            File.WriteAllText(Path.Combine(_output, "index.html"), "new page");
        }

        /// <summary>
        /// Records arguments, directory and environment, maps the deploy-private remote name to
        /// the local URL for transport commands, then execs the real git (same pid).
        /// </summary>
        private void WriteShim() => WriteExecutable(_shimDirectory, "git", $$"""
            log={{Quote(_shimLog)}}
            {
              printf '\036CALL\n'
              for a in "$@"; do printf 'ARG %s\n' "$a"; done
              printf 'PWD %s\n' "$PWD"
              printf '\036ENV\n'
              env
            } >> "$log"
            sub=""; skip=0
            for arg in "$@"; do
              if [ "$skip" = 1 ]; then skip=0; continue; fi
              if [ "$arg" = "-c" ]; then skip=1; continue; fi
              sub="$arg"; break
            done
            case "$sub" in
              ls-remote|fetch|push)
                count=$#
                for arg in "$@"; do
                  case "$arg" in bukit-deploy-*) arg={{Quote(RemoteUrl)}} ;; esac
                  set -- "$@" "$arg"
                done
                shift "$count"
                ;;
            esac
            exec {{Quote(_realGit)}} "$@"
            """);

        private async Task<string> RunRealGitAsync(string workingDirectory, params string[] args)
        {
            var start = new ProcessStartInfo(_realGit)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var arg in args)
            {
                start.ArgumentList.Add(arg);
            }

            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            try
            {
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
                Assert.True(process.ExitCode == 0, $"git {string.Join(' ', args)}: {await stderr}");
                return await stdout;
            }
            finally
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
        }

        private void SetEnvironment(string name, string? value)
        {
            _originalEnvironment.TryAdd(name, Environment.GetEnvironmentVariable(name));
            Environment.SetEnvironmentVariable(name, value);
        }

        private static string ResolveRealGit()
        {
            var method = typeof(GitHubPagesDeployProvider).GetMethod("ResolveGit", BindingFlags.NonPublic | BindingFlags.Static)!;
            return ProcessInspector.TryRealPath((string)method.Invoke(null, [null])!)!;
        }

        private static string Quote(string value) => GitHubPagesDeployProvider.ShellQuote(value);

        private static string WriteExecutable(string directory, string name, string body)
        {
            var path = Path.Combine(directory, name);
            File.WriteAllText(path, "#!/bin/sh\n" + body + "\n");
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            return path;
        }
    }

    /// <summary>
    /// The production process query, observed: records every identity and executable the broker
    /// looks at (test 6), snapshots each process's environment while it waits for the broker
    /// (test 7), and can hold back the first registration to force the race (test 10).
    /// </summary>
    private sealed class ObservingProcessQuery : IGitCredentialProcessQuery
    {
        private readonly ConcurrentDictionary<int, (ProcessIdentity Identity, string? Executable)> _seen = new();
        private readonly ConcurrentDictionary<int, string> _environments = new();
        private readonly ConcurrentDictionary<int, int> _callPids = new();
        private readonly TaskCompletionSource<int> _secondConnection = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly int _self = Environment.ProcessId;
        private bool _hold;
        private int _callCount;

        public bool RaceObserved { get; private set; }

        public void HoldFirstRegistration() => _hold = true;

        public bool TryGetIdentity(int pid, out ProcessIdentity identity)
        {
            var call = Interlocked.Increment(ref _callCount);
            _callPids[call] = pid;
            if (_hold && call == 1)
            {
                // Registration of the first git: wait until its helper has been refused and retried.
                RaceObserved = _secondConnection.Task.Wait(TimeSpan.FromSeconds(15));
            }

            var found = SystemGitCredentialProcessQuery.Instance.TryGetIdentity(pid, out identity);
            if (found)
            {
                _seen.TryAdd(pid, (identity, ProcessInspector.TryGetExecutablePath(pid)));
                if (pid != _self)
                {
                    _environments.TryAdd(pid, ReadEnvironment(pid));
                }
            }

            // Call 2 was the helper's first connection (answered "unregistered"); call 3 is the
            // same helper connecting again.
            if (_hold && call == 3)
            {
                if (_callPids.TryGetValue(2, out var first) && first == pid)
                {
                    _secondConnection.TrySetResult(pid);
                    Thread.Sleep(100);
                }
            }

            return found;
        }

        public string? TryGetExecutablePath(int pid) => SystemGitCredentialProcessQuery.Instance.TryGetExecutablePath(pid);

        public IReadOnlyList<string>? TryGetArguments(int pid) => SystemGitCredentialProcessQuery.Instance.TryGetArguments(pid);

        public string? TryRealPath(string path) => SystemGitCredentialProcessQuery.Instance.TryRealPath(path);

        public IReadOnlyCollection<int> HelperPids()
        {
            var self = ProcessInspector.TryRealPath(Environment.ProcessPath!);
            return _seen.Where(p => p.Key != _self && p.Value.Executable == self).Select(p => p.Key).ToArray();
        }

        public IReadOnlyDictionary<int, string> Environments() => _environments;

        /// <summary>Executables from the first helper up to the topmost process the broker looked at.</summary>
        public List<string> HelperChain()
        {
            var helper = HelperPids().Min();
            var chain = new List<string>();
            for (var pid = helper; _seen.TryGetValue(pid, out var entry) && chain.Count < 64; pid = entry.Identity.ParentPid)
            {
                chain.Add(entry.Executable ?? $"?{pid}");
            }

            return chain;
        }

        private static string ReadEnvironment(int pid)
        {
            if (OperatingSystem.IsLinux())
            {
                try
                {
                    return File.ReadAllText($"/proc/{pid}/environ").Replace('\0', '\n');
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    return string.Empty;
                }
            }

            var start = new ProcessStartInfo("/bin/ps") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            foreach (var arg in new[] { "-E", "-ww", "-o", "command=", "-p", pid.ToString(System.Globalization.CultureInfo.InvariantCulture) })
            {
                start.ArgumentList.Add(arg);
            }

            using var ps = Process.Start(start)!;
            var output = ps.StandardOutput.ReadToEnd();
            ps.WaitForExit(5000);
            return output;
        }
    }

    /// <summary>
    /// <c>git http-backend</c> behind HTTP Basic auth on 127.0.0.1. Requests without the expected
    /// password get 401 at once.
    /// </summary>
    private sealed class LocalGitHttpServer : IAsyncDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly string _git;
        private readonly string _projectRoot;
        private readonly string _emptyConfig;
        private readonly string _password;
        private readonly Task _loop;
        private readonly ConcurrentQueue<string> _wrongPasswords = new();
        private int _unauthorized;
        private int _authorized;

        public LocalGitHttpServer(string git, string projectRoot, string emptyConfig, string password)
        {
            _git = git;
            _projectRoot = projectRoot;
            _emptyConfig = emptyConfig;
            _password = password;
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            Authority = $"127.0.0.1:{port}";
            BaseUrl = $"http://{Authority}/";
            _listener.Prefixes.Add(BaseUrl);
            _listener.Start();
            _loop = Task.Run(AcceptAsync);
        }

        public string Authority { get; }

        public string BaseUrl { get; }

        public int Unauthorized => Volatile.Read(ref _unauthorized);

        public int Authorized => Volatile.Read(ref _authorized);

        public IReadOnlyCollection<string> WrongPasswords => _wrongPasswords.ToArray();

        public Func<Task>? BeforeReceivePack { get; set; }

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            _listener.Close();
            await _loop;
        }

        private async Task AcceptAsync()
        {
            while (true)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
                {
                    return;
                }

                _ = Task.Run(() => HandleAsync(context));
            }
        }

        private async Task HandleAsync(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;
            try
            {
                if (!TryAuthorize(request.Headers["Authorization"], out var user))
                {
                    Interlocked.Increment(ref _unauthorized);
                    response.StatusCode = 401;
                    response.AddHeader("WWW-Authenticate", "Basic realm=\"bukit-test\"");
                    response.Close();
                    return;
                }

                Interlocked.Increment(ref _authorized);
                if (request.HttpMethod == "POST" && request.Url!.AbsolutePath.EndsWith("/git-receive-pack", StringComparison.Ordinal) && BeforeReceivePack is { } hold)
                {
                    await hold();
                }

                using var body = new MemoryStream();
                await request.InputStream.CopyToAsync(body);
                await RunBackendAsync(request, response, user, body.ToArray());
            }
            catch (Exception ex) when (ex is IOException or HttpListenerException or InvalidOperationException or ObjectDisposedException)
            {
                // The client went away (e.g. a cancelled deploy killed git).
            }
        }

        private bool TryAuthorize(string? header, out string user)
        {
            user = string.Empty;
            if (header is null || !header.StartsWith("Basic ", StringComparison.Ordinal))
            {
                return false;
            }

            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header["Basic ".Length..]));
            var separator = decoded.IndexOf(':');
            if (separator < 0)
            {
                return false;
            }

            user = decoded[..separator];
            var password = decoded[(separator + 1)..];
            if (password == _password)
            {
                return true;
            }

            _wrongPasswords.Enqueue(password);
            Interlocked.Increment(ref _unauthorized);
            return false;
        }

        private async Task RunBackendAsync(HttpListenerRequest request, HttpListenerResponse response, string user, byte[] body)
        {
            var start = new ProcessStartInfo(_git)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            start.ArgumentList.Add("http-backend");
            start.Environment["GIT_PROJECT_ROOT"] = _projectRoot;
            start.Environment["GIT_HTTP_EXPORT_ALL"] = "1";
            start.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
            start.Environment["GIT_CONFIG_GLOBAL"] = _emptyConfig;
            start.Environment["GIT_CONFIG_SYSTEM"] = _emptyConfig;
            start.Environment["REQUEST_METHOD"] = request.HttpMethod;
            start.Environment["PATH_INFO"] = request.Url!.AbsolutePath;
            start.Environment["QUERY_STRING"] = request.Url.Query.TrimStart('?');
            start.Environment["CONTENT_TYPE"] = request.ContentType ?? string.Empty;
            start.Environment["CONTENT_LENGTH"] = body.Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
            start.Environment["REMOTE_USER"] = user;
            start.Environment["REMOTE_ADDR"] = "127.0.0.1";
            if (request.Headers["Content-Encoding"] is { } encoding)
            {
                start.Environment["HTTP_CONTENT_ENCODING"] = encoding;
            }

            if (request.Headers["Git-Protocol"] is { } protocol)
            {
                start.Environment["GIT_PROTOCOL"] = protocol;
            }

            using var backend = Process.Start(start)!;
            var stdout = new MemoryStream();
            var copy = backend.StandardOutput.BaseStream.CopyToAsync(stdout);
            var stderr = backend.StandardError.ReadToEndAsync();
            await backend.StandardInput.BaseStream.WriteAsync(body);
            backend.StandardInput.Close();
            await copy;
            await stderr;
            await backend.WaitForExitAsync();

            var raw = stdout.ToArray();
            var split = raw.AsSpan().IndexOf("\r\n\r\n"u8);
            var separatorLength = 4;
            if (split < 0)
            {
                split = raw.AsSpan().IndexOf("\n\n"u8);
                separatorLength = 2;
            }

            var headers = split < 0 ? string.Empty : Encoding.ASCII.GetString(raw, 0, split);
            response.StatusCode = 200;
            foreach (var line in headers.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var colon = line.IndexOf(':');
                if (colon < 0)
                {
                    continue;
                }

                var name = line[..colon].Trim();
                var value = line[(colon + 1)..].Trim();
                if (name.Equals("Status", StringComparison.OrdinalIgnoreCase))
                {
                    response.StatusCode = int.Parse(value.Split(' ')[0], System.Globalization.CultureInfo.InvariantCulture);
                }
                else if (name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    response.ContentType = value;
                }
                else if (!name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                {
                    response.AddHeader(name, value);
                }
            }

            var content = split < 0 ? raw : raw[(split + separatorLength)..];
            await response.OutputStream.WriteAsync(content);
            response.Close();
        }
    }

    private sealed class NullLogger : ILogger
    {
        public void Debug(string message)
        {
        }

        public void Info(string message)
        {
        }

        public void Warn(string message)
        {
        }

        public void Error(string message)
        {
        }
    }
}
