using System.Globalization;
using System.Text.RegularExpressions;

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
}
