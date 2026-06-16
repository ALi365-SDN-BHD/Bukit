using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Bukit.Shared;

namespace Bukit.Cli.Deploy;

public sealed class GitHubPagesDeployProvider : IDeployProvider
{
    public string Name => "github-pages";

    public async Task<DeployResult> DeployAsync(DeployContext context, CancellationToken ct)
    {
        var logger = context.Logger;

        var gitPath = ResolveGit();
        if (gitPath is null)
        {
            return new DeployResult { Success = false, Error = "git command not found. Please install git and ensure it is in PATH." };
        }

        if (!Directory.Exists(context.OutputDir))
        {
            return new DeployResult { Success = false, Error = $"Output directory not found: {context.OutputDir}" };
        }

        if (Directory.GetFiles(context.OutputDir, "*", SearchOption.AllDirectories).Length == 0)
        {
            return new DeployResult { Success = false, Error = $"Output directory is empty: {context.OutputDir}" };
        }

        var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            return new DeployResult { Success = false, Error = "GITHUB_TOKEN environment variable is required for GitHub Pages deployment. Create a token at https://github.com/settings/tokens with 'repo' scope." };
        }

        var branch = NormalizeBranchName(context.Branch);
        if (!TryValidateBranchName(branch, out var branchError))
        {
            return new DeployResult { Success = false, Error = branchError! };
        }

        var message = string.IsNullOrWhiteSpace(context.Message) ? "bukit deploy" : context.Message;

        string? cname = null;
        if (!TryNormalizeCname(context.Cname, out cname, out var cnameError))
        {
            return new DeployResult { Success = false, Error = cnameError! };
        }

        var gitCommandTimeout = ResolveGitCommandTimeout();
        var repoInfo = await GetRepoInfoAsync(gitPath, gitCommandTimeout, ct);
        if (repoInfo is null)
        {
            return new DeployResult { Success = false, Error = "Unable to determine GitHub repository. Ensure you are in a git repository with a remote 'origin' pointing to GitHub." };
        }

        var isProjectPages = !repoInfo.RepoName.EndsWith(".github.io", StringComparison.OrdinalIgnoreCase);
        var deployedUrl = isProjectPages
            ? $"https://{repoInfo.Owner}.github.io/{repoInfo.RepoName}"
            : $"https://{repoInfo.Owner}.github.io";

        if (!string.IsNullOrWhiteSpace(cname))
        {
            deployedUrl = $"https://{cname}";
        }

        logger.Info($"Deploying to GitHub Pages: {deployedUrl}");
        logger.Info($"Target branch: {branch}");

        var tempDir = Path.Combine(Path.GetTempPath(), $"bukit-deploy-{Guid.NewGuid():N}");
        string? askpassScript = null;

