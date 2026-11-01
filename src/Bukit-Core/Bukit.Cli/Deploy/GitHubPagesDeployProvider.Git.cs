using System.Diagnostics;
using System.Text.RegularExpressions;
using Bukit.Shared;

namespace Bukit.Cli.Deploy;

public sealed partial class GitHubPagesDeployProvider
{
    private static readonly TimeSpan GitTerminationGracePeriod = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan GitResolveProbeTimeout = TimeSpan.FromSeconds(3);
    private const int GitStreamByteCap = 4 * 1024 * 1024;
    private const int GitDiagnosticHeadBytes = 32 * 1024;
    private const int GitDiagnosticTailBytes = 32 * 1024;
    private static readonly System.Text.Encoding GitUtf8NoBom = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private static async Task EnsureGitIdentityAsync(string gitPath, string tempDir, TimeSpan gitCommandTimeout, CancellationToken ct, ILogger? logger = null)
    {
        var hasName = false;
        var hasEmail = false;

        try
        {
            var name = await RunGitAndCaptureAsync(gitPath, tempDir, gitCommandTimeout, ct, "config", "--local", "user.name");
            hasName = !string.IsNullOrWhiteSpace(name);
        }
        catch (Exception ex)
        {
            logger?.Debug($"event=git.config.read.failed key=user.name scope=local reason={ex.Message}");
        }

        try
        {
            if (!hasName)
            {
                var globalName = await RunGitAndCaptureAsync(gitPath, tempDir, gitCommandTimeout, ct, "config", "--global", "user.name");
                hasName = !string.IsNullOrWhiteSpace(globalName);
            }
        }
        catch (Exception ex)
        {
            logger?.Debug($"event=git.config.read.failed key=user.name scope=global reason={ex.Message}");
        }

        try
        {
            var email = await RunGitAndCaptureAsync(gitPath, tempDir, gitCommandTimeout, ct, "config", "--local", "user.email");
            hasEmail = !string.IsNullOrWhiteSpace(email);
        }
        catch (Exception ex)
        {
            logger?.Debug($"event=git.config.read.failed key=user.email scope=local reason={ex.Message}");
        }

        try
        {
            if (!hasEmail)
            {
                var globalEmail = await RunGitAndCaptureAsync(gitPath, tempDir, gitCommandTimeout, ct, "config", "--global", "user.email");
                hasEmail = !string.IsNullOrWhiteSpace(globalEmail);
            }
        }
        catch (Exception ex)
        {
            logger?.Debug($"event=git.config.read.failed key=user.email scope=global reason={ex.Message}");
        }

        if (!hasName)
        {
            await RunGitAsync(gitPath, tempDir, gitCommandTimeout, ct, "config", "--local", "user.name", "bukit");
        }

        if (!hasEmail)
        {
            await RunGitAsync(gitPath, tempDir, gitCommandTimeout, ct, "config", "--local", "user.email", "bukit@deploy.local");
        }
    }

