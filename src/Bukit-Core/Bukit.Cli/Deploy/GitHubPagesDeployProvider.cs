using Bukit.Shared;

namespace Bukit.Cli.Deploy;

public sealed partial class GitHubPagesDeployProvider : IDeployProvider
{
    public string Name => "github-pages";

    public async Task<DeployResult> DeployAsync(DeployContext context, CancellationToken ct)
        => await DeployAsync(context, Directory.GetCurrentDirectory(), ct);

    /// <summary>
    /// Deploys using <paramref name="projectRoot"/> for repository identity and transport
    /// context instead of the process working directory.
    /// </summary>
    internal static async Task<DeployResult> DeployAsync(DeployContext context, string projectRoot, CancellationToken ct)
    {
        var logger = context.Logger;
        var gitPath = ResolveGit(logger);
        if (gitPath is null)
        {
            return new DeployResult { Success = false, Error = "git command not found. Please install git and ensure it is in PATH." };
        }

        if (!Directory.Exists(context.OutputDir))
        {
            return new DeployResult { Success = false, Error = $"Output directory not found: {context.OutputDir}" };
        }

        if (!HasDeployableOutputFiles(context.OutputDir))
        {
            return new DeployResult { Success = false, Error = $"Output directory is empty: {context.OutputDir}" };
        }

        if (DeploymentReadinessValidator.Validate(context.OutputDir) is { } readinessError)
        {
            return new DeployResult { Success = false, Error = readinessError };
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
        RepoInfo? repoInfo;
        try
        {
            repoInfo = await GetRepoInfoAsync(gitPath, projectRoot, gitCommandTimeout, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var sanitized = SanitizeError(ex.Message, token);
            logger.Error($"Deployment failed: {sanitized}");
            return new DeployResult { Success = false, Error = sanitized };
        }

        if (repoInfo is null)
        {
            return new DeployResult { Success = false, Error = "Unable to determine GitHub repository. Ensure the configured project is a git repository with a local or worktree 'origin' pointing directly to GitHub." };
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
        var checkoutDir = Path.Combine(tempDir, "checkout");
        var remoteName = $"bukit-deploy-{Guid.NewGuid():N}";
        string? askpassScript = null;

        try
        {
            Directory.CreateDirectory(checkoutDir);

            askpassScript = CreateAskpassScript(tempDir, token);
            var remoteUrl = $"https://github.com/{repoInfo.Owner}/{repoInfo.RepoName}.git";
            // Establish the final transport config context before any preflight or transport:
            // the private remote and its URL live in this temporary repository's local config.
            await RunGitAsync(gitPath, checkoutDir, gitCommandTimeout, ct, "init");
            await RunGitAsync(gitPath, checkoutDir, gitCommandTimeout, ct, "remote", "add", remoteName, remoteUrl);

            await EnsureTransportTargetAsync(gitPath, checkoutDir, remoteName, remoteUrl, gitCommandTimeout, ct);
            var branchExists = await RemoteBranchExistsAsync(gitPath, token, askpassScript, checkoutDir, remoteName, branch, gitCommandTimeout, ct);

            if (branchExists)
            {
                logger.Info($"Fetching existing {branch} branch...");
                await EnsureTransportTargetAsync(gitPath, checkoutDir, remoteName, remoteUrl, gitCommandTimeout, ct);
                string[] fetchArguments = context.KeepHistory
                    ? ["fetch", remoteName, $"refs/heads/{branch}"]
                    : ["fetch", "--depth", "1", remoteName, $"refs/heads/{branch}"];
                await RunGitAuthAsync(gitPath, token, askpassScript, checkoutDir, gitCommandTimeout, ct, fetchArguments);
                // FETCH_HEAD avoids writing into a checked-out unborn branch (the repository
                // default branch), which Git refuses to fetch into.
                await RunGitAsync(gitPath, checkoutDir, gitCommandTimeout, ct, "checkout", "-B", branch, "FETCH_HEAD");
            }
            else
            {
                logger.Info($"Creating new {branch} branch...");
                await RunGitAsync(gitPath, checkoutDir, gitCommandTimeout, ct, "checkout", "-B", branch);
            }

            foreach (var entry in Directory.GetFileSystemEntries(checkoutDir))
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
            CopyDirectory(context.OutputDir, checkoutDir);

            var nojekyllPath = Path.Combine(checkoutDir, ".nojekyll");
            if (!File.Exists(nojekyllPath))
            {
                await File.WriteAllTextAsync(nojekyllPath, string.Empty, ct);
            }

            if (!string.IsNullOrWhiteSpace(cname))
            {
                var cnamePath = Path.Combine(checkoutDir, "CNAME");
                await File.WriteAllTextAsync(cnamePath, cname, ct);
            }

            var privacyErrors = DeploymentPrivacyValidator.Validate(context.OutputDir, checkoutDir);
            if (privacyErrors.Count > 0)
            {
                return new DeployResult
                {
                    Success = false,
                    Error = string.Join(" ", privacyErrors)
                };
            }

            await EnsureGitIdentityAsync(gitPath, checkoutDir, gitCommandTimeout, ct, logger);

            await RunGitAsync(gitPath, checkoutDir, gitCommandTimeout, ct, "add", "-A");
            await RunGitAsync(gitPath, checkoutDir, gitCommandTimeout, ct, "commit", "-m", message, "--allow-empty");

            try
            {
                await EnsureTransportTargetAsync(gitPath, checkoutDir, remoteName, remoteUrl, gitCommandTimeout, ct);
                if (context.Force)
                {
                    await RunGitAuthAsync(gitPath, token, askpassScript, checkoutDir, gitCommandTimeout, ct, "push", "--force", remoteName, branch);
                }
                else
                {
                    await RunGitAuthAsync(gitPath, token, askpassScript, checkoutDir, gitCommandTimeout, ct, "push", remoteName, branch);
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

            logger.Info("Git push completed. GitHub Pages publication has not been verified.");
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

}
