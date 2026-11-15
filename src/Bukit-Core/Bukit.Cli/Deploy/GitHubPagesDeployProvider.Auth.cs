using System.Diagnostics;
using Bukit.Shared;
using Bukit.Shared.Processes;

namespace Bukit.Cli.Deploy;

/// <summary>
/// Where an authenticated git call may connect. Production allows only https://github.com
/// with system certificates; tests replace it through <see cref="GitHubPagesDeployProvider.TransportPolicyOverride"/>.
/// A test policy may also observe the broker's process queries through <paramref name="ProcessQuery"/>
/// and name the helper's executable through <paramref name="Self"/> (a test host is not Bukit);
/// production always uses the system query and the running Bukit.
/// </summary>
internal sealed record GitTransportPolicy(
    GitCredentialTarget Target,
    IReadOnlyList<string> Protocols,
    IGitCredentialProcessQuery? ProcessQuery = null,
    (string Executable, string? Assembly)? Self = null)
{
    internal static readonly GitTransportPolicy GitHub = new(GitCredentialTarget.GitHub, ["https"]);
}

public sealed partial class GitHubPagesDeployProvider
{
    /// <summary>Test-only seam (plan 3.7 test 10); never set from configuration or the environment.</summary>
    internal static readonly AsyncLocal<GitTransportPolicy?> TransportPolicyOverride = new();

