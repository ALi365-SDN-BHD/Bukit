using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bukit.Shared;

namespace Bukit.Engine;

public sealed record GitResult(bool Success, string StdOut, string StdErr, int? ExitCode, bool TimedOut);

public interface IGitRunner
{
    Task<GitResult> RunAsync(string args, string workingDirectory, TimeSpan timeout, CancellationToken cancellationToken);
}

public static class ThemeSourceManager
{
    public sealed record ResolvedTheme(string ThemeRoot, string? Version);

    public static ResolvedTheme? Resolve(string source, string cacheDir, Action<string>? log = null, IGitRunner? gitRunner = null)
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
        gitRunner ??= new ProcessGitRunner();
        var timeout = TimeSpan.FromSeconds(120);

        if (!Directory.Exists(repoPath))
        {
            log?.Invoke($"Cloning theme: {url}");
            var cloneResult = gitRunner.RunAsync($"clone \"{url}\" \"{repoPath}\"", cacheDir, timeout, CancellationToken.None).GetAwaiter().GetResult();
            if (!cloneResult.Success)
            {
                var error = FormatGitError(cloneResult);
                log?.Invoke($"Git clone failed: {error}");
                throw new ConfigException($"Failed to clone theme source '{url}': {error}", DiagnosticCode.ConfigInvalidValue);
            }
        }

        if (versionTag is not null)
        {
            log?.Invoke($"Checking out version: {versionTag}");
            var checkoutResult = gitRunner.RunAsync($"checkout {versionTag}", repoPath, timeout, CancellationToken.None).GetAwaiter().GetResult();
            if (checkoutResult.Success)
            {
                WriteOrValidateLock(cacheDir, url, versionTag, repoPath, gitRunner, timeout);
                return new ResolvedTheme(repoPath, versionTag);
            }

            log?.Invoke($"Fetching tags and retrying: {versionTag}");
            gitRunner.RunAsync("fetch --tags", repoPath, timeout, CancellationToken.None).GetAwaiter().GetResult();
            var retryResult = gitRunner.RunAsync($"checkout {versionTag}", repoPath, timeout, CancellationToken.None).GetAwaiter().GetResult();
            if (!retryResult.Success)
            {
                var error = FormatGitError(retryResult);
                log?.Invoke($"Version checkout failed: {error}");
                throw new ConfigException($"Failed to checkout theme source '{url}' version '{versionTag}': {error}", DiagnosticCode.ConfigInvalidValue);
            }

            WriteOrValidateLock(cacheDir, url, versionTag, repoPath, gitRunner, timeout);
        }
        return new ResolvedTheme(repoPath, versionTag);
    }

    internal static string SafeNameForTests(string url) => SafeName(url);

    private static void WriteOrValidateLock(string cacheDir, string source, string @ref, string repoPath, IGitRunner gitRunner, TimeSpan timeout)
    {
        var commitResult = gitRunner.RunAsync("rev-parse HEAD", repoPath, timeout, CancellationToken.None).GetAwaiter().GetResult();
        if (!commitResult.Success)
        {
            throw new ConfigException($"Failed to resolve theme source '{source}' version '{@ref}' commit: {FormatGitError(commitResult)}", DiagnosticCode.ConfigInvalidValue);
        }

        var commit = commitResult.StdOut.Trim();
        if (string.IsNullOrWhiteSpace(commit))
        {
            throw new ConfigException($"Failed to resolve theme source '{source}' version '{@ref}' commit: git returned an empty commit.", DiagnosticCode.ConfigInvalidValue);
        }

        var lockPath = Path.Combine(cacheDir, "bukit-theme.lock.json");
        var themeLock = ThemeLockFile.Load(lockPath);
        var existing = themeLock.Themes.FirstOrDefault(x => string.Equals(x.Source, source, StringComparison.OrdinalIgnoreCase) && string.Equals(x.Ref, @ref, StringComparison.Ordinal));
        if (existing is not null && !string.Equals(existing.Commit, commit, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConfigException($"Theme lock mismatch for '{source}@{@ref}': locked commit {existing.Commit}, current commit {commit}.", DiagnosticCode.ConfigInvalidValue);
        }

        if (existing is null)
        {
            themeLock.Themes.Add(new ThemeLockEntry { Source = source, Ref = @ref, Commit = commit });
        }
        else
        {
            existing.Commit = commit;
        }

        themeLock.Save(lockPath);
    }

    private static string FormatGitError(GitResult result)
    {
        if (result.TimedOut)
        {
            return "git command timed out";
        }

        return string.IsNullOrWhiteSpace(result.StdErr)
            ? string.IsNullOrWhiteSpace(result.StdOut) ? $"git exited with code {result.ExitCode}" : result.StdOut.Trim()
            : result.StdErr.Trim();
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

internal sealed class ThemeLockFile
{
    public List<ThemeLockEntry> Themes { get; set; } = new();

    public static ThemeLockFile Load(string path)
    {
        if (!File.Exists(path))
        {
            return new ThemeLockFile();
        }

        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(path), ThemeLockJsonContext.Default.ThemeLockFile) ?? new ThemeLockFile();
        }
        catch (JsonException ex)
        {
            throw new ConfigException($"Failed to read theme lock file '{path}': {ex.Message}", ex, DiagnosticCode.ConfigInvalidValue);
        }
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, ThemeLockJsonContext.Default.ThemeLockFile));
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(ThemeLockFile))]
internal sealed partial class ThemeLockJsonContext : JsonSerializerContext;

internal sealed class ThemeLockEntry
{
    public string Source { get; set; } = string.Empty;
    public string Ref { get; set; } = string.Empty;
    public string Commit { get; set; } = string.Empty;
}

public sealed class ProcessGitRunner : IGitRunner
{
    public async Task<GitResult> RunAsync(string args, string workingDirectory, TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            var psi = new ProcessStartInfo("git", args.Trim())
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc is null)
            {
                return new GitResult(false, string.Empty, "Failed to start git process", null, false);
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);
            try
            {
                var stdoutTask = proc.StandardOutput.ReadToEndAsync(timeoutCts.Token);
                var stderrTask = proc.StandardError.ReadToEndAsync(timeoutCts.Token);
                await proc.WaitForExitAsync(timeoutCts.Token);
                var stdout = await stdoutTask;
                var stderr = await stderrTask;
                return new GitResult(proc.ExitCode == 0, stdout, stderr, proc.ExitCode, false);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (!proc.HasExited)
                    {
                        proc.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                }

                return new GitResult(false, string.Empty, "git command timed out", null, true);
            }
        }
        catch (Exception ex)
        {
            return new GitResult(false, string.Empty, ex.Message, null, false);
        }
    }
}
