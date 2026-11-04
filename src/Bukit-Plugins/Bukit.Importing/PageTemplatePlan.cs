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

    private PageTemplatePlan(Dictionary<string, string> names)
    {
        _names = names;
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

        // Roles Core binds by name (home, list kinds) claim their names first.
        foreach (var page in pages)
        {
            var fixedName = page.Type switch
            {
                PageType.Home => "index",
                PageType.PostList => ExplicitName(page, routeMap)
                                     ?? NonEmpty(ThemeGenerator.SanitizeTemplateName(page.Slug)) ?? "insights",
                PageType.CompanyList => ExplicitName(page, routeMap) ?? "companies",
                PageType.ServiceList => ExplicitName(page, routeMap) ?? "services",
                _ => null
            };

            if (fixedName is not null && used.Add(fixedName))
                names[Key(page.RelativePath)] = fixedName;
        }

        // Explicit route-map names next, so a derived name can never steal one.
        foreach (var page in pages)
        {
            var key = Key(page.RelativePath);
            var explicitName = names.ContainsKey(key) ? null : ExplicitName(page, routeMap);
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

        return new PageTemplatePlan(names);
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
