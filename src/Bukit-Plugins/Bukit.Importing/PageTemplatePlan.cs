namespace Bukit.Importing;

/// <summary>
/// Decides which template renders each demo page. Every page gets a template of its
/// own; the fallback names are reserved for generic templates that never carry a
/// specific page's markup or assets.
/// </summary>
internal sealed class PageTemplatePlan
{
    /// <summary>Generic templates Core falls back to when content has no own template.</summary>
    internal static readonly IReadOnlySet<string> FallbackNames =
        new HashSet<string>(["page", "post", "company", "service", "list"], StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, string> _names;
    private readonly Dictionary<string, RouteMapPage> _ownedEntries;

    private PageTemplatePlan(Dictionary<string, string> names, Dictionary<string, RouteMapPage> ownedEntries)
    {
        _names = names;
        _ownedEntries = ownedEntries;
    }

    /// <summary>
    /// The route-map route for this page, or null. Entries match by file name, so when
    /// several files share one entry only the first takes its route; the others keep
    /// their own slug routes instead of colliding on the same URL.
    /// </summary>
    internal string? RouteFor(DiscoveredPage page)
    {
        if (!_ownedEntries.TryGetValue(Key(page.RelativePath), out var entry))
            return null;

        var route = entry.Route.Trim();
        return string.IsNullOrEmpty(route) || route.Contains('{') ? null : route;
    }

    /// <summary>Template file name without extension, e.g. "about".</summary>
    internal string NameFor(DiscoveredPage page) => _names[Key(page.RelativePath)];

    /// <summary>Template path as Core references it, e.g. "pages/about.html".</summary>
    internal string PathFor(DiscoveredPage page) => $"pages/{NameFor(page)}.html";

    internal static PageTemplatePlan Build(IReadOnlyList<DiscoveredPage> pages, RouteMapConfig? routeMap)
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var used = new HashSet<string>(FallbackNames, StringComparer.OrdinalIgnoreCase);
        var explicitOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // The first page matching a route-map entry owns it (its explicit template and route).
        var ownedEntries = new Dictionary<string, RouteMapPage>(StringComparer.OrdinalIgnoreCase);
        var claimedEntries = new HashSet<RouteMapPage>(ReferenceEqualityComparer.Instance);
        foreach (var page in pages)
        {
            var entry = PageClassifier.FindEntry(routeMap, Path.GetFileNameWithoutExtension(page.RelativePath));
            if (entry is not null && claimedEntries.Add(entry))
                ownedEntries[Key(page.RelativePath)] = entry;
        }

        // Roles Core binds by name (home, list kinds) claim their names first.
        foreach (var page in pages)
        {
            var key = Key(page.RelativePath);
            var explicitName = ownedEntries.ContainsKey(key) ? ExplicitName(page, routeMap) : null;
            var fixedName = page.Type switch
            {
                PageType.Home => "index",
                PageType.PostList => explicitName
                                     ?? NonEmpty(ThemeGenerator.SanitizeTemplateName(page.Slug)) ?? "insights",
                PageType.CompanyList => explicitName ?? "companies",
                PageType.ServiceList => explicitName ?? "services",
                _ => null
            };

            if (fixedName is null || !used.Add(fixedName))
                continue;

            names[key] = fixedName;
            if (explicitName is not null && fixedName == explicitName)
                explicitOwners[explicitName] = page.RelativePath;
        }

        // Core treats pages/index.html as the home template, and the importer writes a
        // generic one when the demo has no home page. No other page may take the name.
        used.Add("index");

        // Explicit route-map names next, so a derived name can never steal one. Only the
        // entry's owner uses it: same-named files in other folders (en/about.html,
        // zh/about.html) match the same entry and fall through to derived names.
        foreach (var page in pages)
        {
            var key = Key(page.RelativePath);
            if (names.ContainsKey(key) || !ownedEntries.ContainsKey(key))
                continue;

            var explicitName = ExplicitName(page, routeMap);
            if (explicitName is null)
                continue;

            if (!used.Add(explicitName))
            {
                var owner = explicitOwners.TryGetValue(explicitName, out var first) ? first : "another page";
                throw new ImportException(
                    ImportErrorKind.UserInput,
                    $"Route map pages '{owner}' and '{page.RelativePath}' both use template '{explicitName}'. " +
                    "Every page must have its own template.");
            }

            explicitOwners[explicitName] = page.RelativePath;
            names[key] = explicitName;
        }

        foreach (var page in pages)
        {
            var key = Key(page.RelativePath);
            if (!names.ContainsKey(key))
                names[key] = Claim(used, DerivedName(page));
        }

        return new PageTemplatePlan(names, ownedEntries);
    }

    // A route-map template naming a generic fallback (e.g. "page") is a type hint,
    // not a request to share that template.
    private static string? ExplicitName(DiscoveredPage page, RouteMapConfig? routeMap)
    {
        var fileName = Path.GetFileNameWithoutExtension(page.RelativePath);
        var name = NonEmpty(ThemeGenerator.SanitizeTemplateName(PageClassifier.GetTemplate(routeMap, fileName) ?? ""));
        return name is null || FallbackNames.Contains(name) || name.Equals("index", StringComparison.OrdinalIgnoreCase)
            ? null
            : name;
    }

    private static string DerivedName(DiscoveredPage page)
    {
        var withoutExtension = Path.ChangeExtension(page.RelativePath.Replace('\\', '/'), null) ?? "";
        return NonEmpty(ThemeGenerator.SanitizeTemplateName(withoutExtension.Replace('/', '-'))) ?? "page-template";
    }

    private static string Claim(HashSet<string> used, string name)
    {
        if (used.Add(name))
            return name;

        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{name}-{suffix}";
            if (used.Add(candidate))
                return candidate;
        }
    }

    private static string? NonEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string Key(string relativePath) => relativePath.Replace('\\', '/');
}
