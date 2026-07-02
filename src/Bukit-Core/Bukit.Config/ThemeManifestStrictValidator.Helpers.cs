using System.Diagnostics.CodeAnalysis;
using YamlDotNet.RepresentationModel;

namespace Bukit.Config;

internal static partial class ThemeManifestStrictValidator
{
    private static void ValidateTemplatePath(
        string? templatePath,
        string themeRoot,
        string section,
        string field,
        bool enforceLayouts,
        List<string> issues)
    {
        var value = templatePath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add($"BKT-0100: {section}.{field} must be a non-empty path.");
            return;
        }

        var rootPath = Path.GetFullPath(enforceLayouts
            ? Path.Combine(themeRoot, "layouts")
            : themeRoot);
        var normalizedRoot = EnsureTrailingSeparator(rootPath);

        if (value.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            issues.Add($"BKT-0100: {section}.{field} contains invalid path characters.");
            return;
        }

        if (Path.IsPathRooted(value) || value.StartsWith("/", StringComparison.Ordinal))
        {
            issues.Add($"BKT-0100: {section}.{field} must be a relative path within theme.");
            return;
        }

        var hasTraversal = false;
        var segments = value.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            if (segment == "..")
            {
                hasTraversal = true;
                break;
            }

            if (segment.Contains(':', StringComparison.Ordinal))
            {
                hasTraversal = true;
                break;
            }
        }

        if (hasTraversal)
        {
            issues.Add($"BKT-0100: {section}.{field} has path traversal characters.");
            return;
        }

        var candidatePath = Path.GetFullPath(Path.Combine(rootPath, value.Replace('/', Path.DirectorySeparatorChar)));
        if (!candidatePath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add($"BKT-0100: {section}.{field} is outside theme scope: {value}.");
        }
    }

    private static void ValidateAssetPath(string? assetPath, string themeRoot, string fieldPath, List<string> issues)
        => ValidateTemplatePath(assetPath, themeRoot, fieldPath, "value", false, issues);

    private static void ValidateAssetSequence(YamlSequenceNode list, string themeRoot, string fieldPath, List<string> issues)
    {
        var index = 0;
        foreach (var item in list.Children)
        {
            if (item is YamlScalarNode scalar)
            {
                ValidateTemplatePath(scalar.Value, themeRoot, fieldPath, $"[{index}]", false, issues);
            }
            else
            {
                issues.Add($"BKT-0100: {fieldPath}[{index}] must be a string.");
            }

            index++;
        }
    }

    private static void ValidateBoolean(YamlMappingNode map, string key, string path, List<string> issues)
    {
        if (!HasField(map, key))
        {
            return;
        }

        var node = TryGetNode(map, key);
        if (node is not YamlScalarNode scalar || !bool.TryParse(scalar.Value, out _))
        {
            issues.Add($"BKT-0100: {path} must be a boolean.");
        }
    }

    private static void ValidateOptionalString(YamlMappingNode map, string key, string path, List<string> issues)
    {
        if (!HasField(map, key))
        {
            return;
        }

        if (TryGetNode(map, key) is not YamlScalarNode scalar)
        {
            issues.Add($"BKT-0100: {path} must be a string.");
            return;
        }

        if (scalar.Value is null || scalar.Value.Trim().Length == 0)
        {
            issues.Add($"BKT-0100: {path} must be a non-empty string when set.");
        }
    }

    private static void ValidateOptionalStringList(YamlMappingNode map, string key, string path, List<string> issues)
    {
        var value = TryGetNode(map, key);
        if (value is null)
        {
            return;
        }

        if (value is YamlSequenceNode sequence)
        {
            for (var i = 0; i < sequence.Children.Count; i++)
            {
                if (sequence.Children[i] is not YamlScalarNode scalar || string.IsNullOrWhiteSpace(scalar.Value))
                {
                    issues.Add($"BKT-0100: {path}[{i}] must be a string.");
                }
            }

            return;
        }

        if (value is YamlScalarNode scalarValue)
        {
            if (string.IsNullOrWhiteSpace(scalarValue.Value))
            {
                issues.Add($"BKT-0100: {path} must be a non-empty string.");
            }

            return;
        }

        issues.Add($"BKT-0100: {path} must be a string or list of strings.");
    }

    private static bool IsValidThemeName(string value, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            error = "theme name is null or whitespace.";
            return false;
        }

        if (Path.IsPathRooted(value))
        {
            error = "theme name must not be an absolute path.";
            return false;
        }

        if (value == ".." || value.Contains("..", StringComparison.Ordinal))
        {
            error = "theme name must not contain '..' segments.";
            return false;
        }

        if (value.Contains('/') || value.Contains('\\'))
        {
            error = "theme name must not contain path separators.";
            return false;
        }

        foreach (var ch in value)
        {
            if (ch < 32)
            {
                error = "theme name contains control characters.";
                return false;
            }
        }

        if (IsWindowsDeviceName(value))
        {
            error = $"theme name '{value}' is a reserved Windows device name.";
            return false;
        }

        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch) || ch is '_' || ch is '-' || ch is '.')
            {
                continue;
            }

            error = $"theme name '{value}' contains invalid character '{ch}'. Only [A-Za-z0-9_-.] are allowed.";
            return false;
        }

        return true;
    }

    private static bool IsWindowsDeviceName(string value)
    {
        var segment = value.Trim().ToLowerInvariant();
        if (segment is "con" or "prn" or "aux" or "nul")
        {
            return true;
        }

        if (segment.Length == 4 && segment.StartsWith("com", StringComparison.Ordinal) && char.IsDigit(segment[3]))
        {
            return true;
        }

        if (segment.Length == 4 && segment.StartsWith("lpt", StringComparison.Ordinal) && char.IsDigit(segment[3]))
        {
            return true;
        }

        return false;
    }

    private static bool TryGetMap(YamlMappingNode root, string key, [NotNullWhen(true)] out YamlMappingNode? map)
    {
        map = null;
        if (!TryGetNode(root, key, out var node))
        {
            return false;
        }

        map = node as YamlMappingNode;
        return map is not null;
    }

    private static bool TryGetString(YamlMappingNode map, string key, out string? value)
    {
        value = null;
        if (!TryGetNode(map, key, out var node) || node is not YamlScalarNode scalar)
        {
            return false;
        }

        value = scalar.Value;
        return true;
    }

    private static bool TryGetNode(YamlMappingNode root, string key, [NotNullWhen(true)] out YamlNode? node)
    {
        foreach (var (nodeKey, nodeValue) in root.Children)
        {
            if (nodeKey is YamlScalarNode scalar && string.Equals(scalar.Value, key, StringComparison.OrdinalIgnoreCase))
            {
                node = nodeValue;
                return true;
            }
        }

        node = null;
        return false;
    }

    private static bool HasField(YamlMappingNode map, string key)
        => TryGetNode(map, key, out _);

    private static YamlNode? TryGetNode(YamlMappingNode root, string key)
        => TryGetNode(root, key, out var node)
            ? node
            : null;

    private static IEnumerable<(string Key, YamlNode Value)> EnumerateMap(YamlMappingNode map)
    {
        foreach (var (keyNode, value) in map.Children)
        {
            if (keyNode is YamlScalarNode key && key.Value is string keyValue && !string.IsNullOrWhiteSpace(keyValue))
            {
                yield return (keyValue, value);
            }
        }
    }

    private static void AddUnknownFields(YamlMappingNode map, HashSet<string> allowed, string path, List<string> issues)
    {
        foreach (var (key, _) in map.Children)
        {
            if (key is not YamlScalarNode scalar || scalar.Value is not string keyValue)
            {
                issues.Add($"BKT-0100: {path} contains an unknown non-scalar field key.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(keyValue))
            {
                issues.Add($"BKT-0100: {path}: unknown field ''.");
                continue;
            }

            if (!allowed.Contains(keyValue))
            {
                issues.Add($"BKT-0100: unknown field '{path}.{keyValue}'.");
            }
        }
    }

    private static bool TryGetStringValue(YamlScalarNode node, [NotNullWhen(true)] out string value)
    {
        if (node.Value is string nodeValue)
        {
            value = nodeValue;
            return !string.IsNullOrWhiteSpace(value);
        }

        value = string.Empty;
        return false;
    }

    private static string EnsureTrailingSeparator(string value)
    {
        var normalized = value.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return normalized + Path.DirectorySeparatorChar;
    }
}
