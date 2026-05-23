using System.Diagnostics;
using Bukit.Shared;

namespace Bukit.Engine;

public static class ThemeSourceManager
{
    public sealed record ResolvedTheme(string ThemeRoot, string? Version);

    public static ResolvedTheme? Resolve(string source, string cacheDir, Action<string>? log = null)
    {
        source = source.Trim();
        if (string.IsNullOrWhiteSpace(source)) return null;

        var versionTag = (string?)null;
        var url = source;

        var atIndex = source.LastIndexOf('@');
        if (atIndex > 0)
        {
            var after = source[(atIndex + 1)..];
            if (!after.Contains('/'))
            {
                url = source[..atIndex];
                versionTag = after;
            }
        }

        var name = SafeName(url);
        var repoPath = Path.Combine(cacheDir, name);

        if (!Directory.Exists(repoPath))
        {
            log?.Invoke($"Cloning theme: {url}");
            var cloneResult = RunGit("clone", $"\"{url}\" \"{repoPath}\"", cacheDir);
            if (!cloneResult.Success)
            {
                log?.Invoke($"Git clone failed: {cloneResult.Error}");
                return null;
            }
        }

        if (versionTag is not null)
        {
            log?.Invoke($"Checking out version: {versionTag}");
            var checkoutResult = RunGit("checkout", versionTag, repoPath);
            if (checkoutResult.Success) return new ResolvedTheme(repoPath, versionTag);

            log?.Invoke($"Fetching tags and retrying: {versionTag}");
            RunGit("fetch", "--tags", repoPath);
            var retryResult = RunGit("checkout", versionTag, repoPath);
            if (!retryResult.Success)
            {
                log?.Invoke($"Version checkout failed: {retryResult.Error}");
            }
        }
        else
        {
            RunGit("pull", "", repoPath);
        }

        return new ResolvedTheme(repoPath, versionTag);
    }

    private static (bool Success, string Error) RunGit(string command, string args, string workDir)
    {
        try
        {
            var psi = new ProcessStartInfo("git", $"{command} {args}".Trim())
            {
                WorkingDirectory = workDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc is null) return (false, "Failed to start git process");

            proc.WaitForExit(TimeSpan.FromSeconds(120));
            if (proc.ExitCode == 0) return (true, "");

            var err = proc.StandardError.ReadToEnd();
            return (false, err.Trim());
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static string SafeName(string url)
    {
        var name = url
            .Replace("https://", "")
            .Replace("http://", "")
            .Replace("git@", "")
            .Replace(".git", "")
            .Replace(':', '/')
            .Replace("//", "/")
            .TrimEnd('/');

        var invalid = Path.GetInvalidFileNameChars();
        foreach (var c in invalid) name = name.Replace(c, '_');

        return name;
    }
}