        try
        {
            Directory.CreateDirectory(tempDir);

            askpassScript = CreateAskpassScript(tempDir, token);
            var remoteUrl = $"https://github.com/{repoInfo.Owner}/{repoInfo.RepoName}.git";
            var branchExists = await RemoteBranchExistsAsync(gitPath, token, askpassScript, remoteUrl, branch, gitCommandTimeout, ct);

            if (branchExists)
            {
                logger.Info($"Cloning existing {branch} branch...");
                if (context.KeepHistory)
                {
                    await RunGitAuthAsync(gitPath, token, askpassScript, tempDir, gitCommandTimeout, ct, "clone", "--single-branch", "--branch", branch, remoteUrl, ".");
                }
                else
                {
                    await RunGitAuthAsync(gitPath, token, askpassScript, tempDir, gitCommandTimeout, ct, "clone", "--single-branch", "--branch", branch, "--depth", "1", remoteUrl, ".");
                }
            }
            else
            {
                logger.Info($"Creating new {branch} branch...");
                await RunGitAsync(gitPath, tempDir, gitCommandTimeout, ct, "init");
                await RunGitAsync(gitPath, tempDir, gitCommandTimeout, ct, "checkout", "-b", branch);
                await RunGitAuthAsync(gitPath, token, askpassScript, tempDir, gitCommandTimeout, ct, "remote", "add", "origin", remoteUrl);
            }

            foreach (var entry in Directory.GetFileSystemEntries(tempDir))
            {
                var name = Path.GetFileName(entry);
                if (name is ".git" or ".nojekyll" or "CNAME")
                {
                    continue;
                }

                if (Directory.Exists(entry))
                {
                    Directory.Delete(entry, recursive: true);
                }
                else
                {
                    File.Delete(entry);
                }
            }

            logger.Info("Copying build output...");
            CopyDirectory(context.OutputDir, tempDir);

            var nojekyllPath = Path.Combine(tempDir, ".nojekyll");
            if (!File.Exists(nojekyllPath))
            {
                await File.WriteAllTextAsync(nojekyllPath, string.Empty, ct);
            }

            if (!string.IsNullOrWhiteSpace(cname))
            {
                var cnamePath = Path.Combine(tempDir, "CNAME");
                await File.WriteAllTextAsync(cnamePath, cname, ct);
            }

            await EnsureGitIdentityAsync(gitPath, tempDir, gitCommandTimeout, ct);

            await RunGitAsync(gitPath, tempDir, gitCommandTimeout, ct, "add", "-A");
            await RunGitAsync(gitPath, tempDir, gitCommandTimeout, ct, "commit", "-m", message, "--allow-empty");

            try
            {
                if (context.Force)
                {
                    await RunGitAuthAsync(gitPath, token, askpassScript, tempDir, gitCommandTimeout, ct, "push", "--force", "origin", branch);
                }
                else
                {
                    await RunGitAuthAsync(gitPath, token, askpassScript, tempDir, gitCommandTimeout, ct, "push", "origin", branch);
                }
            }
            catch (Exception pushEx)
            {
                if (!context.Force && IsNonFastForwardPush(pushEx.Message))
                {
                    return new DeployResult
                    {
                        Success = false,
                        Error = "Non-fast-forward push rejected. The remote branch has diverged; rerun with --force to overwrite it."
                    };
                }

                throw;
            }

            logger.Info("Deployment successful.");
            return new DeployResult { Success = true, DeployedUrl = deployedUrl };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var sanitized = SanitizeError(ex.Message, token, askpassScript, tempDir);
            var friendly = AugmentErrorHint(sanitized);
            logger.Error($"Deployment failed: {friendly}");
            return new DeployResult { Success = false, Error = friendly };
        }
        finally
        {
            CleanupAskpassScript(askpassScript);
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
            catch (Exception cleanupEx)
            {
                Console.Error.WriteLine($"Deploy: failed to clean up temp dir: {cleanupEx.GetType().Name}");
            }
        }
    }

    private static string CreateAskpassScript(string tempDir, string token)
    {
        if (OperatingSystem.IsWindows())
        {
            var scriptPath = Path.Combine(tempDir, "git-askpass.bat");
            File.WriteAllText(scriptPath, $"@echo {token}\r\n");
            return scriptPath;
        }

        var unixPath = Path.Combine(tempDir, "git-askpass");
        File.WriteAllText(unixPath, $"#!/bin/sh\necho \"{token}\"\n");
        try
        {
            File.SetUnixFileMode(unixPath, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        }
        catch (Exception modeEx)
        {
            Console.Error.WriteLine($"Deploy: failed to set askpass file mode: {modeEx.GetType().Name}");
        }

        return unixPath;
    }

    private static void CleanupAskpassScript(string? scriptPath)
    {
        if (scriptPath is null) return;
        try { File.Delete(scriptPath); } catch (Exception delEx) { Console.Error.WriteLine($"Deploy: failed to clean up askpass script: {delEx.GetType().Name}"); }
    }

    private static string SanitizeError(string message, string token, params string?[] sensitivePaths)
    {
        var sanitized = string.IsNullOrWhiteSpace(token)
            ? message
            : message.Replace(token, "***", StringComparison.Ordinal);

        foreach (var path in sensitivePaths)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                sanitized = sanitized.Replace(path, "[redacted-path]", StringComparison.Ordinal);
            }
        }

        return sanitized;
    }

    private static string AugmentErrorHint(string message)
    {
        if (message.Contains("Git command timed out during GitHub Pages deployment", StringComparison.Ordinal) ||
            message.Contains("timed out during GitHub Pages deployment", StringComparison.Ordinal))
        {
            return message;
        }

        if (message.Contains("403", StringComparison.Ordinal) || message.Contains("Forbidden", StringComparison.OrdinalIgnoreCase))
        {
            return message + " Ensure your GITHUB_TOKEN has 'repo' scope: https://github.com/settings/tokens";
        }

        if (message.Contains("Could not resolve host", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("unable to access", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Could not connect", StringComparison.OrdinalIgnoreCase))
        {
            return message + " Check your network connection and ensure GitHub is reachable.";
        }

        if (message.Contains("Permission denied", StringComparison.Ordinal) ||
            message.Contains("not authorized", StringComparison.OrdinalIgnoreCase))
        {
            return message + " Verify your GITHUB_TOKEN is valid and has 'repo' scope.";
        }

        return message;
    }

    internal static bool IsNonFastForwardPush(string message)
        => message.Contains("non-fast-forward", StringComparison.OrdinalIgnoreCase) ||
           message.Contains("fetch first", StringComparison.OrdinalIgnoreCase) ||
           message.Contains("updates were rejected", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeBranchName(string? branch)
        => string.IsNullOrWhiteSpace(branch) ? "gh-pages" : branch.Trim();

    private static bool TryValidateBranchName(string branch, out string? error)
    {
        const string errorMessage = "deploy.branch is not a valid Git branch name for GitHub Pages deployment. Use a simple branch name such as 'gh-pages', 'pages', or 'docs/site'.";

        if (string.IsNullOrWhiteSpace(branch))
        {
            error = errorMessage;
            return false;
        }

        if (string.Equals(branch, "HEAD", StringComparison.Ordinal))
        {
            error = errorMessage;
            return false;
        }

        if (branch.StartsWith('-', StringComparison.Ordinal) ||
            branch.StartsWith("refs/", StringComparison.Ordinal) ||
            branch.EndsWith('/') ||
            branch.EndsWith('.') ||
            branch.EndsWith(".lock", StringComparison.Ordinal) ||
            branch.StartsWith('/', StringComparison.Ordinal) ||
            branch.Contains("..", StringComparison.Ordinal) ||
            branch.Contains("@{", StringComparison.Ordinal) ||
            branch.Contains('\\') ||
            branch.Contains(':', StringComparison.Ordinal) ||
            branch.Contains('?', StringComparison.Ordinal) ||
            branch.Contains('*', StringComparison.Ordinal) ||
            branch.Contains('[', StringComparison.Ordinal) ||
            branch.Any(char.IsWhiteSpace) ||
            branch.IndexOfAny(new[] { '\0', '\r', '\n', '\t' }) >= 0)
        {
            error = errorMessage;
            return false;
        }

        error = null;
        return true;
    }

    internal static bool TryNormalizeCname(string? value, out string? normalized, out string? error)
    {
        const string errorMessage = "deploy.cname must be a single domain name, for example 'www.example.com'. Do not include protocol, path, port, or whitespace.";

        normalized = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            error = null;
            return true;
        }

        var cname = value.Trim().ToLowerInvariant();

        if (cname.Length > 253 || cname.EndsWith('.', StringComparison.Ordinal) || cname.Length == 0)
        {
            error = errorMessage;
            return false;
        }

        if (cname.Contains(' ') || cname.Contains('\0') || cname.Contains('\r') || cname.Contains('\n') || cname.Contains('\t'))
        {
            error = errorMessage;
            return false;
        }

        if (cname.Contains('/') || cname.Contains(':') || cname.Contains('?') || cname.Contains('#') || cname.Contains("..", StringComparison.Ordinal))
        {
            error = errorMessage;
            return false;
        }

        var labels = cname.Split('.');
        foreach (var label in labels)
        {
            if (string.IsNullOrEmpty(label) || label.Length > 63)
            {
                error = errorMessage;
                return false;
            }
        }

        if (!IsValidDomain(cname))
        {
            error = errorMessage;
            return false;
        }

        normalized = cname;
        error = null;
        return true;
    }

    private static bool IsValidDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return false;
        }

        if (domain.Length > 253)
        {
            return false;
        }

        return Regex.IsMatch(domain, @"^[A-Za-z0-9]([A-Za-z0-9\-]{0,61}[A-Za-z0-9])?(\.[A-Za-z0-9]([A-Za-z0-9\-]{0,61}[A-Za-z0-9])?)*$", RegexOptions.CultureInvariant);
    }

    private static TimeSpan ResolveGitCommandTimeout()
    {
        var timeout = Environment.GetEnvironmentVariable("BUKIT_DEPLOY_GIT_TIMEOUT_SECONDS");
        if (!int.TryParse(timeout, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
        {
            return TimeSpan.FromSeconds(300);
        }

        if (seconds < 0)
        {
            return TimeSpan.FromSeconds(300);
        }

        return seconds == 0 ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(seconds);
    }

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

                throw;
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

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var dest = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, dest, overwrite: true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(dir);
            if (dirName is ".git")
            {
                continue;
            }

            CopyDirectory(dir, Path.Combine(destDir, dirName));
        }
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
