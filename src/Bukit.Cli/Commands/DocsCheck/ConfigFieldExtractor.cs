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

            refs.Add(value);
        }

        var list = new List<string>(refs);
        list.Sort(StringComparer.Ordinal);
        return list;
    }
}
