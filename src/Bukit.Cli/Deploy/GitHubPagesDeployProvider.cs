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
            return new DeployResult { Success = false, Error = "GITHUB_TOKEN environment variable is required for GitHub Pages deployment." };
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
        try
        {
            Directory.CreateDirectory(tempDir);

            var remoteUrl = $"https://x-access-token:{token}@github.com/{repoInfo.Owner}/{repoInfo.RepoName}.git";
            var branchExists = await RemoteBranchExistsAsync(gitPath, remoteUrl, branch, ct);

            if (branchExists)
            {
                logger.Info($"Cloning existing {branch} branch...");
                await RunGitAsync(gitPath, tempDir, ct, "clone", "--single-branch", "--branch", branch, "--depth", "1", remoteUrl, ".");
            }
            else
            {
                logger.Info($"Creating new {branch} branch...");
                await RunGitAsync(gitPath, tempDir, ct, "init");
                await RunGitAsync(gitPath, tempDir, ct, "checkout", "-b", branch);
                await RunGitAsync(gitPath, tempDir, ct, "remote", "add", "origin", remoteUrl);
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

            await RunGitAsync(gitPath, tempDir, ct, "add", "-A");
            await RunGitAsync(gitPath, tempDir, ct, "commit", "-m", message, "--allow-empty");
            await RunGitAsync(gitPath, tempDir, ct, "push", "origin", branch);

            logger.Info("Deployment successful.");
            return new DeployResult { Success = true, DeployedUrl = deployedUrl };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error($"Deployment failed: {ex.Message}");
            return new DeployResult { Success = false, Error = ex.Message };
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
            catch
            {
            }
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

    private static async Task<bool> RemoteBranchExistsAsync(string gitPath, string remoteUrl, string branch, CancellationToken ct)
    {
        try
        {
            var output = await RunGitAndCaptureAsync(gitPath, null, ct, "ls-remote", "--heads", remoteUrl, $"refs/heads/{branch}");
            return !string.IsNullOrWhiteSpace(output);
        }
        catch
        {
            return false;
        }
    }

    private static async Task RunGitAsync(string gitPath, string? workingDir, CancellationToken ct, params string[] args)
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

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start git process.");
        await proc.WaitForExitAsync(ct);

        if (proc.ExitCode != 0)
        {
            var error = await proc.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"git {string.Join(' ', args)} failed (exit {proc.ExitCode}): {error.Trim()}");
        }
    }

    private static async Task<string> RunGitAndCaptureAsync(string gitPath, string? workingDir, CancellationToken ct, params string[] args)
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

        using var proc = Process.Start(psi);
        if (proc is null)
        {
            return string.Empty;
        }

        await proc.WaitForExitAsync(ct);
        var output = await proc.StandardOutput.ReadToEndAsync();
        return output.Trim();
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

    private sealed record RepoInfo(string Owner, string RepoName);
}
