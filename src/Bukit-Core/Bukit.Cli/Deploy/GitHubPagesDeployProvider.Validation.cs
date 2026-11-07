using System.Globalization;
using System.Text.RegularExpressions;
using Bukit.Config;
using Bukit.Shared;

namespace Bukit.Cli.Deploy;

public sealed partial class GitHubPagesDeployProvider
{
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

    private static string AugmentErrorHint(string message, string tokenEnv)
    {
        if (message.Contains("Git command timed out during GitHub Pages deployment", StringComparison.Ordinal) ||
            message.Contains("timed out during GitHub Pages deployment", StringComparison.Ordinal))
        {
            return message;
        }

        if (message.Contains("403", StringComparison.Ordinal) || message.Contains("Forbidden", StringComparison.OrdinalIgnoreCase))
        {
            return message + $" Ensure your {tokenEnv} can write repository contents (fine-grained: 'Contents: Read and write'): https://github.com/settings/tokens";
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
            return message + $" Verify your {tokenEnv} is valid and can write repository contents (fine-grained: 'Contents: Read and write').";
        }

        return message;
    }

    internal static bool IsNonFastForwardPush(string message)
        => message.Contains("non-fast-forward", StringComparison.OrdinalIgnoreCase) ||
           message.Contains("fetch first", StringComparison.OrdinalIgnoreCase) ||
           message.Contains("updates were rejected", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeBranchName(string? branch)
        => string.IsNullOrWhiteSpace(branch) ? "gh-pages" : branch.Trim();

    internal const string DefaultTokenEnvName = "GITHUB_TOKEN";

    // Upper-case only on purpose: GitHub token prefixes (ghp_, github_pat_) are lower-case, so a
    // token pasted into --token-env by mistake is rejected without its value being echoed.
    private static bool IsValidTokenEnvName(string value)
    {
        if (value.Length is < 1 or > 128 || !(value[0] is (>= 'A' and <= 'Z') or '_'))
        {
            return false;
        }

        foreach (var c in value)
        {
            if (!(c is (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_'))
            {
                return false;
            }
        }

        return true;
    }

    internal static bool TryNormalizeTokenEnvName(string? value, out string? normalized, out string? error)
    {
        normalized = null;
        error = null;
        if (value is null)
        {
            normalized = DefaultTokenEnvName;
            return true;
        }

        // An explicit but empty value (for example an unset shell variable passed as "$DOCS_TOKEN")
        // must fail instead of silently falling back to GITHUB_TOKEN, which may belong to another account.
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            error = "deploy --token-env must not be empty. Pass the name of an environment variable such as 'DOCS_DEPLOY_TOKEN', or omit the option to use GITHUB_TOKEN.";
            return false;
        }

        if (!IsValidTokenEnvName(trimmed))
        {
            error = "deploy --token-env must be an environment variable name made of upper-case letters, digits and underscores (for example 'DOCS_DEPLOY_TOKEN'). The value is not echoed.";
            return false;
        }

        normalized = trimmed;
        return true;
    }

    // Shares GitHubRepoSlug with deploy.repo in Bukit.Config. Unset (null) is valid and means "use origin".
    internal static bool TryNormalizeRepo(string? value, out string? normalized, out string? error)
    {
        normalized = null;
        error = null;
        if (value is null)
        {
            return true;
        }

        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            error = "deploy --repo must not be empty. Pass 'owner/name', for example 'ALi365-SDN-BHD/site', or omit the option to use origin.";
            return false;
        }

        if (!GitHubRepoSlug.IsValid(trimmed))
        {
            error = "deploy --repo must be 'owner/name' (for example 'ALi365-SDN-BHD/site'), without a URL, '.git' suffix, or whitespace. The value is not echoed.";
            return false;
        }

        normalized = trimmed;
        return true;
    }

    // Returns the target repository (explicit --repo / deploy.repo) or the project origin.
    // An explicit target never reads origin for identity, so it works without an origin remote.
    private static async Task<(RepoInfo? Info, string? Error)> ResolveRepoInfoAsync(
        DeployContext context,
        string gitPath,
        string projectRoot,
        TimeSpan gitCommandTimeout,
        CancellationToken ct)
    {
        if (context.Repo is null)
        {
            return (await GetRepoInfoAsync(gitPath, projectRoot, gitCommandTimeout, ct), null);
        }

        if (!TryNormalizeRepo(context.Repo, out var targetRepo, out var targetRepoError))
        {
            return (null, targetRepoError);
        }

        context.Logger.Info($"Deploy target repository: {targetRepo} (from {context.RepoSource})");
        await WarnIfTargetDiffersFromOriginAsync(gitPath, projectRoot, gitCommandTimeout, targetRepo!, context.Logger, ct);
        return (ParseRepoInfo(targetRepo!), null);
    }

    private static RepoInfo ParseRepoInfo(string ownerSlashName)
    {
        var parts = ownerSlashName.Split('/');
        return new RepoInfo(parts[0], parts[1]);
    }

    private static async Task WarnIfTargetDiffersFromOriginAsync(
        string gitPath,
        string projectRoot,
        TimeSpan gitCommandTimeout,
        string targetRepo,
        ILogger logger,
        CancellationToken ct)
    {
        RepoInfo? origin;
        try
        {
            origin = await GetRepoInfoAsync(gitPath, projectRoot, gitCommandTimeout, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // No usable origin is expected when the target is given explicitly; the target still applies.
            logger.Debug($"origin could not be read for comparison: {ex.GetType().Name}");
            return;
        }

        if (origin is null)
        {
            return;
        }

        var originSlug = $"{origin.Owner}/{origin.RepoName}";
        if (!string.Equals(originSlug, targetRepo, StringComparison.OrdinalIgnoreCase))
        {
            logger.Warn($"Deploy target {targetRepo} differs from the project origin {originSlug}; deploying to {targetRepo}.");
        }
    }

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

        if (branch.StartsWith("-", StringComparison.Ordinal) ||
            branch.StartsWith("refs/", StringComparison.Ordinal) ||
            branch.EndsWith('/') ||
            branch.EndsWith('.') ||
            branch.EndsWith(".lock", StringComparison.Ordinal) ||
            branch.StartsWith("/", StringComparison.Ordinal) ||
            branch.Contains("..", StringComparison.Ordinal) ||
            branch.Contains("@{", StringComparison.Ordinal) ||
            branch.Contains('\\') ||
            branch.Contains(":", StringComparison.Ordinal) ||
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

        var trimmed = value.Trim();
        if (!string.Equals(trimmed, value, StringComparison.Ordinal))
        {
            error = errorMessage;
            return false;
        }

        var cname = trimmed.ToLowerInvariant();

        if (cname.Length > 253 || cname.EndsWith(".", StringComparison.Ordinal) || cname.Length == 0)
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

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            if (File.GetAttributes(file).HasFlag(FileAttributes.ReparsePoint) ||
                fileName.Equals(".bukit-build-state.json", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals(".bukit-output-marker", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var dest = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, dest, overwrite: true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(dir);
            if (File.GetAttributes(dir).HasFlag(FileAttributes.ReparsePoint) ||
                dirName.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
                dirName.Equals(".bukit", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            CopyDirectory(dir, Path.Combine(destDir, dirName));
        }
    }

    private static bool HasDeployableOutputFiles(string outputDir)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        foreach (var path in Directory.EnumerateFiles(outputDir, "*", options))
        {
            var relativePath = Path.GetRelativePath(outputDir, path)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');
            var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var fileName = segments.LastOrDefault() ?? string.Empty;
            if (segments.Any(segment =>
                    segment.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
                    segment.Equals(".bukit", StringComparison.OrdinalIgnoreCase)) ||
                fileName.Equals(".bukit-build-state.json", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals(".bukit-output-marker", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return true;
        }

        return false;
    }
}
