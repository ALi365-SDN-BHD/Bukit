using System.Diagnostics;
using System.Text;
using Bukit.Cli.Shared;
using Bukit.Config;
using Bukit.Engine;
using Bukit.Shared;

namespace Bukit.Cli.Commands;

internal static class WorktreeBuildRepository
{
    private static readonly TimeSpan GitTimeout = TimeSpan.FromMinutes(2);

    internal static async Task<GitRepository> ValidateRepositoryAsync(
        AppConfig config,
        ResolvedConfigPath resolved,
        ConfigOverrides overrides,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var rootResult = await RunGitAsync(resolved.RootDir, ["rev-parse", "--show-toplevel"], logger, cancellationToken).ConfigureAwait(false);
        if (rootResult.ExitCode != 0 || string.IsNullOrWhiteSpace(rootResult.StandardOutput))
        {
            throw new WorktreeBuildException("i18n-worktree-git-required", "Multilingual build requires the site to be inside a Git repository.");
        }
        var repositoryRoot = Path.GetFullPath(rootResult.StandardOutput.Trim());
        var head = await RequireGitOutputAsync(repositoryRoot, ["rev-parse", "--verify", "HEAD"], logger, "i18n-worktree-git-required", cancellationToken).ConfigureAwait(false);
        var status = await RunGitAsync(repositoryRoot, ["status", "--porcelain=v1", "--untracked-files=all"], logger, cancellationToken).ConfigureAwait(false);
        if (status.ExitCode != 0)
        {
            throw new WorktreeBuildException("i18n-worktree-git-required", status.StandardError.Trim());
        }
        if (!string.IsNullOrEmpty(status.StandardOutput))
        {
            throw new WorktreeBuildException("i18n-worktree-dirty", "Multilingual build requires a clean Git worktree with no staged, unstaged, or untracked changes.");
        }

        var effective = ConfigApplier.Apply(config, overrides);
        IReadOnlyList<string> inputPaths;
        try
        {
            inputPaths = ResolveInputPaths(effective, resolved, repositoryRoot, logger);
        }
        catch (ConfigException ex)
        {
            throw new WorktreeBuildException("i18n-worktree-input-untracked", ex.Message, ex);
        }
        var configRelative = WorktreeBuildPaths.RelativeInside(repositoryRoot, resolved.FullConfigPath, "Configuration file");
        var trackedConfig = await RunGitAsync(repositoryRoot, ["ls-files", "--error-unmatch", "--", configRelative], logger, cancellationToken).ConfigureAwait(false);
        if (trackedConfig.ExitCode != 0)
        {
            throw new WorktreeBuildException("i18n-worktree-input-untracked", $"Required build input is not tracked by Git: {configRelative}");
        }
        if (inputPaths.Count > 0)
        {
            var ignored = await RunGitAsync(repositoryRoot, ["ls-files", "--others", "--ignored", "--exclude-standard", "-z", "--", .. inputPaths], logger, cancellationToken).ConfigureAwait(false);
            if (ignored.ExitCode != 0)
            {
                throw new WorktreeBuildException("i18n-worktree-git-required", ignored.StandardError.Trim());
            }
            var ignoredPath = ignored.StandardOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (ignoredPath is not null)
            {
                throw new WorktreeBuildException("i18n-worktree-input-untracked", $"Required build input is ignored and not tracked by Git: {ignoredPath}");
            }
        }
        return new GitRepository(repositoryRoot, head.Trim());
    }

    internal static async Task AddWorktreeAsync(
        GitRepository repository,
        string path,
        string registryPath,
        List<string> ownedWorktrees,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.AppendAllTextAsync(registryPath, path + Environment.NewLine, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        WorktreeBuildPaths.RestrictFile(registryPath);
        var result = await RunGitAsync(repository.Root, ["worktree", "add", "--detach", path, repository.Head], logger, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new WorktreeBuildException("i18n-worktree-worker-failed", $"Unable to create detached Git worktree: {result.StandardError.Trim()}");
        }
        ownedWorktrees.Add(path);
    }

    internal static async Task CleanupOwnedWorktreesAsync(
        string repositoryRoot,
        IReadOnlyList<string> ownedWorktrees,
        string registryPath,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        List<string>? failures = null;
        foreach (var path in ownedWorktrees.Reverse())
        {
            if (!await IsRegisteredWorktreeAsync(repositoryRoot, path, logger, cancellationToken).ConfigureAwait(false))
            {
                WorktreeBuildPaths.TryDeleteDirectory(path);
                continue;
            }
            var result = await RunGitAsync(repositoryRoot, ["worktree", "remove", "--force", path], logger, cancellationToken).ConfigureAwait(false);
            if (result.ExitCode != 0)
            {
                failures ??= [];
                failures.Add(result.StandardError.Trim());
            }
        }
        if (failures is { Count: > 0 })
        {
            throw new WorktreeBuildException("i18n-worktree-cleanup-failed", string.Join("; ", failures));
        }
        File.Delete(registryPath);
    }

    internal static async Task RecoverOwnedWorktreesAsync(
        string repositoryRoot,
        string runsRoot,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        foreach (var runRoot in Directory.EnumerateDirectories(runsRoot))
        {
            var registry = Path.Combine(runRoot, "owned-worktrees.txt");
            if (!File.Exists(registry))
            {
                WorktreeBuildPaths.TryDeleteDirectory(runRoot);
                continue;
            }
            var paths = (await File.ReadAllLinesAsync(registry, cancellationToken).ConfigureAwait(false))
                .Where(path => WorktreeBuildPaths.IsSameOrChild(runRoot, path))
                .Distinct(PlatformPathHelper.PathComparison == StringComparison.OrdinalIgnoreCase
                    ? StringComparer.OrdinalIgnoreCase
                    : StringComparer.Ordinal)
                .ToArray();
            await CleanupOwnedWorktreesAsync(repositoryRoot, paths, registry, logger, cancellationToken).ConfigureAwait(false);
            WorktreeBuildPaths.TryDeleteDirectory(runRoot);
            logger.Warn($"event=build.i18n.worktree.recovered run={Path.GetFileName(runRoot)} worktrees={paths.Length}");
        }
    }

    private static async Task<bool> IsRegisteredWorktreeAsync(
        string repositoryRoot,
        string path,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var result = await RunGitAsync(repositoryRoot, ["worktree", "list", "--porcelain"], logger, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new WorktreeBuildException("i18n-worktree-cleanup-failed", result.StandardError.Trim());
        }
        return result.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.StartsWith("worktree ", StringComparison.Ordinal))
            .Select(line => line["worktree ".Length..].TrimEnd('\r'))
            .Any(candidate => string.Equals(
                PathUtils.GetCanonicalFullPath(candidate),
                PathUtils.GetCanonicalFullPath(path),
                PlatformPathHelper.PathComparison));
    }

