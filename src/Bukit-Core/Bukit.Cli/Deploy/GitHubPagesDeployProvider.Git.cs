using System.Diagnostics;
using System.Text.RegularExpressions;
using Bukit.Shared;

namespace Bukit.Cli.Deploy;

public sealed partial class GitHubPagesDeployProvider
{
    private static readonly TimeSpan GitTerminationGracePeriod = TimeSpan.FromSeconds(2);
    private const int GitStreamByteCap = 4 * 1024 * 1024;
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
                UseShellExecute = false
            });
            if (proc is not null)
            {
                bool exited = proc.WaitForExit(3000);
                if (!exited)
                {
                    TryKillProcessTree(proc);
                    return null;
                }
                var output = proc.StandardOutput.ReadToEnd().Trim();
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

    private static async Task<RepoInfo?> GetRepoInfoAsync(string gitPath, TimeSpan gitCommandTimeout, CancellationToken ct)
    {
        try
        {
            var url = await RunGitAndCaptureAsync(gitPath, null, gitCommandTimeout, ct, "remote", "get-url", "origin");
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            if (!TryParseGitHubRemoteUrl(url, out var repoInfo))
            {
                return null;
            }

            return repoInfo;
        }
        catch (GitTimeoutException)
        {
            throw;
        }
        catch
        {
            return null;
        }
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

    private static async Task<bool> RemoteBranchExistsAsync(string gitPath, string token, string askpassScript, string remoteUrl, string branch, TimeSpan gitCommandTimeout, CancellationToken ct)
    {
        var output = await RunGitAuthAndCaptureAsync(gitPath, token, askpassScript, null, gitCommandTimeout, ct, "ls-remote", "--heads", remoteUrl, $"refs/heads/{branch}");
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

    private static async Task<GitProcessOutput> WaitForGitProcessAndDrainAsync(
        Process process,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        string commandLine)
    {
        var stdoutCollector = new BoundedGitCollector(GitStreamByteCap);
        var stderrCollector = new BoundedGitCollector(GitStreamByteCap);
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

        await Task.WhenAll(stdoutTask, stderrTask);

        if (stdoutCollector.Exceeded || stderrCollector.Exceeded)
        {
            TryKillProcessTree(process);
            string stream = stdoutCollector.Exceeded ? "stdout" : "stderr";
            throw new GitException(
                $"git {commandLine} produced more than {GitStreamByteCap} bytes on {stream}.");
        }

        return new GitProcessOutput(stdoutCollector.GetText(), stderrCollector.GetText());
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

    private sealed class BoundedGitCollector : IDisposable
    {
        private readonly int _maxBytes;
        private readonly MemoryStream _buffer;
        private long _totalBytesRead;

        public BoundedGitCollector(int maxBytes)
        {
            _maxBytes = maxBytes;
            _buffer = new MemoryStream(capacity: Math.Min(maxBytes, 4096));
        }

        public bool Exceeded { get; private set; }

        public void Dispose() => _buffer.Dispose();

        public async Task ReadAsync(Stream stream, Process process)
        {
            var readBuffer = new byte[4096];
            while (true)
            {
                int bytesRead = await stream.ReadAsync(readBuffer, CancellationToken.None);
                if (bytesRead == 0) break;

                _totalBytesRead += bytesRead;
                int space = _maxBytes - (int)_buffer.Length;
                if (space > 0)
                {
                    int toWrite = Math.Min(bytesRead, space);
                    _buffer.Write(readBuffer, 0, toWrite);
                }

                if (_totalBytesRead > _maxBytes)
                {
                    Exceeded = true;
                    TryKillProcessTree(process);
                    return;
                }
            }
        }

        public string GetText() => GitUtf8NoBom.GetString(_buffer.ToArray());
    }

    private sealed class GitTimeoutException(string message, string commandLine, TimeSpan timeout) : Exception(message)
    {
        public string CommandLine { get; } = commandLine;
        public TimeSpan Timeout { get; } = timeout;
    }

    private sealed record RepoInfo(string Owner, string RepoName);
}
