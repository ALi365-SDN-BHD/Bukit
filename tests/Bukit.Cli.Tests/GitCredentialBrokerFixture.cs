using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using Bukit.Cli.Deploy;
using Bukit.Shared;
using Bukit.Shared.Processes;
using Xunit;

namespace Bukit.Cli.Tests;

/// <summary>
/// One deployment against a local authenticated repository, in an isolated environment (plan 3.7
/// tests 5-13). Unix puts a <c>/bin/sh</c> wrapper named <c>git</c> first on PATH that records
/// each call and execs the real git; Windows uses <see cref="WindowsGitShim"/>, which starts the
/// real git as its child. Either way the wrapper maps the deploy-private remote to the local URL
/// and the process chain the broker checks is the real one.
/// </summary>
internal sealed class BrokerDeployScope : IAsyncDisposable
{
    internal const string Token = "ghp_test_broker_integration_good_0001";
    internal const string WrongToken = "ghp_test_broker_integration_wrong_0002";
    private const string TokenEnv = "BUKIT_IT_BROKER_DEPLOY_TOKEN";

    private static readonly string[] ClearedVariables =
    [
        "GIT_DIR", "GIT_WORK_TREE", "GIT_COMMON_DIR", "GIT_INDEX_FILE", "GIT_OBJECT_DIRECTORY",
        "GIT_ALTERNATE_OBJECT_DIRECTORIES", "GIT_CONFIG", "GIT_CONFIG_PARAMETERS", "GIT_CONFIG_COUNT",
        "GIT_CONFIG_NOSYSTEM", "GIT_ALLOW_PROTOCOL", "GIT_ASKPASS", "SSH_ASKPASS", "GIT_TERMINAL_PROMPT",
        "GITHUB_TOKEN", "BUKIT_TEST_GIT_SCRIPT", "BUKIT_DEPLOY_INJECT_CONFIG",
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
    private readonly (string Executable, string? Assembly)? _self;

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
        // The Windows test host is testhost.exe, not Bukit: the helper runs as dotnet bukit.dll.
        _self = OperatingSystem.IsWindows()
            ? (ProcessInspector.TryRealPath(ResolveDotnetHost())!, ProcessInspector.TryRealPath(Path.Combine(AppContext.BaseDirectory, "bukit.dll"))!)
            : null;
        SelfExecutable = _self?.Executable ?? ProcessInspector.TryRealPath(Environment.ProcessPath!)!;
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
            if (OperatingSystem.IsWindows())
            {
                WindowsGitShim.Install(scope._shimDirectory);
                scope.SetEnvironment("BUKIT_DEPLOY_REAL_GIT", scope._realGit);
                scope.SetEnvironment("BUKIT_DEPLOY_HTTP_REMOTE", scope.RemoteUrl);
            }
            else
            {
                scope.WriteShim();
            }

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
            Query,
            _self);
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
    [UnsupportedOSPlatform("windows")]
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
    [UnsupportedOSPlatform("windows")]
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

        ClearReadOnlyAttributes();
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

    /// <summary>Git writes its objects read-only, and Windows refuses to delete a read-only file.</summary>
    private void ClearReadOnlyAttributes()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };
        foreach (var file in Directory.EnumerateFiles(Root, "*", options))
        {
            File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
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
    [UnsupportedOSPlatform("windows")]
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

    public void SetEnvironment(string name, string? value)
    {
        _originalEnvironment.TryAdd(name, Environment.GetEnvironmentVariable(name));
        Environment.SetEnvironmentVariable(name, value);
    }

    private static string ResolveRealGit()
    {
        var method = typeof(GitHubPagesDeployProvider).GetMethod("ResolveGit", BindingFlags.NonPublic | BindingFlags.Static)!;
        return ProcessInspector.TryRealPath((string)method.Invoke(null, [null])!)!;
    }

    private static string ResolveDotnetHost()
    {
        var host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrEmpty(host) && File.Exists(host))
        {
            return host;
        }

        var name = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
        return (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(dir => Path.Combine(dir.Trim(), name))
            .FirstOrDefault(File.Exists)
            ?? throw new InvalidOperationException("dotnet host not found");
    }

    private static string Quote(string value) => GitHubPagesDeployProvider.ShellQuote(value);

    [UnsupportedOSPlatform("windows")]
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
internal sealed class ObservingProcessQuery : IGitCredentialProcessQuery
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
        if (OperatingSystem.IsWindows())
        {
            return string.Empty;
        }

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
internal sealed class LocalGitHttpServer : IAsyncDisposable
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

internal sealed class NullLogger : ILogger
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
