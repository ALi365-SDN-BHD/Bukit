using System.Diagnostics;
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

        var repoInfo = await GetRepoInfoAsync(gitPath, ct);
        if (repoInfo is null)
        {
            return new DeployResult { Success = false, Error = "Unable to determine GitHub repository. Ensure you are in a git repository with a remote 'origin' pointing to GitHub." };
        }

        var branch = string.IsNullOrWhiteSpace(context.Branch) ? "gh-pages" : context.Branch;
        var message = string.IsNullOrWhiteSpace(context.Message) ? "bukit deploy" : context.Message;

        var isProjectPages = !repoInfo.RepoName.EndsWith(".github.io", StringComparison.OrdinalIgnoreCase);
        var deployedUrl = isProjectPages
            ? $"https://{repoInfo.Owner}.github.io/{repoInfo.RepoName}"
            : $"https://{repoInfo.Owner}.github.io";

        if (!string.IsNullOrWhiteSpace(context.Cname))
        {
            deployedUrl = $"https://{context.Cname}";
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
            var branchExists = await RemoteBranchExistsAsync(gitPath, token, askpassScript, remoteUrl, branch, ct);

            if (branchExists)
            {
                logger.Info($"Cloning existing {branch} branch...");
                if (context.KeepHistory)
                {
                    await RunGitAuthAsync(gitPath, token, askpassScript, tempDir, ct, "clone", "--single-branch", "--branch", branch, remoteUrl, ".");
                }
                else
                {
                    await RunGitAuthAsync(gitPath, token, askpassScript, tempDir, ct, "clone", "--single-branch", "--branch", branch, "--depth", "1", remoteUrl, ".");
                }
            }
            else
            {
                logger.Info($"Creating new {branch} branch...");
                await RunGitAsync(gitPath, tempDir, ct, "init");
                await RunGitAsync(gitPath, tempDir, ct, "checkout", "-b", branch);
                await RunGitAuthAsync(gitPath, token, askpassScript, tempDir, ct, "remote", "add", "origin", remoteUrl);
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

            if (!string.IsNullOrWhiteSpace(context.Cname))
            {
                var cnamePath = Path.Combine(tempDir, "CNAME");
                await File.WriteAllTextAsync(cnamePath, context.Cname, ct);
            }

            await EnsureGitIdentityAsync(gitPath, tempDir, ct);

            await RunGitAsync(gitPath, tempDir, ct, "add", "-A");
            await RunGitAsync(gitPath, tempDir, ct, "commit", "-m", message, "--allow-empty");

            try
            {
                await RunGitAuthAsync(gitPath, token, askpassScript, tempDir, ct, "push", "origin", branch);
            }
            catch (Exception pushEx)
            {
                var pushMsg = pushEx.Message;
                if (pushMsg.Contains("non-fast-forward") || pushMsg.Contains("fetch first") || pushMsg.Contains("updates were rejected"))
                {
                    if (!context.Force)
                    {
                        return new DeployResult
                        {
                            Success = false,
                            Error = "Non-fast-forward push detected. The remote branch has diverged. Re-run with --force to overwrite it."
                        };
                    }

                    logger.Warn("Non-fast-forward push detected. The remote branch has diverged. Force-pushing because --force was specified...");
                    await RunGitAuthAsync(gitPath, token, askpassScript, tempDir, ct, "push", "--force", "origin", branch);
                }
                else
                {
                    throw;
                }
            }

            logger.Info("Deployment successful.");
            return new DeployResult { Success = true, DeployedUrl = deployedUrl };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var sanitized = SanitizeError(ex.Message, token);
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

    private static string SanitizeError(string message, string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return message;
        return message.Replace(token, "***");
    }

    private static string AugmentErrorHint(string message)
    {
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

    private static async Task EnsureGitIdentityAsync(string gitPath, string tempDir, CancellationToken ct)
    {
        var hasName = false;
        var hasEmail = false;

        try
        {
            var name = await RunGitAndCaptureAsync(gitPath, tempDir, ct, "config", "--local", "user.name");
            hasName = !string.IsNullOrWhiteSpace(name);
        }
        catch
        {
        }

        try
        {
            if (!hasName)
            {
                var globalName = await RunGitAndCaptureAsync(gitPath, tempDir, ct, "config", "--global", "user.name");
                hasName = !string.IsNullOrWhiteSpace(globalName);
            }
        }
        catch
        {
        }

        try
        {
            var email = await RunGitAndCaptureAsync(gitPath, tempDir, ct, "config", "--local", "user.email");
            hasEmail = !string.IsNullOrWhiteSpace(email);
        }
        catch
        {
        }

        try
        {
            if (!hasEmail)
            {
                var globalEmail = await RunGitAndCaptureAsync(gitPath, tempDir, ct, "config", "--global", "user.email");
                hasEmail = !string.IsNullOrWhiteSpace(globalEmail);
            }
        }
        catch
        {
        }

        if (!hasName)
        {
            await RunGitAsync(gitPath, tempDir, ct, "config", "--local", "user.name", "bukit");
        }

        if (!hasEmail)
        {
            await RunGitAsync(gitPath, tempDir, ct, "config", "--local", "user.email", "bukit@deploy.local");
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

    private static async Task<RepoInfo?> GetRepoInfoAsync(string gitPath, CancellationToken ct)
    {
        try
        {
            var url = await RunGitAndCaptureAsync(gitPath, null, ct, "remote", "get-url", "origin");
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            url = url.Trim();
            var match = Regex.Match(url, @"github\.com[:/]([^/]+)/([^/\s.]+?)(\.git)?$", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return null;
            }

            return new RepoInfo(match.Groups[1].Value, match.Groups[2].Value);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<bool> RemoteBranchExistsAsync(string gitPath, string token, string askpassScript, string remoteUrl, string branch, CancellationToken ct)
    {
        try
        {
            var output = await RunGitAuthAndCaptureAsync(gitPath, token, askpassScript, null, ct, "ls-remote", "--heads", remoteUrl, $"refs/heads/{branch}");
            return !string.IsNullOrWhiteSpace(output);
        }
        catch
        {
            return false;
        }
    }

    private static async Task RunGitAuthAsync(string gitPath, string token, string askpassScript, string? workingDir, CancellationToken ct, params string[] args)
    {
        var psi = CreateGitProcess(gitPath, workingDir, args);
        psi.Environment["GIT_ASKPASS"] = askpassScript;
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start git process.");
        await proc.WaitForExitAsync(ct);

        if (proc.ExitCode != 0)
        {
            var error = await proc.StandardError.ReadToEndAsync();
            throw new GitException($"git {string.Join(' ', args)} failed (exit {proc.ExitCode}): {error.Trim()}");
        }
    }

    private static async Task<string> RunGitAuthAndCaptureAsync(string gitPath, string token, string askpassScript, string? workingDir, CancellationToken ct, params string[] args)
    {
        var psi = CreateGitProcess(gitPath, workingDir, args);
        psi.Environment["GIT_ASKPASS"] = askpassScript;
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";

        using var proc = Process.Start(psi);
        if (proc is null)
        {
            return string.Empty;
        }

        await proc.WaitForExitAsync(ct);
        var output = await proc.StandardOutput.ReadToEndAsync();
        return output.Trim();
    }

    private static async Task RunGitAsync(string gitPath, string? workingDir, CancellationToken ct, params string[] args)
    {
        var psi = CreateGitProcess(gitPath, workingDir, args);

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start git process.");
        await proc.WaitForExitAsync(ct);

        if (proc.ExitCode != 0)
        {
            var error = await proc.StandardError.ReadToEndAsync();
            throw new GitException($"git {string.Join(' ', args)} failed (exit {proc.ExitCode}): {error.Trim()}");
        }
    }

    private static async Task<string> RunGitAndCaptureAsync(string gitPath, string? workingDir, CancellationToken ct, params string[] args)
    {
        var psi = CreateGitProcess(gitPath, workingDir, args);

        using var proc = Process.Start(psi);
        if (proc is null)
        {
            return string.Empty;
        }

        await proc.WaitForExitAsync(ct);
        var output = await proc.StandardOutput.ReadToEndAsync();
        return output.Trim();
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

    private sealed class GitException : Exception
    {
        public GitException(string message) : base(message) { }
    }

    private sealed record RepoInfo(string Owner, string RepoName);
}
