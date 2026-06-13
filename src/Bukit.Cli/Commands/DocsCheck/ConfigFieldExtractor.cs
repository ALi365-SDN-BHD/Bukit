using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.RegularExpressions;
using Bukit.Config;

namespace Bukit.Cli.Commands.DocsCheck;

public static class ConfigFieldExtractor
{
    private static readonly Dictionary<string, string> TopLevelMapping = new()
    {
        ["SiteConfig"] = "site",
        ["ContentConfig"] = "content",
        ["BuildConfig"] = "build",
        ["ThemeConfig"] = "theme",
        ["TaxonomyConfig"] = "taxonomy",
        ["LoggingConfig"] = "logging",
        ["DeployConfig"] = "deploy",
    };

    private static readonly HashSet<Type> PrimitiveTypes = new()
    {
        typeof(string),
        typeof(int),
        typeof(bool),
        typeof(double),
        typeof(long),
    };

    private static readonly Regex YamlRefPattern = new(
        @"\b[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+\b",
        RegexOptions.Compiled);

    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Config types are known and preserved.")]
    public static IReadOnlyList<string> ExtractAllConfigPaths()
    {
        var paths = new List<string>();
        var appConfigType = typeof(AppConfig);

        foreach (var prop in appConfigType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!TopLevelMapping.TryGetValue(prop.PropertyType.Name, out var topLevelKey))
                continue;

            WalkType(prop.PropertyType, topLevelKey, paths, new HashSet<Type>());
        }

        paths.Sort(StringComparer.Ordinal);
        return paths;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Config types are known and preserved.")]
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Config types are known and preserved.")]
    private static void WalkType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type, string prefix, List<string> paths, HashSet<Type> visited)
    {
        if (!visited.Add(type))
            return;

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var propType = prop.PropertyType;
            var snakeName = PascalToSnakeCase(prop.Name);
            var fullPath = $"{prefix}.{snakeName}";

            if (IsTerminalType(propType))
            {
                paths.Add(fullPath);
                continue;
            }

            if (IsRecordType(propType))
            {
                paths.Add(fullPath);
                WalkType(propType, fullPath, paths, visited);
            }
        }
    }

    private static bool IsTerminalType(Type type)
    {
        if (PrimitiveTypes.Contains(type))
            return true;

        if (type.IsGenericType)
        {
            var genericDef = type.GetGenericTypeDefinition();
            if (genericDef == typeof(Nullable<>))
                return true;
            if (genericDef == typeof(IReadOnlyList<>) || genericDef == typeof(IReadOnlyDictionary<,>))
                return true;
        }

        return false;
    }

    private static bool IsRecordType(Type type)
    {
        var name = type.Name;
        if (name.EndsWith("Config") || name.EndsWith("Record"))
            return true;

        return type.Namespace == "Bukit.Config";
    }

    private static string PascalToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        var result = Regex.Replace(name, "(?<=.)([A-Z])", "_$1");
        return result.ToLowerInvariant();
    }

    private static readonly HashSet<string> KnownTopLevelKeys = new()
    {
        "site", "content", "build", "theme", "taxonomy", "logging", "deploy"
    };

    private static readonly HashSet<string> FileExtensionSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        ".yaml", ".yml", ".json", ".md", ".git", ".tar.gz", ".lock.json",
        ".html", ".css", ".xml", ".txt", ".csv", ".js", ".ts",
    };

    public static IReadOnlyList<string> ExtractYamlReferences(string text)
    {
        var matches = YamlRefPattern.Matches(text);
        var refs = new HashSet<string>(matches.Count);
        foreach (Match match in matches)
        {
            var value = match.Value;
            var firstDot = value.IndexOf('.');
            if (firstDot < 0)
            {
                continue;
            }

            var prefix = value[..firstDot];
            if (!KnownTopLevelKeys.Contains(prefix))
            {
                continue;
            }

            if (HasFileExtensionSuffix(value))
            {
                continue;
            }

            refs.Add(value);
        }

        var list = new List<string>(refs);
        list.Sort(StringComparer.Ordinal);
        return list;
    }

    private static bool HasFileExtensionSuffix(string value)
    {
        foreach (var suffix in FileExtensionSuffixes)
        {
            if (value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static readonly HashSet<string> DynamicMapPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "site.menus",
        "site.plugins",
        "site.external_plugins",
        "site.collections",
        "site.permalinks",
        "theme.params",
        "theme.shortcodes",
        "theme.components",
        "content.model_schema.field_scopes",
    };

    private static readonly HashSet<string> KnownTemplateVariables = new(StringComparer.OrdinalIgnoreCase)
    {
        "site.data",
        "site.modules",
        "site.params",
        "site.data_files",
        "site.related_pages",
        "site.rss",
        "content.custom_field_enum_mismatch",
        "content.custom_field_format_mismatch",
        "content.custom_field_range_mismatch",
        "content.custom_field_type_mismatch",
        "content.unknown_raw_key",
        "content.required_custom_field_missing",
        "content.required_collection_field_missing",
    };

    public static bool IsKnownTemplateVariable(string path)
    {
        if (KnownTemplateVariables.Contains(path))
            return true;

        foreach (var prefix in KnownTemplateVariablePrefixes)
        {
            if (path.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static readonly HashSet<string> KnownTemplateVariablePrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "site.data",
        "site.modules",
        "site.params",
    };

    public static bool IsDynamicMapChild(string path)
    {
        foreach (var prefix in DynamicMapPrefixes)
        {
            if (path.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static IReadOnlyList<string> ExtractYamlReferencesFromDoc(string text)
    {
        var refs = new HashSet<string>();

        var yamlBlockRegex = new Regex(@"```ya?ml\s*\n(.*?)```", RegexOptions.Singleline);
        foreach (Match block in yamlBlockRegex.Matches(text))
        {
            var yamlContent = block.Groups[1].Value;
            foreach (var r in ExtractYamlReferences(yamlContent))
                refs.Add(r);
        }

        var tableRegex = new Regex(@"\|\s*`([a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+)`\s*\|");
        foreach (Match m in tableRegex.Matches(text))
        {
            var value = m.Groups[1].Value;
            var firstDot = value.IndexOf('.');
            if (firstDot < 0)
                continue;

            var prefix = value[..firstDot];
            if (!KnownTopLevelKeys.Contains(prefix))
                continue;

            if (HasFileExtensionSuffix(value))
                continue;

            refs.Add(value);
        }

        var list = new List<string>(refs);
        list.Sort(StringComparer.Ordinal);
        return list;
    }
}
