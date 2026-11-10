using Bukit.Engine.Abstractions.Content;
using Scriban.Runtime;

namespace Bukit.Rendering.Scriban;

/// <summary>
/// Holds the converted, read-only <c>pages</c> and <c>site</c> values for the most recent
/// source instances, so one render pass converts each shared model once instead of once
/// per page. Sources are matched by reference: the models are records, and value equality
/// would be both slow and wrong for this purpose.
/// </summary>
internal sealed class SharedModelCache
{
    [ThreadStatic]
    private static SharedModelCache? t_current;

    private Entry<IReadOnlyList<PageInfo>, ScriptArray>? _pages;
    private Entry<SiteModel, ScriptObject>? _site;

    internal ScriptArray GetPages(IReadOnlyList<PageInfo> source) =>
        GetOrCreate(ref _pages, source, static pages => ScribanListModelMapper.ToPageInfoScriptArray(pages));

    internal ScriptObject GetSite(SiteModel source) =>
        GetOrCreate(ref _site, source, static site => ScribanSiteModelMapper.ToScriptObject(site));

    /// <summary>
    /// The cache active on this thread, if any. The renderer activates its cache around the
    /// synchronous model binding call, so the governed <see cref="ScribanModelBinder"/> facade
    /// keeps its single-parameter signatures.
    /// </summary>
    internal static SharedModelCache? Current => t_current;

    internal Scope Activate()
    {
        var previous = t_current;
        t_current = this;
        return new Scope(previous);
    }

    internal readonly struct Scope(SharedModelCache? previous) : IDisposable
    {
        public void Dispose() => t_current = previous;
    }

    internal void Clear()
    {
        Volatile.Write(ref _pages, null);
        Volatile.Write(ref _site, null);
    }

    private static TValue GetOrCreate<TKey, TValue>(ref Entry<TKey, TValue>? slot, TKey source, Func<TKey, TValue> convert)
        where TKey : class
        where TValue : class
    {
        while (true)
        {
            var current = Volatile.Read(ref slot);
            if (current is not null && ReferenceEquals(current.Key, source))
            {
                return current.Value;
            }

            // Key and value are published together, so a reader never pairs one source
            // with another source's converted value.
            var created = new Entry<TKey, TValue>(source, convert);
            if (ReferenceEquals(Interlocked.CompareExchange(ref slot, created, current), current))
            {
                return created.Value;
            }
        }
    }

    /// <summary>
    /// One source and its converted, frozen value. The value is created at most once;
    /// a plain lock is used instead of Lazy&lt;T&gt;, which needs trimming annotations under AOT.
    /// </summary>
    private sealed class Entry<TKey, TValue>(TKey key, Func<TKey, TValue> convert)
        where TKey : class
        where TValue : class
    {
        private readonly Lock _gate = new();
        private TValue? _value;

        internal TKey Key { get; } = key;

        internal TValue Value
        {
            get
            {
                var value = Volatile.Read(ref _value);
                if (value is not null)
                {
                    return value;
                }

                lock (_gate)
                {
                    value = _value;
                    if (value is null)
                    {
                        value = ScriptValueFreezer.Freeze(convert(Key));
                        Volatile.Write(ref _value, value);
                    }

                    return value;
                }
            }
        }
    }
}
