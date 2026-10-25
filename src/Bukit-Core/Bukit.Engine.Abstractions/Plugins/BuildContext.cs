using System.Diagnostics.CodeAnalysis;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;

namespace Bukit.Engine.Abstractions.Plugins;

public sealed class BuildContext
{
    private readonly Dictionary<string, object> _data = new(StringComparer.OrdinalIgnoreCase);

    public required string RootDir { get; init; }
    public required string OutputDir { get; init; }
    public required string BaseUrl { get; init; }
    public required string LayoutsDir { get; init; }
    public required IReadOnlyList<RoutedContentDocument> RoutedDocuments { get; init; }
    public IReadOnlyList<RouteInfo> StaticHtmlRoutes { get; set; } = Array.Empty<RouteInfo>();
    public CanonicalContentGraph ContentGraph { get; init; } = CanonicalContentGraph.Empty;
    public IContentBodyStore BodyStore { get; init; } = NullContentBodyStore.Instance;
    public IReadOnlyDictionary<string, SeoIndexEntry> SeoIndex { get; set; } = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase);
    public List<RoutedContentDocument> DerivedDocuments { get; } = new();
    public List<(RouteInfo Route, DateTimeOffset LastModified)> DerivedRoutes { get; } = new();
    public List<PluginExecutionInfo> PluginExecutions { get; } = new();

    /// <summary>
    /// Untyped view of the shared plugin data. It is the same store that the typed
    /// <see cref="Get{T}"/>, <see cref="TryGet{T}"/> and <see cref="Set{T}"/> methods use.
    /// </summary>
    [Obsolete("Use BuildDataKey<T> with Get, TryGet, Set, Remove or GetOrAdd. This untyped view will be removed in a future major version.")]
    public Dictionary<string, object> Data => _data;

    public Func<string, string>? TemplateResolver { get; init; }
    public required ILogger Logger { get; init; }

    /// <summary>Gets the value stored under <paramref name="key"/>, or throws when it is missing.</summary>
    public T Get<T>(BuildDataKey<T> key)
        where T : notnull
    {
        if (TryGet(key, out var value))
        {
            return value;
        }

        throw new KeyNotFoundException($"Build data '{key.Name}' has not been set.");
    }

    /// <summary>Reads the value stored under <paramref name="key"/>. Throws when the stored value has a different type.</summary>
    public bool TryGet<T>(BuildDataKey<T> key, [MaybeNullWhen(false)] out T value)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_data)
        {
            if (!_data.TryGetValue(key.Name, out var raw))
            {
                value = default;
                return false;
            }

            value = Cast(key, raw);
            return true;
        }
    }

    /// <summary>Stores <paramref name="value"/> under <paramref name="key"/>. Throws when the name already holds a value of another type.</summary>
    public void Set<T>(BuildDataKey<T> key, T value)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        lock (_data)
        {
            if (_data.TryGetValue(key.Name, out var existing))
            {
                _ = Cast(key, existing);
            }

            _data[key.Name] = value;
        }
    }

    /// <summary>Removes the value stored under <paramref name="key"/>.</summary>
    public bool Remove<T>(BuildDataKey<T> key)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_data)
        {
            return _data.Remove(key.Name);
        }
    }

    /// <summary>Returns the existing value, or atomically stores and returns the one created by <paramref name="factory"/>.</summary>
    public T GetOrAdd<T>(BuildDataKey<T> key, Func<T> factory)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(factory);
        lock (_data)
        {
            if (_data.TryGetValue(key.Name, out var raw))
            {
                return Cast(key, raw);
            }

            var created = factory();
            ArgumentNullException.ThrowIfNull(created, nameof(factory));
            _data[key.Name] = created;
            return created;
        }
    }

    /// <summary>Read-only copy of the shared data by name, used for the template <c>site.data</c> projection.</summary>
    internal IReadOnlyDictionary<string, object> SnapshotData()
    {
        lock (_data)
        {
            return new Dictionary<string, object>(_data, StringComparer.OrdinalIgnoreCase);
        }
    }

    public string ResolveTemplateKind(string kind)
    {
        if (TemplateResolver is null)
        {
            throw new ConfigException(
                $"No template resolver is available for plugin template kind '{kind}'.",
                DiagnosticCode.ConfigInvalidValue);
        }

        return TemplateResolver(kind);
    }

    private static T Cast<T>(BuildDataKey<T> key, object raw)
        where T : notnull
    {
        if (raw is T typed)
        {
            return typed;
        }

        throw new InvalidOperationException(
            $"Build data '{key.Name}' holds {raw.GetType().FullName}, but the key expects {typeof(T).FullName}.");
    }
}
