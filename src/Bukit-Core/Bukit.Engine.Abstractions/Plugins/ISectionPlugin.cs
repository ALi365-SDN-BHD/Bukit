using System.Collections.Concurrent;

namespace Bukit.Engine.Abstractions.Plugins;

public enum SectionHook
{
    BeforeRender,
    AfterRender,
    ResolveItems
}

public sealed class SectionContext
{
    public required string SectionType { get; init; }
    public string? Variant { get; init; }
    public Dictionary<string, object?>? Props { get; set; }
    public string? RenderedHtml { get; set; }
    public Dictionary<string, object?> Data { get; init; } = new();
}

public interface ISectionPlugin
{
    SectionHook SupportedHook { get; }
    Task ExecuteAsync(SectionContext context, CancellationToken ct = default);
}

public static class SectionPluginRegistry
{
    private static readonly ConcurrentDictionary<string, ISectionPlugin> _plugins = new(StringComparer.OrdinalIgnoreCase);

    public static void Register(string name, ISectionPlugin plugin)
    {
        if (!_plugins.TryAdd(name, plugin))
        {
            if (_plugins.TryGetValue(name, out var existing))
            {
                throw new InvalidOperationException(
                    $"Section plugin '{name}' is already registered ({existing.GetType().FullName}). " +
                    $"Cannot register duplicate from {plugin.GetType().FullName}.");
            }
        }
    }

    public static IReadOnlyDictionary<string, ISectionPlugin> GetAll()
    {
        return new Dictionary<string, ISectionPlugin>(_plugins, StringComparer.OrdinalIgnoreCase);
    }

    public static bool TryResolve(string name, out ISectionPlugin? plugin)
    {
        return _plugins.TryGetValue(name, out plugin);
    }
}
