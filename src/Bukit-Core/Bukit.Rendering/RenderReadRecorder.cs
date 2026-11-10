namespace Bukit.Rendering;

/// <summary>
/// Collects the shared data collections one render read, so incremental builds can skip a page
/// when nothing it read changed. The engine opens a recorder around a synchronous render call;
/// the Scriban renderer reports every template it used. A render that reports nothing, or uses
/// something the analysis cannot follow, counts as reading every collection.
/// </summary>
/// <remarks>
/// Collection keys: <c>site.data.&lt;key&gt;</c>, <c>site.modules.&lt;key&gt;</c>,
/// <c>site.data_index.&lt;key&gt;</c>, <c>site.related_pages</c>, <c>site.data_files</c> and
/// <c>pages</c>, with keys in lower case. <c>site.data.*</c> (and the same for modules and
/// data_index) means every key in that scope; <see cref="All"/> means every collection.
/// </remarks>
internal sealed class RenderReadRecorder
{
    internal const string All = "*";
    internal const string Pages = "pages";

    [ThreadStatic]
    private static RenderReadRecorder? t_current;

    // Names templates can use without reading shared data: model roots other than site/pages,
    // engine-provided functions, and Scriban built-ins. Unknown names count as reading everything.
    private static readonly HashSet<string> s_safeRoots = new(StringComparer.OrdinalIgnoreCase)
    {
        "page", "seo", "items", "pagination", "collection", "taxonomy", "filter", "content",
        "section", "comp", "render_section", "render_component", "image", "util", "shortcode",
        "include", "array", "date", "html", "math", "object", "regex", "string", "timespan",
        "empty", "blank", "for", "while", "tablerow"
    };

    private readonly HashSet<string> _keys = new(StringComparer.Ordinal);
    private readonly HashSet<string> _roots = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _declared = new(StringComparer.OrdinalIgnoreCase);
    private bool _tracked;
    private bool _all;
    private bool _templatePages;
    private bool _pagesAreListItems;

    internal static RenderReadRecorder? Current => t_current;

    internal static Scope Begin()
    {
        var recorder = new RenderReadRecorder();
        var previous = t_current;
        t_current = recorder;
        return new Scope(recorder, previous);
    }

    /// <summary>Marks the render as reported by a renderer that records its templates.</summary>
    internal void MarkTracked() => _tracked = true;

    /// <summary>In a list render, the root <c>pages</c> holds the list items, not every page.</summary>
    internal void MarkPagesAsListItems() => _pagesAreListItems = true;

    internal void RecordAll() => _all = true;

    internal void RecordKey(string key) => _keys.Add(key);

    internal void Declare(string name) => _declared.Add(name);

    internal void AddTemplate(TemplateReadSet reads)
    {
        if (reads.All)
        {
            _all = true;
            return;
        }

        _keys.UnionWith(reads.Keys);
        _roots.UnionWith(reads.Roots);
        _declared.UnionWith(reads.Declarations);
        _templatePages |= reads.Pages;
    }

    /// <summary>The sorted collection keys read, or <c>["*"]</c> for every collection.</summary>
    internal string[] Complete()
    {
        if (!_tracked || _all)
        {
            return [All];
        }

        foreach (var root in _roots)
        {
            if (!s_safeRoots.Contains(root) && !_declared.Contains(root))
            {
                return [All];
            }
        }

        var keys = new SortedSet<string>(_keys, StringComparer.Ordinal);
        if (_templatePages && !_pagesAreListItems)
        {
            keys.Add(Pages);
        }

        keys.RemoveWhere(key => !key.EndsWith(".*", StringComparison.Ordinal) &&
            keys.Contains(ScopeWildcard(key)));
        return [.. keys];
    }

    private static readonly string[] s_scopes = ["site.data.", "site.modules.", "site.data_index."];

    private static string ScopeWildcard(string key)
    {
        foreach (var scope in s_scopes)
        {
            if (key.StartsWith(scope, StringComparison.Ordinal))
            {
                return scope + "*";
            }
        }

        return string.Empty;
    }

    internal readonly struct Scope(RenderReadRecorder recorder, RenderReadRecorder? previous) : IDisposable
    {
        internal RenderReadRecorder Recorder => recorder;

        public void Dispose() => t_current = previous;
    }
}

/// <summary>What one template reads: collection keys, other global names, and names it declares.</summary>
internal sealed record TemplateReadSet(
    bool All,
    IReadOnlySet<string> Keys,
    IReadOnlySet<string> Roots,
    IReadOnlySet<string> Declarations,
    bool Pages)
{
    internal static TemplateReadSet Everything { get; } = new(
        true,
        new HashSet<string>(),
        new HashSet<string>(),
        new HashSet<string>(),
        false);
}
