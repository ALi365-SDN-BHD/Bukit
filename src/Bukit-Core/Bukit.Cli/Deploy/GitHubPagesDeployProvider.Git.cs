using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Bukit.Cli.Deploy;

public sealed partial class GitHubPagesDeployProvider
{
    private static async Task EnsureGitIdentityAsync(string gitPath, string tempDir, TimeSpan gitCommandTimeout, CancellationToken ct)
    {
        var hasName = false;
        var hasEmail = false;

        try
        {
            var name = await RunGitAndCaptureAsync(gitPath, tempDir, gitCommandTimeout, ct, "config", "--local", "user.name");
            hasName = !string.IsNullOrWhiteSpace(name);
        }
        catch
        {
        }

        try
        {
            if (!hasName)
            {
                var globalName = await RunGitAndCaptureAsync(gitPath, tempDir, gitCommandTimeout, ct, "config", "--global", "user.name");
                hasName = !string.IsNullOrWhiteSpace(globalName);
            }
        }
        catch
        {
        }

        try
        {
            var email = await RunGitAndCaptureAsync(gitPath, tempDir, gitCommandTimeout, ct, "config", "--local", "user.email");
            hasEmail = !string.IsNullOrWhiteSpace(email);
        }
        catch
        {
        }

        try
        {
            if (!hasEmail)
            {
                var globalEmail = await RunGitAndCaptureAsync(gitPath, tempDir, gitCommandTimeout, ct, "config", "--global", "user.email");
                hasEmail = !string.IsNullOrWhiteSpace(globalEmail);
            }
        }
        catch
        {
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

    private static string? ResolveGit()
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
                proc.WaitForExit(3000);
                var output = proc.StandardOutput.ReadToEnd().Trim();
                if (!string.IsNullOrWhiteSpace(output) && File.Exists(output))
                {
                    return output;
                }
            }
        }
        catch
        {
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
        try
        {
            var output = await RunGitAuthAndCaptureAsync(gitPath, token, askpassScript, null, gitCommandTimeout, ct, "ls-remote", "--heads", remoteUrl, $"refs/heads/{branch}");
            return !string.IsNullOrWhiteSpace(output);
        }
        catch (GitTimeoutException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private static async Task RunGitAuthAsync(string gitPath, string token, string askpassScript, string? workingDir, TimeSpan gitCommandTimeout, CancellationToken ct, params string[] args)
    {
        var commandLine = string.Join(' ', args);
        var psi = CreateGitProcess(gitPath, workingDir, args);
        psi.Environment["GIT_ASKPASS"] = askpassScript;
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
        psi.Environment[AskpassTokenEnvironmentVariable] = token;

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start git process.");
        await WaitForGitProcessAsync(proc, gitCommandTimeout, ct, commandLine);

        if (proc.ExitCode != 0)
        {
            var error = await proc.StandardError.ReadToEndAsync();
            throw new GitException($"git {commandLine} failed (exit {proc.ExitCode}): {error.Trim()}");
        }
    }

    private static async Task<string> RunGitAuthAndCaptureAsync(string gitPath, string token, string askpassScript, string? workingDir, TimeSpan gitCommandTimeout, CancellationToken ct, params string[] args)
    {
        var commandLine = string.Join(' ', args);
        var psi = CreateGitProcess(gitPath, workingDir, args);
        psi.Environment["GIT_ASKPASS"] = askpassScript;
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
        psi.Environment[AskpassTokenEnvironmentVariable] = token;

        using var proc = Process.Start(psi);
        if (proc is null)
        {
            return string.Empty;
        }

        await WaitForGitProcessAsync(proc, gitCommandTimeout, ct, commandLine);
        var output = await proc.StandardOutput.ReadToEndAsync();
        var error = await proc.StandardError.ReadToEndAsync();
        if (proc.ExitCode != 0)
        {
            throw new GitException($"git {commandLine} failed (exit {proc.ExitCode}): {error.Trim()}");
        }

        return output.Trim();
    }

    private static async Task RunGitAsync(string gitPath, string? workingDir, TimeSpan gitCommandTimeout, CancellationToken ct, params string[] args)
    {
        var commandLine = string.Join(' ', args);
        var psi = CreateGitProcess(gitPath, workingDir, args);

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start git process.");
        await WaitForGitProcessAsync(proc, gitCommandTimeout, ct, commandLine);

        if (proc.ExitCode != 0)
        {
            var error = await proc.StandardError.ReadToEndAsync();
            throw new GitException($"git {commandLine} failed (exit {proc.ExitCode}): {error.Trim()}");
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

        await WaitForGitProcessAsync(proc, gitCommandTimeout, ct, commandLine);
        var output = await proc.StandardOutput.ReadToEndAsync();
        var error = await proc.StandardError.ReadToEndAsync();
        if (proc.ExitCode != 0)
        {
            throw new GitException($"git {commandLine} failed (exit {proc.ExitCode}): {error.Trim()}");
        }

        return output.Trim();
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

                try
                {
                    await proc.WaitForExitAsync(CancellationToken.None);
                }
                catch
                {
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

            try
            {
                await proc.WaitForExitAsync(CancellationToken.None);
            }
            catch
            {
            }

            if (isTimeout)
            {
                throw new GitTimeoutException(
                    $"Git command timed out during GitHub Pages deployment after {timeout.TotalSeconds:0} seconds. " +
                    "Check network connectivity and GitHub availability, or set BUKIT_DEPLOY_GIT_TIMEOUT_SECONDS to a larger value.",
                    commandLine,
                    timeout);
            }

            if (ct.IsCancellationRequested)
            {
                throw new OperationCanceledException(ct);
            }

            throw;
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
        catch (Exception)
        {
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

    private sealed class GitTimeoutException(string message, string commandLine, TimeSpan timeout) : Exception(message)
    {
        public string CommandLine { get; } = commandLine;
        public TimeSpan Timeout { get; } = timeout;
    }

    private sealed record RepoInfo(string Owner, string RepoName);
}