    /// <summary>
    /// Keys git itself writes into a fresh repository, plus the deploy identity. Anything else
    /// in the temporary repository's config means something other than Bukit wrote it, so
    /// authentication is refused (plan 3.5).
    /// </summary>
    private static readonly HashSet<string> AllowedLocalConfigKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "core.repositoryformatversion",
        "core.filemode",
        "core.bare",
        "core.logallrefupdates",
        "core.ignorecase",
        "core.precomposeunicode",
        "core.symlinks",
        "user.name",
        "user.email",
    };

    private static readonly string[] AuthenticatedRemovedEnvironmentVariables =
    [
        "GIT_ASKPASS",
        "SSH_ASKPASS",
        "GIT_SSL_NO_VERIFY",
        "GIT_SSL_CAINFO",
        "GIT_SSL_CAPATH",
        "GIT_PROXY_COMMAND",
    ];

    /// <summary>
    /// Isolation for every git call in the temporary repository (plan 3.5): no system or global
    /// config, no templates, no hooks, no fsmonitor, no system attributes.
    /// </summary>
    private sealed class GitSandbox
    {
        private GitSandbox(string globalConfig, string hooksDirectory)
        {
            GlobalConfig = globalConfig;
            HooksDirectory = hooksDirectory;
        }

        internal string GlobalConfig { get; }

        internal string HooksDirectory { get; }

        internal static GitSandbox Create(string tempDir)
        {
            var globalConfig = Path.Combine(tempDir, "empty.gitconfig");
            File.WriteAllText(globalConfig, string.Empty);
            var hooks = Directory.CreateDirectory(Path.Combine(tempDir, "empty-hooks")).FullName;
            return new GitSandbox(globalConfig, hooks);
        }

        internal void Apply(ProcessStartInfo psi)
        {
            psi.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
            psi.Environment["GIT_CONFIG_GLOBAL"] = GlobalConfig;
            psi.Environment["GIT_ATTR_NOSYSTEM"] = "1";
            // GIT_ALLOW_PROTOCOL overrides protocol.allow; traces can print the auth header;
            // GIT_EXEC_PATH would choose the remote helper and the trusted shell directory.
            foreach (var name in psi.Environment.Keys
                         .Where(static name =>
                             // Windows environment names ignore case: match them the same way.
                             name.ToUpperInvariant() is "GIT_ALLOW_PROTOCOL" or "GIT_TEMPLATE_DIR" or "GIT_CURL_VERBOSE" or "GIT_EXEC_PATH" ||
                             name.StartsWith("GIT_TRACE", StringComparison.OrdinalIgnoreCase))
                         .ToArray())
            {
                psi.Environment.Remove(name);
            }

            string[] prefix =
            [
                "-c", "init.templateDir=",
                "-c", $"core.hooksPath={HooksDirectory}",
                "-c", "core.fsmonitor=false",
            ];
            for (var i = prefix.Length - 1; i >= 0; i--)
            {
                psi.ArgumentList.Insert(0, prefix[i]);
            }
        }
    }

    /// <summary>The broker and git arguments for authenticated calls in one deployment.</summary>
    private sealed class GitAuthContext : IAsyncDisposable
    {
        private GitAuthContext(GitCredentialBroker broker, GitSandbox sandbox, string remoteName, string[] arguments, string? shellDirectory)
        {
            Broker = broker;
            Sandbox = sandbox;
            RemoteName = remoteName;
            Arguments = arguments;
            ShellDirectory = shellDirectory;
        }

        internal GitCredentialBroker Broker { get; }

        internal GitSandbox Sandbox { get; }

        internal string RemoteName { get; }

        /// <summary>The <c>-c</c> options that precede the subcommand of every authenticated call.</summary>
        internal string[] Arguments { get; }

        /// <summary>Windows: the directory of the expected shell, put first on PATH (see <see cref="ResolveShells"/>).</summary>
        internal string? ShellDirectory { get; }

        internal static async Task<GitAuthContext> StartAsync(
            string gitPath,
            GitSandbox sandbox,
            string workingDir,
            string token,
            string remoteName,
            string canonicalUrl,
            TimeSpan timeout,
            ILogger? logger,
            CancellationToken ct)
        {
            var policy = TransportPolicyOverride.Value ?? GitTransportPolicy.GitHub;
            var (self, assembly) = policy.Self ?? ResolveSelf();
            var execPath = await RunGitAndCaptureAsync(sandbox, gitPath, workingDir, timeout, ct, "--exec-path");
            var (shells, shellDirectory) = ResolveShells(execPath);
            var expectations = new GitCredentialPeerExpectations(self, assembly, ResolveRemoteHelpers(execPath), shells);
            uint? uid = null;
            if (!OperatingSystem.IsWindows())
            {
                uid = ProcessInspector.TryGetCurrentUserId(out var current)
                    ? current
                    : throw new GitException("Unable to determine the current user for the git credential broker.");
            }

            var broker = GitCredentialBroker.Start(
                token,
                new GitCredentialPeerPolicy(expectations, policy.Target, policy.ProcessQuery ?? SystemGitCredentialProcessQuery.Instance, uid),
                logger);
            var helper = BuildCredentialHelperCommand(self, assembly, broker.Endpoint);
            var arguments = new List<string>
            {
                "-c", "credential.helper=",
                "-c", $"credential.helper={helper}",
                "-c", "core.askPass=",
                "-c", "credential.useHttpPath=false",
                "-c", "protocol.allow=never",
            };
            foreach (var protocol in policy.Protocols)
            {
                arguments.AddRange(["-c", $"protocol.{protocol}.allow=always"]);
            }

            // The URL-specific value outranks any http.<url>.sslVerify that slipped into local config.
            arguments.AddRange(["-c", "http.sslVerify=true", "-c", $"http.{canonicalUrl}.sslVerify=true"]);
            return new GitAuthContext(broker, sandbox, remoteName, [.. arguments], shellDirectory);
        }

        public ValueTask DisposeAsync() => Broker.DisposeAsync();
    }

    /// <summary>The broker's socket path and directory, redacted from deploy errors.</summary>
    private static string?[] CredentialPaths(GitAuthContext? auth) => [auth?.Broker.Endpoint, auth?.Broker.Directory];

    private static async Task StopCredentialBrokerAsync(GitAuthContext? auth)
    {
        if (auth is not null)
        {
            await auth.DisposeAsync();
        }
    }

    /// <summary>
    /// git runs a <c>!</c> helper through <c>sh -c "&lt;value&gt; get"</c>; a quoted value without
    /// <c>!</c> would be read as <c>git credential-&lt;value&gt;</c>, so the shell form is required.
    /// </summary>
    internal static string BuildCredentialHelperCommand(string executable, string? assembly, string endpoint)
    {
        var parts = new List<string> { ShellQuote(executable) };
        if (assembly is not null)
        {
            parts.Add(ShellQuote(assembly));
        }

        parts.Add(GitCredentialPeerPolicy.HelperCommand);
        parts.Add(ShellQuote(endpoint));
        return "!" + string.Join(' ', parts);
    }

    /// <summary>POSIX single quoting; git for Windows runs helpers with its MSYS sh too.</summary>
    internal static string ShellQuote(string value) => "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    /// <summary>
    /// The canonical path of the running Bukit, or of <c>dotnet</c> plus <c>bukit.dll</c> when
    /// Bukit runs from its dll (plan 3.2).
    /// </summary>
    private static (string Executable, string? Assembly) ResolveSelf()
    {
        var processPath = Environment.ProcessPath
            ?? throw new GitException("Unable to locate the running Bukit executable for the git credential helper.");
        var executable = ProcessInspector.TryRealPath(processPath)
            ?? throw new GitException("Unable to resolve the running Bukit executable for the git credential helper.");
        if (!string.Equals(Path.GetFileNameWithoutExtension(executable), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            return (executable, null);
        }

        // Under dotnet, bukit.dll sits in the application base directory (Assembly.Location is
        // empty in single-file and AOT builds, which never run under dotnet).
        var location = Path.Combine(AppContext.BaseDirectory, "bukit.dll");
        var assembly = File.Exists(location) ? ProcessInspector.TryRealPath(location) : null;
        return assembly is null
            ? throw new GitException("Unable to locate bukit.dll for the git credential helper.")
            : (executable, assembly);
    }

    private static HashSet<string> ResolveRemoteHelpers(string execPath)
    {
        var helpers = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var suffix = OperatingSystem.IsWindows() ? ".exe" : string.Empty;
        // Apple Git links git-remote-https to git-remote-http; the process image is the target.
        foreach (var name in new[] { "git-remote-https", "git-remote-http" })
        {
            var candidate = Path.Combine(execPath, name + suffix);
            if (File.Exists(candidate) && ProcessInspector.TryRealPath(candidate) is { } real)
            {
                helpers.Add(real);
            }
        }

        return helpers.Count > 0
            ? helpers
            : throw new GitException("git's HTTPS remote helper was not found in its exec path.");
    }

    /// <summary>
    /// The shells git may leave between its remote helper and the credential helper, and on
    /// Windows the directory to put first on PATH for authenticated calls.
    /// </summary>
    /// <remarks>
    /// Git for Windows does not run <c>/bin/sh</c>: it starts the <c>!</c> helper through
    /// <c>sh</c> found on PATH, so a Cygwin or MSYS2 <c>sh</c> earlier on PATH would become the
    /// helper's parent. Accepting whatever <c>sh</c> PATH names would let any program on PATH
    /// sit between git and the token, so the expectation stays fixed to the shells inside the
    /// installation that owns <c>git --exec-path</c>, and authenticated calls get that
    /// installation's shell directory first on PATH so git starts exactly that shell. Without
    /// such a shell the deploy fails closed instead of accepting another one.
    /// </remarks>
    private static (HashSet<string> Shells, string? ShellDirectory) ResolveShells(string execPath)
    {
        var shells = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        if (!OperatingSystem.IsWindows())
        {
            if (ProcessInspector.TryRealPath("/bin/sh") is { } sh)
            {
                shells.Add(sh);
            }

            return (shells, null);
        }

        var candidates = WindowsGitShellCandidates(execPath, File.Exists)
            ?? throw new GitException("git's exec path is not in a recognised Git for Windows layout (<git>\\mingw64\\libexec\\git-core); refusing to run the deploy credential helper.");
        string? shellDirectory = null;
        foreach (var candidate in candidates)
        {
            if (ProcessInspector.TryRealPath(candidate) is { } real)
            {
                shells.Add(real);
                shellDirectory ??= Path.GetDirectoryName(candidate);
            }
        }

        return shellDirectory is null
            ? throw new GitException("Git for Windows' own sh.exe was not found in its installation; it is needed to run the deploy credential helper.")
            : (shells, shellDirectory);
    }

    private static readonly string[] WindowsGitPlatformDirectories = ["mingw64", "clangarm64", "mingw32"];

    /// <summary>
    /// The existing shells of the Git for Windows installation that owns <paramref name="execPath"/>,
    /// or null when the path is not <c>&lt;root&gt;\{mingw64|clangarm64|mingw32}\libexec\git-core</c>
    /// with a git marker in that root. The root is never searched for upwards: a Git without
    /// <c>usr\bin</c> (MinGit) would otherwise reach a user-writable directory such as
    /// <c>C:\usr\bin</c>, whose shell would then be trusted and put first on PATH.
    /// </summary>
    internal static IReadOnlyList<string>? WindowsGitShellCandidates(string execPath, Func<string, bool> fileExists)
    {
        ArgumentNullException.ThrowIfNull(fileExists);
        var gitCore = new DirectoryInfo(Path.TrimEndingDirectorySeparator(Path.GetFullPath(execPath)));
        var libexec = gitCore.Parent;
        var platform = libexec?.Parent;
        var root = platform?.Parent;
        if (root is null ||
            !gitCore.Name.Equals("git-core", StringComparison.OrdinalIgnoreCase) ||
            !libexec!.Name.Equals("libexec", StringComparison.OrdinalIgnoreCase) ||
            !WindowsGitPlatformDirectories.Contains(platform!.Name, StringComparer.OrdinalIgnoreCase) ||
            !(fileExists(Path.Combine(root.FullName, "cmd", "git.exe")) || fileExists(Path.Combine(platform.FullName, "bin", "git.exe"))))
        {
            return null;
        }

        var candidates = new List<string>();
        foreach (var directory in new[] { Path.Combine(root.FullName, "usr", "bin"), Path.Combine(root.FullName, "bin") })
        {
            foreach (var name in new[] { "sh.exe", "bash.exe" })
            {
                var candidate = Path.Combine(directory, name);
                if (fileExists(candidate))
                {
                    candidates.Add(candidate);
                }
            }
        }

        return candidates;
    }

    /// <summary>
    /// Refuses authentication when the temporary repository's config holds anything Bukit did
    /// not write (plan 3.5): a hook or another program could have added an http proxy or CA.
    /// </summary>
    private static async Task EnsureLocalConfigAllowedAsync(string gitPath, GitAuthContext auth, string workingDir, TimeSpan timeout, CancellationToken ct)
    {
        var result = await RunGitAllowFailureAsync(auth.Sandbox, gitPath, workingDir, timeout, ct, "config", "--local", "--list", "--null");
        if (result.ExitCode != 0)
        {
            throw new GitException($"git config --local --list failed (exit {result.ExitCode}): {result.Stderr.Trim()}");
        }

        foreach (var record in result.Stdout.Split('\0'))
        {
            if (record.Length == 0)
            {
                continue;
            }

            var separator = record.IndexOf('\n');
            var key = separator < 0 ? record : record[..separator];
            if (AllowedLocalConfigKeys.Contains(key) ||
                string.Equals(key, $"remote.{auth.RemoteName}.url", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, $"remote.{auth.RemoteName}.fetch", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Only the section is named: the rest of a key can carry a URL with credentials.
            var section = key.Split('.')[0];
            throw new GitException($"Refusing to authenticate: the temporary deploy repository has an unexpected '{section}.*' setting.");
        }
    }
}
