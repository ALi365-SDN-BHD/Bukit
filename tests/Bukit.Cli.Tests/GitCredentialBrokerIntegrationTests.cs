using System.Net.Sockets;
using Bukit.Cli.Deploy;
using Bukit.Shared;
using Bukit.Shared.Processes;
using Xunit;

namespace Bukit.Cli.Tests;

/// <summary>
/// Plan 3.7 integration tests 5-11 and 13: real git, the real broker and the real
/// <c>__git-credential</c> helper against a local <c>git http-backend</c> behind Basic auth
/// (localhost only), through <see cref="BrokerDeployScope"/>. The transport policy is replaced
/// only through <see cref="GitHubPagesDeployProvider.TransportPolicyOverride"/>; the peer checks
/// are the production ones. Unix only: these tests read the shell wrapper's records; Windows runs
/// the real push in <see cref="GitCredentialBrokerWindowsIntegrationTests"/> (plan 3.7 test 12).
/// </summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class GitCredentialBrokerIntegrationTests
{
    private const string Token = BrokerDeployScope.Token;
    private const string WrongToken = BrokerDeployScope.WrongToken;

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
    /// Ubuntu CI (git 2.43, /bin/sh is dash) keeps the shell:
    /// <c>dotnet &lt;- dash &lt;- git-remote-http &lt;- git &lt;- dash (wrapper root)</c>, four levels.
    /// On macOS git's <c>sh -c</c> execs the helper, so no shell stays on the chain; the trampoline adds
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
        Assert.True(Path.GetFileName(chain[^1]) is "git" or "sh" or "dash" or "bash", described);
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

    /// <summary>
    /// The transport never reads global or system config, so a rewrite of the GitHub target there
    /// neither blocks the deploy nor runs its remote helper.
    /// </summary>
    [UnixFact]
    public async Task AttackerGitConfig_GlobalRewriteOfGitHubTargetIsIgnored()
    {
        await using var scope = await BrokerDeployScope.CreateAsync();
        scope.WriteAttackerConfig(rewriteGitHub: true);

        var result = await scope.DeployAsync();

        Assert.True(result.Success, result.Error);
        Assert.Equal("new page", await scope.ReadRemoteFileAsync("index.html"));
        Assert.True(scope.AttackerMarkers().Length == 0, scope.DescribeMarkers());
    }

    // ---- test 7: no token in any environment ----

    [UnixFact]
    public async Task Environment_GitAndHelperNeverSeeToken()
    {
        await using var scope = await BrokerDeployScope.CreateAsync();
        // Plan Q7 / 3.5: the environment is trusted, git config is not. SSL_CERT_FILE is how
        // OpenSSL git users supply a company CA, so authenticated calls must still receive it.
        scope.SetEnvironment("SSL_CERT_FILE", Path.Combine(scope.Root, "company-ca.pem"));

        var result = await scope.DeployAsync();

        Assert.True(result.Success, result.Error);
        var calls = scope.ReadShimCalls();
        var gitEnvironments = calls.Select(call => call.Environment).ToList();
        Assert.NotEmpty(gitEnvironments);
        Assert.All(gitEnvironments, env =>
        {
            Assert.DoesNotContain(Token, env, StringComparison.Ordinal);
            Assert.DoesNotContain("GIT_ASKPASS=", env, StringComparison.Ordinal);
            Assert.DoesNotContain("BUKIT_GITHUB_TOKEN", env, StringComparison.Ordinal);
        });
        var authenticated = calls
            .Where(call => call.Arguments.Any(arg => arg is "ls-remote" or "fetch" or "push"))
            .Select(call => call.Environment)
            .ToList();
        Assert.NotEmpty(authenticated);
        Assert.All(authenticated, env => Assert.Contains("SSL_CERT_FILE=", env, StringComparison.Ordinal));

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
}
