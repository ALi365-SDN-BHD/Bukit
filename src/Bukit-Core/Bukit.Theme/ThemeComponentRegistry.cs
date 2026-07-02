namespace Bukit.Theme;

public sealed class ThemeComponentRegistry
{
    private readonly string _themeRoot;
    private readonly ThemeComponentRegistry? _parentRegistry;
    private readonly IReadOnlyDictionary<string, ThemeSectionDefinition> _sections;
    private readonly IReadOnlyDictionary<string, ThemeComponentDefinition> _components;

    public IReadOnlyDictionary<string, ThemeSectionDefinition> Sections => _sections;
    public IReadOnlyDictionary<string, ThemeComponentDefinition> Components => _components;
    public string ThemeRoot => _themeRoot;

    public ThemeComponentRegistry(string themeRoot, ThemeManifestV2 manifest, ThemeComponentRegistry? parentRegistry = null)
    {
        _themeRoot = themeRoot;
        _parentRegistry = parentRegistry;

        _sections = BuildSectionsMap(manifest);
        _components = BuildComponentsMap(manifest);
    }

    private IReadOnlyDictionary<string, ThemeSectionDefinition> BuildSectionsMap(ThemeManifestV2 manifest)
    {
        var map = new Dictionary<string, ThemeSectionDefinition>(StringComparer.OrdinalIgnoreCase);

        if (manifest.Sections is not null)
        {
            foreach (var kv in manifest.Sections)
            {
                map[kv.Key] = kv.Value;
            }
        }

        return map;
    }

    private IReadOnlyDictionary<string, ThemeComponentDefinition> BuildComponentsMap(ThemeManifestV2 manifest)
    {
        var map = new Dictionary<string, ThemeComponentDefinition>(StringComparer.OrdinalIgnoreCase);

        if (manifest.Components is not null)
        {
            foreach (var kv in manifest.Components)
            {
                map[kv.Key] = kv.Value;
            }
        }

        return map;
    }

    public string? ResolveSectionTemplate(string sectionName, string? variant = null)
    {
        if (!_sections.TryGetValue(sectionName, out var def))
        {
            return _parentRegistry?.ResolveSectionTemplate(sectionName, variant);
        }

        if (variant is not null && def.Variants is not null && def.Variants.TryGetValue(variant, out var vd))
        {
            return ResolveTemplatePath(_themeRoot, vd.Template);
        }

        return ResolveTemplatePath(_themeRoot, def.Template);
    }

    public ThemeSectionDefinition? ResolveSection(string sectionName)
    {
        if (_sections.TryGetValue(sectionName, out var def)) return def;
        return _parentRegistry?.ResolveSection(sectionName);
    }

    public string? ResolveComponentTemplate(string componentName)
    {
        if (!_components.TryGetValue(componentName, out var def))
        {
            return _parentRegistry?.ResolveComponentTemplate(componentName);
        }

        return ResolveTemplatePath(_themeRoot, def.Template);
    }

    public ThemeComponentDefinition? ResolveComponent(string componentName)
    {
        if (_components.TryGetValue(componentName, out var def)) return def;
        return _parentRegistry?.ResolveComponent(componentName);
    }

    public string? ResolveLayoutTemplate(string layoutName)
    {
        var layoutsDir = Path.Combine(_themeRoot, "layouts");
        var filePath = Path.Combine(layoutsDir, $"{layoutName}.html");
        if (File.Exists(filePath)) return filePath;

        if (_parentRegistry is not null)
        {
            var parentLayouts = Path.Combine(_parentRegistry._themeRoot, "layouts");
            var parentPath = Path.Combine(parentLayouts, $"{layoutName}.html");
            if (File.Exists(parentPath)) return parentPath;
        }

        return null;
    }

    public string? ResolvePageTemplate(string templateName)
    {
        var pagesDir = Path.Combine(_themeRoot, "layouts", "pages");
        var filePath = Path.Combine(pagesDir, $"{templateName}.html");
        if (File.Exists(filePath)) return filePath;

        return _parentRegistry?.ResolvePageTemplate(templateName);
    }

    public IEnumerable<string> GetAllSectionNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in _sections.Keys) names.Add(key);
        if (_parentRegistry is not null)
        {
            foreach (var key in _parentRegistry.GetAllSectionNames()) names.Add(key);
        }
        return names;
    }

    public IEnumerable<string> GetAllComponentNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in _components.Keys) names.Add(key);
        if (_parentRegistry is not null)
        {
            foreach (var key in _parentRegistry.GetAllComponentNames()) names.Add(key);
        }
        return names;
    }

    private static string? ResolveTemplatePath(string themeRoot, string? relativeTemplatePath)
    {
        if (string.IsNullOrWhiteSpace(relativeTemplatePath) || Path.IsPathRooted(relativeTemplatePath))
        {
            return null;
        }

        var layoutsRoot = Path.GetFullPath(Path.Combine(themeRoot, "layouts"));
        var safeRoot = layoutsRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(layoutsRoot, relativeTemplatePath));
        return candidate.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase) ? candidate : null;
    }
}
