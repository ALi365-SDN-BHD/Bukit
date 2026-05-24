// DESKTOP-REMOVED: ExternalAssemblyPluginSource disabled (AOT-only, no dynamic assembly loading).
#if false
using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography;
using System.Runtime.Loader;
#endif
using Bukit.Config;
using Bukit.Engine.Plugins.Protocol;
using Bukit.Shared;

namespace Bukit.Engine.Plugins;

public interface IPluginSource
{
    IEnumerable<IBukitPlugin> GetPlugins();
}

public sealed class BuiltInPluginSource : IPluginSource
{
    public IEnumerable<IBukitPlugin> GetPlugins()
    {
        yield return new BuiltIn.DataFilesPlugin();
        yield return new BuiltIn.PagesIndexPlugin();
        yield return new BuiltIn.TaxonomyPlugin();
        yield return new BuiltIn.SitemapPlugin();
        yield return new BuiltIn.FeedPlugin();
        yield return new BuiltIn.SearchIndexPlugin();
        yield return new BuiltIn.PaginationPlugin();
        yield return new BuiltIn.ArchivePlugin();
        yield return new BuiltIn.RelatedContentPlugin();
        yield return new BuiltIn.AliasPlugin();
        yield return new BuiltIn.LlmsTxtPlugin();
        yield return new BuiltIn.MenuPlugin();
        yield return new BuiltIn.ImageProcessingPlugin();
    }
}

// DESKTOP-REMOVED: ExternalAssemblyPluginSource disabled (AOT-only, no dynamic assembly loading).
#if false
public sealed class ExternalAssemblyPluginSource : IPluginSource
{
    private static readonly object ResolvingLock = new();
    private static readonly ConcurrentDictionary<string, byte> PluginDirectories = new(StringComparer.OrdinalIgnoreCase);
    private static bool _resolvingHandlerRegistered;
    private static int _resolvingHandlerRegistrationCount;
    private readonly string _pluginsDir;
    private readonly ILogger _logger;
    private readonly string _trustMode;
    private readonly IReadOnlyDictionary<string, string>? _allowlist;

    public ExternalAssemblyPluginSource(string rootDir, ILogger logger, SiteConfig siteConfig)
    {
        _pluginsDir = Path.Combine(rootDir, "plugins");
        _logger = logger;
        _trustMode = (siteConfig.ExternalAssemblyTrustMode ?? "warn").Trim().ToLowerInvariant();
        _allowlist = siteConfig.ExternalAssemblyAllowlist;
    }

    public IEnumerable<IBukitPlugin> GetPlugins()
    {
        if (!Directory.Exists(_pluginsDir))
        {
            yield break;
        }

        RegisterPluginsDirectory(_pluginsDir);
        EnsureResolvingHandlerRegistered();

        foreach (var path in Directory.EnumerateFiles(_pluginsDir, "*.dll"))
        {
            if (!TryAllow(path))
            {
                continue;
            }

            Assembly? assembly;
            try
            {
                assembly = Assembly.LoadFrom(path);
            }
            catch (Exception ex) when (ex is FileLoadException or BadImageFormatException or FileNotFoundException)
            {
                _logger.Warn($"Failed to load plugin assembly '{path}': {ex.Message}");
                continue;
            }

            foreach (var type in GetLoadableTypes(assembly))
            {
                if (type is null ||
                    type.IsAbstract ||
                    type.IsInterface ||
                    !typeof(IBukitPlugin).IsAssignableFrom(type))
                {
                    continue;
                }

                IBukitPlugin? instance = null;
                try
                {
                    instance = Activator.CreateInstance(type) as IBukitPlugin;
                }
                catch (Exception ex)
                {
                    _logger.Warn($"Failed to create plugin '{type.FullName}' from '{path}': {ex.Message}");
                }

                if (instance is not null)
                {
                    yield return instance;
                }
            }
        }
    }