    private static string? ResolveGit(ILogger? logger = null)
    {
        var paths = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var names = OperatingSystem.IsWindows() ? new[] { "git.exe", "git.cmd" } : new[] { "git" };
        foreach (var dir in paths.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var name in names)
            {
                var full = Path.Combine(dir.Trim(), name);
                if (File.Exists(full))
                {
                    return full;
                }
            }
        }

        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "which",
                Arguments = "git",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });
            if (proc is not null)
            {
                using var stdoutCollector = new BoundedGitCollector(GitStreamByteCap);
                using var stderrCollector = new BoundedGitCollector(GitStreamByteCap);
                Task stdoutTask = stdoutCollector.ReadAsync(proc.StandardOutput.BaseStream, proc);
                Task stderrTask = stderrCollector.ReadAsync(proc.StandardError.BaseStream, proc);

                bool exited = proc.WaitForExit((int)GitResolveProbeTimeout.TotalMilliseconds);
                if (!exited)
                {
                    TryKillProcessTree(proc);
                    ObserveGitOutputTasksAsync(stdoutTask, stderrTask).GetAwaiter().GetResult();
                    return null;
                }

                var drainCompleted = WaitForTerminationGraceAsync(
                    Task.WhenAll(stdoutTask, stderrTask),
                    GitTerminationGracePeriod).GetAwaiter().GetResult();
                if (!drainCompleted || stdoutCollector.Exceeded || stderrCollector.Exceeded)
                {
                    return null;
                }

                var output = stdoutCollector.GetText().Trim();
                if (!string.IsNullOrWhiteSpace(output) && File.Exists(output))
                {
                    return output;
                }
            }
        }
        catch (Exception ex)
        {
            logger?.Debug($"event=git.resolve.failed method=which reason={ex.Message}");
        }

        return null;
    }

    private static async Task<RepoInfo?> GetRepoInfoAsync(string gitPath, string projectRoot, TimeSpan gitCommandTimeout, CancellationToken ct)
    {
        if (!IsInsideRepository(projectRoot))
        {
            // Not a repository at all: no project-scoped identity can exist.
            return null;
        }

        var local = await RunGitAllowFailureAsync(gitPath, projectRoot, gitCommandTimeout, ct,
            "config", "--local", "--includes", "--null", "--get-all", "remote.origin.url");
        if (local.ExitCode is not (0 or 1))
        {
            throw new GitException($"git config remote.origin.url failed (exit {local.ExitCode}): {local.Stderr.Trim()}");
        }

        // A distinct worktree scope exists only when the repository enables it. Without the
        // extension, --worktree is the local scope (and errors in linked worktrees).
        var worktreeConfig = await RunGitAllowFailureAsync(gitPath, projectRoot, gitCommandTimeout, ct,
            "config", "--local", "--bool", "--get", "extensions.worktreeConfig");
        if (worktreeConfig.ExitCode is not (0 or 1))
        {
            throw new GitException($"git config extensions.worktreeConfig failed (exit {worktreeConfig.ExitCode}): {worktreeConfig.Stderr.Trim()}");
        }

        var selected = local;
        if (worktreeConfig.ExitCode == 0 &&
            string.Equals(worktreeConfig.Stdout.Trim(), "true", StringComparison.OrdinalIgnoreCase))
        {
            var scoped = await RunGitAllowFailureAsync(gitPath, projectRoot, gitCommandTimeout, ct,
                "config", "--worktree", "--includes", "--null", "--get-all", "remote.origin.url");
            if (scoped.ExitCode is not (0 or 1))
            {
                throw new GitException($"git config remote.origin.url failed (exit {scoped.ExitCode}): {scoped.Stderr.Trim()}");
            }

            if (scoped.ExitCode == 0)
            {
                selected = scoped;
            }
        }

        if (selected.ExitCode == 1)
        {
            return null;
        }

        // Git fetch URL semantics use the first value; an empty or invalid first value wins
        // over later values and must not fall through to lower scopes.
        var first = FirstNulValue(selected.Stdout);
        if (string.IsNullOrWhiteSpace(first) || !TryParseGitHubRemoteUrl(first, out var repoInfo))
        {
            return null;
        }

        return repoInfo;
    }

    private static string? FirstNulValue(string value)
    {
        var end = value.IndexOf('\0');
        return end < 0 ? value : value[..end];
    }

    private static bool IsInsideRepository(string projectRoot)
    {
        for (var current = new DirectoryInfo(Path.GetFullPath(projectRoot)); current is not null; current = current.Parent)
        {
            var dotGit = Path.Combine(current.FullName, ".git");
            if (Directory.Exists(dotGit) || File.Exists(dotGit))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryParseGitHubRemoteUrl(string remoteUrl, out RepoInfo? repoInfo)
    {
        repoInfo = null;
        var url = remoteUrl.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        var scpMatch = Regex.Match(
            url,
            @"^git@github\.com:(?<owner>[^/\s]+)/(?<repo>[^/\s]+?)(?:\.git)?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (scpMatch.Success)
        {
            repoInfo = new RepoInfo(scpMatch.Groups["owner"].Value, scpMatch.Groups["repo"].Value);
            return true;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parts = uri.AbsolutePath
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        var owner = parts[0];
        var repo = parts[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? parts[1][..^4]
            : parts[1];

        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo))
        {
            return false;
        }

        repoInfo = new RepoInfo(owner, repo);
        return true;
    }

    private static async Task<bool> RemoteBranchExistsAsync(string gitPath, string token, string askpassScript, string workingDir, string remoteName, string branch, TimeSpan gitCommandTimeout, CancellationToken ct)
    {
        var output = await RunGitAuthAndCaptureAsync(gitPath, token, askpassScript, workingDir, gitCommandTimeout, ct, "ls-remote", "--heads", remoteName, $"refs/heads/{branch}");
        return !string.IsNullOrWhiteSpace(output);
    }

    private static async Task RunGitAuthAsync(string gitPath, string token, string askpassScript, string? workingDir, TimeSpan gitCommandTimeout, CancellationToken ct, params string[] args)
    {
        var commandLine = string.Join(' ', args);
        var psi = CreateGitProcess(gitPath, workingDir, args);
        psi.Environment["GIT_ASKPASS"] = askpassScript;
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
        psi.Environment[AskpassTokenEnvironmentVariable] = token;

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start git process.");
        var output = await WaitForGitProcessAndDrainAsync(proc, gitCommandTimeout, ct, commandLine);

        if (proc.ExitCode != 0)
        {
            throw new GitException($"git {commandLine} failed (exit {proc.ExitCode}): {output.Stderr.Trim()}");
        }
    }

    private static async Task<string> RunGitAuthAndCaptureAsync(string gitPath, string token, string askpassScript, string? workingDir, TimeSpan gitCommandTimeout, CancellationToken ct, params string[] args)
    {
        var commandLine = string.Join(' ', args);
        var psi = CreateGitProcess(gitPath, workingDir, args);
        psi.Environment["GIT_ASKPASS"] = askpassScript;
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
        psi.Environment[AskpassTokenEnvironmentVariable] = token;

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start git process.");

        var output = await WaitForGitProcessAndDrainAsync(proc, gitCommandTimeout, ct, commandLine);
        if (proc.ExitCode != 0)
        {
            throw new GitException($"git {commandLine} failed (exit {proc.ExitCode}): {output.Stderr.Trim()}");
        }

        return output.Stdout.Trim();
    }

    private static async Task RunGitAsync(string gitPath, string? workingDir, TimeSpan gitCommandTimeout, CancellationToken ct, params string[] args)
    {
        var commandLine = string.Join(' ', args);
        var psi = CreateGitProcess(gitPath, workingDir, args);

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start git process.");
        var output = await WaitForGitProcessAndDrainAsync(proc, gitCommandTimeout, ct, commandLine);

        if (proc.ExitCode != 0)
        {
            throw new GitException($"git {commandLine} failed (exit {proc.ExitCode}): {output.Stderr.Trim()}");
        }
    }

    private static async Task<string> RunGitAndCaptureAsync(string gitPath, string? workingDir, TimeSpan gitCommandTimeout, CancellationToken ct, params string[] args)
    {
        var commandLine = string.Join(' ', args);
        var psi = CreateGitProcess(gitPath, workingDir, args);

        using var proc = Process.Start(psi);
        if (proc is null)
        {
            return string.Empty;
        }

        var output = await WaitForGitProcessAndDrainAsync(proc, gitCommandTimeout, ct, commandLine);
        if (proc.ExitCode != 0)
        {
            throw new GitException($"git {commandLine} failed (exit {proc.ExitCode}): {output.Stderr.Trim()}");
        }

        return output.Stdout.Trim();
    }

    private sealed record GitCommandResult(int ExitCode, string Stdout, string Stderr);

    private static async Task<GitCommandResult> RunGitAllowFailureAsync(
        string gitPath,
        string? workingDir,
        TimeSpan timeout,
        CancellationToken ct,
        params string[] args)
    {
        var commandLine = string.Join(' ', args);
        var psi = CreateGitProcess(gitPath, workingDir, args);
        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start git process.");
        var output = await WaitForGitProcessAndDrainAsync(proc, timeout, ct, commandLine);
        return new GitCommandResult(proc.ExitCode, output.Stdout, output.Stderr);
    }

    /// <summary>
    /// Runs in the final transfer config context: the temporary repository already exists and
    /// its private remote is configured, so conditional includes activated by those writes are
    /// visible before any branch query, fetch or push.
    /// </summary>
    private static async Task EnsureTransportTargetAsync(
        string gitPath,
        string workingDir,
        string remoteName,
        string canonicalUrl,
        TimeSpan timeout,
        CancellationToken ct)
    {
        var rewrites = await RunGitAllowFailureAsync(gitPath, workingDir, timeout, ct,
            "config", "--includes", "--null", "--get-regexp", "^url\\..*\\.(insteadof|pushinsteadof)$");
        if (rewrites.ExitCode == 0)
        {
            foreach (var record in rewrites.Stdout.Split('\0'))
            {
                if (record.Length == 0)
                {
                    continue;
                }

                var separator = record.IndexOf('\n');
                var rewriteValue = separator < 0 ? string.Empty : record[(separator + 1)..];
                if (canonicalUrl.StartsWith(rewriteValue, StringComparison.Ordinal))
                {
                    // Never echo the raw key/base URL: it can carry another service's
                    // userinfo credentials.
                    throw new GitException(
                        "Refusing to deploy because an active URL rewrite would change the GitHub Pages transport target. " +
                        "Remove the matching insteadOf/pushInsteadOf rule from local, worktree, included, global or system Git config.");
                }
            }
        }
        else if (rewrites.ExitCode != 1)
        {
            throw new GitException($"git config URL rewrite query failed (exit {rewrites.ExitCode}): {rewrites.Stderr.Trim()}");
        }

        foreach (var key in new[] { $"remote.{remoteName}.url", $"remote.{remoteName}.pushurl" })
        {
            var values = await RunGitAllowFailureAsync(gitPath, workingDir, timeout, ct,
                "config", "--local", "--includes", "--null", "--get-all", key);
            if (values.ExitCode == 1)
            {
                if (key.EndsWith(".url", StringComparison.Ordinal))
                {
                    throw new GitException($"Private deploy remote is missing its canonical URL: {key}");
                }

                continue;
            }

            if (values.ExitCode != 0)
            {
                throw new GitException($"git config {key} query failed (exit {values.ExitCode}): {values.Stderr.Trim()}");
            }

            foreach (var url in values.Stdout.Split('\0'))
            {
                if (url.Length > 0 && !string.Equals(url, canonicalUrl, StringComparison.Ordinal))
                {
                    throw new GitException($"Private deploy remote has an extra or different transport URL for {key}; refusing to deploy.");
                }
            }
        }
    }

    private static async Task<GitProcessOutput> WaitForGitProcessAndDrainAsync(
        Process process,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        string commandLine)
    {
        using var stdoutCollector = new BoundedGitCollector(GitStreamByteCap);
        using var stderrCollector = new BoundedGitCollector(GitStreamByteCap);
        Task stdoutTask = stdoutCollector.ReadAsync(process.StandardOutput.BaseStream, process);
        Task stderrTask = stderrCollector.ReadAsync(process.StandardError.BaseStream, process);

        try
        {
            await WaitForGitProcessAsync(process, timeout, cancellationToken, commandLine);
        }
        catch
        {
            await ObserveGitOutputTasksAsync(stdoutTask, stderrTask);
            throw;
        }

        // Bounded drain: even on normal exit, a grandchild may hold the pipe
        var drainCompleted = await WaitForTerminationGraceAsync(
            Task.WhenAll(stdoutTask, stderrTask),
            GitTerminationGracePeriod);
        if (!drainCompleted)
        {
            Console.Error.WriteLine(
                $"Deploy: git output drain did not complete within {GitTerminationGracePeriod.TotalSeconds:0} seconds.");
        }

        var stdout = stdoutCollector.Seal();
        var stderr = stderrCollector.Seal();
        if (stdout.Exceeded || stderr.Exceeded)
        {
            TryKillProcessTree(process);
            string stream = stdout.Exceeded ? "stdout" : "stderr";
            throw new GitException(
                $"git {commandLine} produced more than {GitStreamByteCap} bytes on {stream}.");
        }

        return new GitProcessOutput(stdout.GetText(), stderrCollector.GetDiagnosticText());
    }

    private static async Task ObserveGitOutputTasksAsync(
        Task stdoutTask,
        Task stderrTask)
    {
        var completed = await WaitForTerminationGraceAsync(
            ObserveGitOutputTasksIgnoringFailureAsync(stdoutTask, stderrTask),
            GitTerminationGracePeriod);
        if (!completed)
        {
            Console.Error.WriteLine(
                $"Deploy: git output drain did not complete within {GitTerminationGracePeriod.TotalSeconds:0} seconds.");
        }
    }

    private static async Task ObserveGitOutputTasksIgnoringFailureAsync(
        Task stdoutTask,
        Task stderrTask)
    {
        try
        {
            await Task.WhenAll(stdoutTask, stderrTask);
        }
        catch
        {
        }
    }

    private static async Task WaitForGitProcessAsync(Process proc, TimeSpan timeout, CancellationToken ct, string commandLine)
    {
        if (timeout == Timeout.InfiniteTimeSpan)
        {
            try
            {
                await proc.WaitForExitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                TryKillProcessTree(proc);
                var terminationCompleted = await WaitForTerminationGraceAsync(
                    WaitForGitExitIgnoringFailureAsync(proc),
                    GitTerminationGracePeriod);
                if (!terminationCompleted)
                {
                    Console.Error.WriteLine(
                        $"Deploy: git process termination did not complete within {GitTerminationGracePeriod.TotalSeconds:0} seconds.");
                }

                throw new OperationCanceledException(ct);
            }

            return;
        }

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            await proc.WaitForExitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            var isTimeout = timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested;
            TryKillProcessTree(proc);
            var terminationCompleted = await WaitForTerminationGraceAsync(
                WaitForGitExitIgnoringFailureAsync(proc),
                GitTerminationGracePeriod);
            if (!terminationCompleted)
            {
                Console.Error.WriteLine(
                    $"Deploy: git process termination did not complete within {GitTerminationGracePeriod.TotalSeconds:0} seconds.");
            }

            if (isTimeout)
            {
                throw new GitTimeoutException(
                    $"Git command timed out during GitHub Pages deployment after {timeout.TotalSeconds:0} seconds. " +
                    "Check network connectivity and GitHub availability, or set BUKIT_DEPLOY_GIT_TIMEOUT_SECONDS to a larger value." +
                    (terminationCompleted
                        ? string.Empty
                        : $" Process termination did not complete within {GitTerminationGracePeriod.TotalSeconds:0} seconds."),
                    commandLine,
                    timeout);
            }

            ct.ThrowIfCancellationRequested();

            throw;
        }
    }

    private static async Task WaitForGitExitIgnoringFailureAsync(Process process)
    {
        try
        {
            await process.WaitForExitAsync(CancellationToken.None);
        }
        catch
        {
        }
    }

    internal static async Task<bool> WaitForTerminationGraceAsync(
        Task completion,
        TimeSpan gracePeriod)
    {
        try
        {
            await completion.WaitAsync(gracePeriod);
            return true;
        }
        catch (TimeoutException)
        {
            _ = ObserveGitTaskEventuallyAsync(completion);
            return false;
        }
    }

    private static async Task ObserveGitTaskEventuallyAsync(Task task)
    {
        try
        {
            await task;
        }
        catch
        {
        }
    }

    private static void TryKillProcessTree(Process proc)
    {
        try
        {
            if (!proc.HasExited)
            {
                proc.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Deploy: failed to kill git process tree pid={proc.Id} reason={ex.GetType().Name}: {ex.Message}");
        }
    }

    private static readonly string[] InheritedRepositoryEnvironmentVariables =
    [
        "GIT_DIR",
        "GIT_WORK_TREE",
        "GIT_COMMON_DIR",
        "GIT_INDEX_FILE",
        "GIT_OBJECT_DIRECTORY",
        "GIT_ALTERNATE_OBJECT_DIRECTORIES",
        "GIT_IMPLICIT_WORK_TREE",
        "GIT_GRAFT_FILE",
        "GIT_NO_REPLACE_OBJECTS",
        "GIT_REPLACE_REF_BASE",
        "GIT_PREFIX",
        "GIT_INTERNAL_SUPER_PREFIX",
        "GIT_SHALLOW_FILE",
        "GIT_CONFIG",
        "GIT_CONFIG_PARAMETERS",
        "GIT_CONFIG_COUNT"
    ];

    private static ProcessStartInfo CreateGitProcess(string gitPath, string? workingDir, string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = gitPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        if (workingDir is not null)
        {
            psi.WorkingDirectory = workingDir;
        }

        // Child git processes must never be redirected to another repository, index, object
        // store, or runtime config. Normal credential/proxy/SSH settings stay inherited.
        foreach (var name in InheritedRepositoryEnvironmentVariables)
        {
            psi.Environment.Remove(name);
        }

        foreach (var name in psi.Environment.Keys
                     .Where(static name =>
                         name.StartsWith("GIT_CONFIG_KEY_", StringComparison.Ordinal) ||
                         name.StartsWith("GIT_CONFIG_VALUE_", StringComparison.Ordinal))
                     .ToArray())
        {
            psi.Environment.Remove(name);
        }

        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        return psi;
    }

    private sealed class GitException(string message) : Exception(message)
    {
    }

    private sealed record GitProcessOutput(string Stdout, string Stderr);

    internal sealed class BoundedGitCollector : IDisposable
    {
        private readonly int _maxBytes;
        private readonly MemoryStream _buffer;
        private readonly object _gate = new();
        private long _totalBytesRead;
        private bool _exceeded;
        private bool _sealed;
        private bool _disposed;

        public BoundedGitCollector(int maxBytes)
        {
            _maxBytes = maxBytes;
            _buffer = new MemoryStream(capacity: Math.Min(maxBytes, 4096));
        }

        public bool Exceeded
        {
            get
            {
                lock (_gate)
                {
                    return _exceeded;
                }
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _sealed = true;
                _disposed = true;
                _buffer.Dispose();
            }
        }

        public async Task ReadAsync(Stream stream, Process process)
        {
            var readBuffer = new byte[4096];
            while (true)
            {
                int bytesRead = await stream.ReadAsync(readBuffer, CancellationToken.None);
                if (bytesRead == 0) break;

                var exceeded = false;
                lock (_gate)
                {
                    if (_sealed) return;

                    _totalBytesRead += bytesRead;
                    int space = _maxBytes - (int)_buffer.Length;
                    if (space > 0)
                    {
                        int toWrite = Math.Min(bytesRead, space);
                        _buffer.Write(readBuffer, 0, toWrite);
                    }

                    if (_totalBytesRead > _maxBytes)
                    {
                        _exceeded = true;
                        _sealed = true;
                        exceeded = true;
                    }
                }

                if (exceeded)
                {
                    TryKillProcessTree(process);
                    return;
                }
            }
        }

        internal OutputSnapshot Seal()
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                _sealed = true;
                return new OutputSnapshot(_buffer.GetBuffer(), checked((int)_buffer.Length), _totalBytesRead, _exceeded);
            }
        }

        public string GetText() => Seal().GetText();

        public string GetDiagnosticText()
        {
            var snapshot = Seal();
            byte[] buffer = snapshot.Buffer;
            if (snapshot.Length <= GitDiagnosticHeadBytes + GitDiagnosticTailBytes)
            {
                return GitUtf8NoBom.GetString(buffer, 0, snapshot.Length);
            }

            var head = DecodeUtf8PrefixWithinByteLimit(buffer, 0, GitDiagnosticHeadBytes, GitDiagnosticHeadBytes);
            var tail = DecodeUtf8SuffixWithinByteLimit(buffer, snapshot.Length - GitDiagnosticTailBytes, GitDiagnosticTailBytes, GitDiagnosticTailBytes);
            return $"{head}\n... [truncated {snapshot.TotalBytesRead - GitDiagnosticHeadBytes - GitDiagnosticTailBytes} bytes] ...\n{tail}";
        }

        private static string DecodeUtf8PrefixWithinByteLimit(byte[] buffer, int offset, int length, int maxEmittedBytes)
        {
            var text = GitUtf8NoBom.GetString(buffer, offset, length);
            if (GitUtf8NoBom.GetByteCount(text) <= maxEmittedBytes)
            {
                return text;
            }

            var low = 0;
            var high = text.Length;
            while (low < high)
            {
                var middle = low + ((high - low + 1) / 2);
                if (GitUtf8NoBom.GetByteCount(text.AsSpan(0, middle)) <= maxEmittedBytes)
                {
                    low = middle;
                }
                else
                {
                    high = middle - 1;
                }
            }

            return text[..low];
        }

        private static string DecodeUtf8SuffixWithinByteLimit(byte[] buffer, int offset, int length, int maxEmittedBytes)
        {
            var text = GitUtf8NoBom.GetString(buffer, offset, length);
            if (GitUtf8NoBom.GetByteCount(text) <= maxEmittedBytes)
            {
                return text;
            }

            var low = 0;
            var high = text.Length;
            while (low < high)
            {
                var middle = low + ((high - low) / 2);
                if (GitUtf8NoBom.GetByteCount(text.AsSpan(middle)) <= maxEmittedBytes)
                {
                    high = middle;
                }
                else
                {
                    low = middle + 1;
                }
            }

            return text[high..];
        }

        internal sealed record OutputSnapshot(byte[] Buffer, int Length, long TotalBytesRead, bool Exceeded)
        {
            internal string GetText() => GitUtf8NoBom.GetString(Buffer, 0, Length);
        }
    }

    private sealed class GitTimeoutException(string message, string commandLine, TimeSpan timeout) : Exception(message)
    {
        public string CommandLine { get; } = commandLine;
        public TimeSpan Timeout { get; } = timeout;
    }

    private sealed record RepoInfo(string Owner, string RepoName);
}
