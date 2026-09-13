using Bukit.Config;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Shared;

namespace Bukit.Engine;

internal sealed class BuildTransaction : IDisposable
{
    private static readonly AsyncLocal<BuildTransaction?> Active = new();
    private readonly BuildTransaction? _previous;
    private readonly BuildResourceLease _lease;
    private readonly List<Target> _targets;
    private readonly ILogger _logger;
    private readonly string _logicalOutput;
    private readonly List<(string Path, string Fingerprint)> _readOnly = [];
    private readonly Action<string, string, bool> _move;
    private readonly Action<string, bool> _delete;
    private bool _committed;
    private sealed class Target(string path, bool directory)
    {
        internal readonly string Path = path;
        internal readonly bool Directory = directory;
        internal readonly string Stage = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path)!, ".bukit-txn-" + Guid.NewGuid().ToString("N") + "-stage");
        internal readonly string Backup = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path)!, ".bukit-txn-" + Guid.NewGuid().ToString("N") + "-backup");
        internal bool BackedUp;
        internal bool Installed;
    }

    internal string OutputDir { get; }
    internal string CacheDir { get; }
    internal string MediaCacheDir { get; }

    private BuildTransaction(List<Target> targets, BuildResourceLease lease, ILogger logger,
        string output, string cache, string media, string logicalOutput, Action<string, string, bool>? move, Action<string, bool>? delete)
    {
        _targets = targets;
        _logicalOutput = logicalOutput;
        _lease = lease;
        _logger = logger;
        _move = move ?? Move;
        _delete = delete ?? Delete;
        _previous = Active.Value;
        Active.Value = this;
        OutputDir = Map(output);
        CacheDir = Map(cache);
        MediaCacheDir = Map(media);
    }

    internal static BuildTransaction Begin(AppConfig config, string rootDir, ConfigOverrides overrides, ILogger logger,
        CancellationToken cancellationToken = default, Action<string, string, bool>? move = null, Action<string, bool>? delete = null)
    {
        config = ConfigApplier.Apply(config, overrides);
        ConfigValidator.Validate(config);
        var logicalRoot = Path.GetFullPath(rootDir);
        if (!Directory.Exists(logicalRoot)) throw new IOException($"Build root does not exist: {logicalRoot}");
        var logicalOutput = Path.GetFullPath(BuildPathUtils.MakeAbsolute(rootDir, config.Build.Output));
        rootDir = ResourcePath(rootDir);
        var output = ResourcePath(BuildPathUtils.MakeAbsolute(rootDir, config.Build.Output));
        var cache = ResourcePath(string.IsNullOrWhiteSpace(overrides.CacheDir) ? Path.Combine(rootDir, ".cache") : overrides.CacheDir);
        if (!BuildResourceLease.Contains(rootDir, output) || output.Split(Path.DirectorySeparatorChar).Contains(".git") || BuildResourceLease.Same(output, cache) || BuildResourceLease.Contains(output, cache) || BuildResourceLease.Contains(cache, output))
            throw new ConfigException("Unsafe or overlapping build output/cache directories. How to fix: use a dedicated output subdirectory and separate cache directory.", DiagnosticCode.BuildOutputUnsafe);
        var media = ContentProviderFactory.BuildEffectiveMediaConfig(config.Content.Media, rootDir, Path.Combine(cache, "media")).DownloadDir;
        var resources = GetResources(config, rootDir, overrides, output, cache, media);
        var theme = ThemePathResolver.Resolve(rootDir, config.Theme, logger);
        var inputs = new[] { theme.LayoutsDir, theme.AssetsDir, theme.StaticDir, theme.ParentLayoutsDir, theme.ParentAssetsDir, theme.ParentStaticDir, theme.UserLayoutsDir }
            .Where(path => path is not null).Select(path => ResourcePath(path!)).ToArray();
        foreach (var resource in resources.Where(resource => resource.Write))
            if (inputs.Any(input => BuildResourceLease.Same(input, resource.Path) || BuildResourceLease.Contains(input, resource.Path) || resource.Directory && BuildResourceLease.Contains(resource.Path, input)))
                throw new IOException($"Build resource overlaps source tree and cannot be staged safely: {resource.Path}");
        foreach (var file in resources.Where(resource => !resource.Directory))
            if (resources.Any(other => BuildResourceLease.Contains(file.Path, other.Path) || other.Directory && BuildResourceLease.Same(file.Path, other.Path)))
                throw new IOException($"A file resource cannot contain another resource: {file.Path}");
        foreach (var resource in resources.Where(r => r.Write))
        {
            if (BuildResourceLease.Same(resource.Path, rootDir) || BuildResourceLease.Contains(resource.Path, rootDir) || resource.Path.Split(Path.DirectorySeparatorChar).Contains(".git"))
                throw new IOException($"Unsafe build resource: {resource.Path}");
            if (resource.Path != output && resource.Directory && !resource.NewAncestor && (BuildResourceLease.Contains(output, resource.Path) || BuildResourceLease.Contains(resource.Path, output)))
                throw new IOException($"Output and cache resources overlap: {resource.Path}");
        }
        foreach (var reader in resources.Where(resource => !resource.Write))
            if (resources.Any(writer => writer.Write && (BuildResourceLease.Same(reader.Path, writer.Path) || reader.Directory && BuildResourceLease.Contains(reader.Path, writer.Path))))
                throw new IOException($"Read-only cache overlaps a writer: {reader.Path}");
        var targets = new List<Target>();
        foreach (var resource in resources.Where(r => r.Write).OrderBy(r => r.Path.Length))
        {
            if (targets.Any(t => t.Directory && (BuildResourceLease.Same(t.Path, resource.Path) || BuildResourceLease.Contains(t.Path, resource.Path)))) continue;
            if (targets.Any(t => BuildResourceLease.Same(t.Path, resource.Path))) throw new IOException($"Duplicate incompatible resource: {resource.Path}");
            targets.Add(new Target(resource.Path, resource.Directory));
        }
        var lease = BuildResourceLease.Acquire(resources);
        BuildTransaction? transaction = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Validate the formal target, never a conveniently empty staging directory.
            BuildPlanner.ValidateOutputDirectory(config, logicalRoot, logicalOutput, overrides);
            transaction = new(targets, lease, logger, output, cache, ResourcePath(media), logicalOutput, move, delete);
            foreach (var target in targets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!Directory.Exists(Path.GetDirectoryName(target.Stage))) throw new IOException($"Build resource parent disappeared: {target.Path}");
                if (target.Directory)
                {
                    Directory.CreateDirectory(target.Stage);
                    if (target.Path == output && !config.Build.Clean) RejectGeneratedOutputLinks(output, config);
                    if (Directory.Exists(target.Path) && !(config.Build.Clean && target.Path == output)) CopyDirectory(target.Path, target.Stage, cancellationToken, target.Path == output);
                    else if (File.Exists(target.Path)) throw new IOException($"A file occupies directory resource: {target.Path}");
                }
                else if (File.Exists(target.Path)) File.Copy(target.Path, target.Stage);
                else if (Directory.Exists(target.Path)) throw new IOException($"A directory occupies file resource: {target.Path}");
            }
            foreach (var resource in resources.Where(r => !r.Write))
                transaction._readOnly.Add((resource.Path, Fingerprint(transaction.Map(resource.Path))));
            return transaction;
        }
        catch { if (transaction is null) lease.Dispose(); else transaction.Dispose(); throw; }
    }

    internal static IReadOnlyList<string> WatchResources(AppConfig config, string root, string output, string cache)
    {
        var media = ContentProviderFactory.BuildEffectiveMediaConfig(config.Content.Media, root, Path.Combine(cache, "media")).DownloadDir;
        return GetResources(config, root, new() { CacheDir = cache }, output, cache, media).Where(resource => resource.Write).Select(resource => resource.Path).ToArray();
    }

    private static List<BuildResourceLease.Resource> GetResources(AppConfig config, string rootDir, ConfigOverrides overrides, string output, string cache, string media)
    {
        var resources = new List<BuildResourceLease.Resource> { new(output, true, true), new(cache, true, true), new(ResourcePath(media), true, config.Content.Media.DownloadToLocal) };
        foreach (var source in config.Content.Sources ?? [])
        {
            if (!string.Equals(source.Type, "notion", StringComparison.OrdinalIgnoreCase) || source.Notion is not { } notion) continue;
            var mode = (notion.CacheMode ?? "off").Trim().ToLowerInvariant();
            if (mode is not ("readonly" or "readwrite")) continue;
            var path = string.IsNullOrWhiteSpace(notion.CacheDir) ? Path.Combine(rootDir, ".cache", "notion") : BuildPathUtils.MakeAbsolute(rootDir, notion.CacheDir);
            resources.Add(new(ResourcePath(path), true, mode == "readwrite"));
        }
        if (config.Site.Plugins?.GetValueOrDefault("pages-index")?.Enabled != false && PagesIndexConfigHelper.HasNotionContent(config) && config.Theme.Params is { } parameters &&
            PagesIndexConfigHelper.TryGetMap(parameters, "pages_index", out var pages) && PagesIndexConfigHelper.TryGetMap(pages, "resolve_notion", out var resolve) && PagesIndexConfigHelper.TryGetBool(resolve, "enabled", false) && PagesIndexConfigHelper.TryGetStringList(resolve, "field_keys").Count > 0 && PagesIndexConfigHelper.TryGetInt(resolve, "max_items", 200) > 0)
        {
            var mode = PagesIndexCacheHelper.NormalizeCacheMode(PagesIndexConfigHelper.TryGetString(resolve, "cache_mode") ?? "readwrite");
            if (mode != "off") resources.Add(new(ResourcePath(PagesIndexCacheHelper.ResolveCachePath(rootDir, PagesIndexConfigHelper.TryGetString(resolve, "cache_path"))), false, mode == "readwrite"));
        }
        if (!string.IsNullOrWhiteSpace(overrides.MetricsPath))
        {
            var path = BuildPathUtils.MakeAbsolute(rootDir, overrides.MetricsPath);
            resources.Add(new(ResourcePath(path), false, true));
            resources.Add(new(ResourcePath(Path.ChangeExtension(path, ".html")), false, true));
            if (BuildResourceLease.Same(resources[^1].Path, resources[^2].Path)) throw new IOException("Metrics JSON and HTML require distinct paths.");
        }
        // Missing ancestors are themselves a write resource. Stage and publish that empty
        // subtree once, rather than creating formal cache/metrics parents before success.
        foreach (var resource in resources.Where(resource => resource.Write).ToArray())
        {
            string? missing = null;
            for (var parent = Path.GetDirectoryName(resource.Path); parent is not null && !Directory.Exists(parent); parent = Path.GetDirectoryName(parent))
            {
                if (File.Exists(parent)) throw new IOException($"A file occupies resource parent: {parent}");
                missing = parent;
            }
            if (missing is not null) resources.Add(new(missing, true, true, NewAncestor: true));
        }
        return resources;
    }

    // A nested Engine call inherits AsyncLocal state. Claims always use formal identities,
    // even when an existing resolver has already mapped a path for its parent build.
    private static string ResourcePath(string path) => BuildResourceLease.Canonical(Logical(path));

    internal static string PhysicalRoot(string root) => Active.Value is null ? root : BuildResourceLease.Canonical(root);
    internal static string Physical(string path) => Active.Value?.Map(path) ?? path;
    internal static string Logical(string path)
    {
        var full = Path.GetFullPath(path);
        if (Active.Value is { } transaction && (BuildResourceLease.Same(full, transaction.OutputDir) || BuildResourceLease.Contains(transaction.OutputDir, full)))
            return transaction._logicalOutput + full[transaction.OutputDir.Length..];
        foreach (var target in Active.Value?._targets ?? [])
            if (BuildResourceLease.Same(full, target.Stage) || target.Directory && BuildResourceLease.Contains(target.Stage, full))
                return target.Path + full[target.Stage.Length..];
        return path;
    }
    internal static string DefaultMediaCache(string fallback) => Active.Value?.MediaCacheDir ?? fallback;

    private string Map(string path)
    {
        var full = BuildResourceLease.Canonical(path);
        foreach (var target in _targets)
            if (BuildResourceLease.Same(full, target.Path) || target.Directory && BuildResourceLease.Contains(target.Path, full))
                return target.Stage + full[target.Path.Length..];
        return path;
    }

    internal void Commit(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var resource in _readOnly)
            if (Fingerprint(Map(resource.Path)) != resource.Fingerprint) throw new IOException($"Read-only cache was modified: {resource.Path}");
        try
        {
            foreach (var target in _targets)
            {
                if (!Exists(target.Stage, target.Directory)) continue;
                if (Exists(target.Path, target.Directory)) { _move(target.Path, target.Backup, target.Directory); target.BackedUp = true; }
                _move(target.Stage, target.Path, target.Directory);
                target.Installed = true;
            }
        }
        catch (Exception original)
        {
            var failures = new List<Exception> { original };
            foreach (var target in _targets.AsEnumerable().Reverse())
            {
                try
                {
                    if (target.Installed) _delete(target.Path, target.Directory);
                    if (target.BackedUp) { _move(target.Backup, target.Path, target.Directory); target.BackedUp = false; }
                }
                catch (Exception recovery) { failures.Add(new IOException($"Rollback failed; preserve backup '{target.Backup}' for target '{target.Path}'.", recovery)); }
            }
            throw new AggregateException("Build transaction commit failed; rollback attempted.", failures);
        }
        _committed = true;
        foreach (var target in _targets.Where(t => t.BackedUp))
            try { _delete(target.Backup, target.Directory); target.BackedUp = false; }
            catch (Exception ex) { _logger.Warn($"Build committed; backup cleanup failed: {target.Backup}: {ex.Message}"); }
    }

    private static void RejectGeneratedOutputLinks(string output, AppConfig config)
    {
        var paths = new List<string> { ".bukit", ".bukit-output-marker", ".bukit-build-state.json" };
        paths.AddRange(PublicOutputLifecycle.ProjectionPlan(config, []).Select(item => item.Destination));
        foreach (var language in I18nOutputMerger.GetLanguages(config.Site))
        {
            paths.Add(language);
            paths.AddRange(PublicOutputLifecycle.ProjectionPlan(config, []).Select(item => Path.Combine(language, item.Destination)));
            paths.Add(Path.Combine(language, ".bukit"));
        }
        foreach (var relative in paths)
        {
            var current = output;
            foreach (var part in relative.Split('/', Path.DirectorySeparatorChar))
            {
                current = Path.Combine(current, part);
                try { if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new IOException($"Generated output cannot traverse a symbolic link: {current}"); }
                catch (FileNotFoundException) { break; }
                catch (DirectoryNotFoundException) { break; }
            }
        }
    }

    private static string Fingerprint(string path)
    {
        if (!Directory.Exists(path) && !File.Exists(path)) return "missing";
        IEnumerable<string> files = Directory.Exists(path) ? Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal) : [path];
        IEnumerable<string> directories = Directory.Exists(path) ? Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories).Select(directory => "dir:" + Path.GetRelativePath(path, directory)).Order(StringComparer.Ordinal) : [];
        return string.Join("\n", directories) + "\n" + string.Join("\n", files.Select(file => Path.GetRelativePath(path, file) + ":" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file)))));
    }

    private static void CopyDirectory(string source, string destination, CancellationToken cancellationToken, bool preserveLinks)
    {
        foreach (var entry in new DirectoryInfo(source).EnumerateFileSystemInfos())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = Path.Combine(destination, entry.Name);
            if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                if (!preserveLinks) throw new IOException($"Cannot safely stage cache symbolic link: {entry.FullName}");
                var link = entry.LinkTarget ?? throw new IOException($"Cannot read symbolic link: {entry.FullName}");
                if (Path.IsPathRooted(link))
                {
                    var mapped = Physical(link);
                    var owner = Active.Value?._targets.FirstOrDefault(target => target.Directory && BuildResourceLease.Contains(target.Stage, path));
                    if (owner is not null && (BuildResourceLease.Same(owner.Stage, mapped) || BuildResourceLease.Contains(owner.Stage, mapped)))
                        link = Path.GetRelativePath(Path.GetDirectoryName(path)!, mapped);
                }
                if (entry is DirectoryInfo) Directory.CreateSymbolicLink(path, link); else File.CreateSymbolicLink(path, link);
                continue;
            }
            if (entry is DirectoryInfo) { Directory.CreateDirectory(path); CopyDirectory(entry.FullName, path, cancellationToken, preserveLinks); }
            else File.Copy(entry.FullName, path);
        }
    }
    private static bool Exists(string path, bool directory) => directory ? Directory.Exists(path) : File.Exists(path);
    private static void Move(string from, string to, bool directory) { if (directory) Directory.Move(from, to); else File.Move(from, to); }
    private static void Delete(string path, bool directory) { if (directory) Directory.Delete(path, true); else File.Delete(path); }
    public void Dispose()
    {
        Active.Value = _previous;
        foreach (var target in _targets)
            try { if (Exists(target.Stage, target.Directory)) _delete(target.Stage, target.Directory); }
            catch (Exception ex) { _logger.Warn($"Build staging cleanup failed (committed={_committed}): {target.Stage}: {ex.Message}"); }
        _lease.Dispose();
    }
}