    private bool TryAllow(string path)
    {
        if (_allowlist is null || _allowlist.Count == 0)
        {
            if (_trustMode == "warn")
            {
                _logger.Warn("External assembly allowlist is not configured. Loading external plugins without hash verification.");
                return true;
            }

            throw new InvalidOperationException("site.externalAssemblyAllowlist is required when site.externalAssemblyTrustMode is strict.");
        }

        var fileName = Path.GetFileName(path);
        if (!_allowlist.TryGetValue(fileName, out var expectedHash) || string.IsNullOrWhiteSpace(expectedHash))
        {
            var message = $"External plugin assembly '{fileName}' is not allowed by site.externalAssemblyAllowlist.";
            if (_trustMode == "warn")
            {
                _logger.Warn(message);
                return false;
            }

            throw new InvalidOperationException(message);
        }

        var actualHash = ComputeSha256Hex(path);
        if (!string.Equals(actualHash, expectedHash.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            var message = $"External plugin assembly '{fileName}' hash does not match allowlist.";
            if (_trustMode == "warn")
            {
                _logger.Warn(message);
                return false;
            }

            throw new InvalidOperationException(message);
        }

        return true;
    }

    private static string ComputeSha256Hex(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
    }

    private static void RegisterPluginsDirectory(string pluginsDir)
    {
        PluginDirectories[Path.GetFullPath(pluginsDir)] = 0;
    }

    private static void EnsureResolvingHandlerRegistered()
    {
        lock (ResolvingLock)
        {
            if (_resolvingHandlerRegistered)
            {
                return;
            }

            AssemblyLoadContext.Default.Resolving += ResolveFromKnownPluginDirectories;
            _resolvingHandlerRegistered = true;
            _resolvingHandlerRegistrationCount++;
        }
    }

    private static Assembly? ResolveFromKnownPluginDirectories(AssemblyLoadContext context, AssemblyName assemblyName)
    {
        if (assemblyName.Name is null)
        {
            return null;
        }

        foreach (var pluginsDir in PluginDirectories.Keys)
        {
            var candidatePath = Path.Combine(pluginsDir, assemblyName.Name + ".dll");
            if (!File.Exists(candidatePath))
            {
                continue;
            }

            try
            {
                return context.LoadFromAssemblyPath(Path.GetFullPath(candidatePath));
            }
            catch
            {
            }
        }

        return null;
    }

    internal static int ResolvingHandlerRegistrationCount => _resolvingHandlerRegistrationCount;

    internal static void ResetResolvingRegistrationForTests()
    {
        lock (ResolvingLock)
        {
            _resolvingHandlerRegistered = false;
            _resolvingHandlerRegistrationCount = 0;
            PluginDirectories.Clear();
            AssemblyLoadContext.Default.Resolving -= ResolveFromKnownPluginDirectories;
        }
    }

    private static IEnumerable<Type?> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types;
        }
    }
}
#endif

public static class PluginRegistry
{
    private const string CacheKey = "__plugin_registry_cache";
    private sealed class PluginCacheEntry
    {
        public required IReadOnlyList<(IBukitPlugin Plugin, string Source)> Plugins { get; init; }
    }

    private static int _cacheBuildCount;

    public static IEnumerable<(IBukitPlugin Plugin, string Source)> GetAllPlugins(BuildContext context)
    {
        if (TryGetCached(context, out var cached))
        {
            foreach (var item in cached.Plugins)
            {
                yield return item;
            }

            yield break;
        }

        lock (context.Data)
        {
            if (!TryGetCached(context, out cached))
            {
                cached = new PluginCacheEntry
                {
                    Plugins = BuildPlugins(context)
                };
                context.Data[CacheKey] = cached;
                _cacheBuildCount++;
            }
        }

        foreach (var item in cached.Plugins)
        {
            yield return item;
        }
    }

    private static IReadOnlyList<(IBukitPlugin Plugin, string Source)> BuildPlugins(BuildContext context)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<(IBukitPlugin Plugin, string Source)>();

        var sources = new (IPluginSource Source, string Name)[]
        {
            (new BuiltInPluginSource(), "built-in"),
            (new ExternalProtocolPluginSource(context), "external-protocol")
        };

        foreach (var (source, name) in sources)
        {
            foreach (var plugin in source.GetPlugins())
            {
                if (plugin is not null)
                {
                    var key = $"{plugin.Name}@{plugin.Version}";
                    if (seen.Add(key))
                    {
                        result.Add((plugin, name));
                    }
                }
            }
        }

        return result;
    }

    internal static int CacheBuildCountForTests => _cacheBuildCount;

    internal static void ResetCacheForTests()
    {
        _cacheBuildCount = 0;
    }

    private static bool TryGetCached(BuildContext context, out PluginCacheEntry cached)
    {
        if (context.Data.TryGetValue(CacheKey, out var value) && value is PluginCacheEntry entry)
        {
            cached = entry;
            return true;
        }

        cached = null!;
        return false;
    }
}