    private static IReadOnlyList<string> ResolveInputPaths(
        AppConfig config,
        ResolvedConfigPath resolved,
        string repositoryRoot,
        ILogger logger)
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        var siteRepositoryRelative = WorktreeBuildPaths.RelativeInside(repositoryRoot, resolved.RootDir, "Site root");
        void Add(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) && !Directory.Exists(path))
            {
                return;
            }
            paths.Add(WorktreeBuildPaths.RelativeInside(repositoryRoot, path, "Build input"));
            var lexicalRelative = Path.GetRelativePath(resolved.RootDir, path);
            if (!Path.IsPathRooted(lexicalRelative) &&
                lexicalRelative != ".." &&
                !lexicalRelative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                paths.Add(Path.Combine(siteRepositoryRelative, lexicalRelative));
            }
            ValidateSymlinkTargets(path, repositoryRoot);
        }

        Add(resolved.FullConfigPath);
        foreach (var themeInput in I18nWorktreeProtocol.ResolveThemeInputPaths(resolved.RootDir, config.Theme, logger))
        {
            Add(themeInput);
        }
        Add(Path.Combine(resolved.RootDir, "data"));
        foreach (var source in config.Content.Sources ?? [])
        {
            if (source.Markdown is { } markdown)
            {
                Add(BuildPathUtils.MakeAbsolute(resolved.RootDir, markdown.Dir));
            }
        }
        if (config.Content.RouteMetadata?.Source is { Length: > 0 } routeMetadata)
        {
            Add(BuildPathUtils.MakeAbsolute(resolved.RootDir, routeMetadata));
        }
        Add(Path.Combine(resolved.RootDir, ".bukit", "plugins.yaml"));
        Add(Path.Combine(resolved.RootDir, "plugins"));
        return paths.Order(StringComparer.Ordinal).ToArray();
    }

    private static void ValidateSymlinkTargets(string path, string repositoryRoot)
    {
        var pending = new Stack<string>();
        pending.Push(path);
        while (pending.TryPop(out var entry))
        {
            FileSystemInfo info = Directory.Exists(entry) ? new DirectoryInfo(entry) : new FileInfo(entry);
            if (info.LinkTarget is { } &&
                (info.ResolveLinkTarget(returnFinalTarget: true)?.FullName is not { } target ||
                 !WorktreeBuildPaths.IsSameOrChild(repositoryRoot, target)))
            {
                throw new WorktreeBuildException("i18n-worktree-input-untracked", $"Required build input symlink leaves the Git repository: {entry}");
            }
            if (info is DirectoryInfo && info.LinkTarget is null)
            {
                foreach (var child in Directory.EnumerateFileSystemEntries(entry))
                {
                    pending.Push(child);
                }
            }
        }
    }

    internal static async Task<I18nGitCommandResult> RunGitAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var startInfo = NewProcessStartInfo("git");
        startInfo.WorkingDirectory = workingDirectory;
        startInfo.ArgumentList.Add("-C");
        startInfo.ArgumentList.Add(workingDirectory);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
        try
        {
            return await I18nWorktreeGitRunner.RunAsync(startInfo, GitTimeout, logger, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            throw new WorktreeBuildException("i18n-worktree-git-required", "Git executable is unavailable.", ex);
        }
    }

    internal static async Task<string> RequireGitOutputAsync(
        string root,
        IReadOnlyList<string> arguments,
        ILogger logger,
        string code,
        CancellationToken cancellationToken)
    {
        var result = await RunGitAsync(root, arguments, logger, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            throw new WorktreeBuildException(code, result.StandardError.Trim());
        }
        return result.StandardOutput;
    }

    internal static ProcessStartInfo NewProcessStartInfo(string fileName)
        => new()
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
}

internal sealed record GitRepository(string Root, string Head);
