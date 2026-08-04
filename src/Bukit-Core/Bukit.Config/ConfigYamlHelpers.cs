using System.Globalization;
using Bukit.Shared;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Bukit.Config;

internal static class ConfigYamlHelpers
{
    internal static YamlMappingNode GetMapping(YamlMappingNode node, string key, string? parentPath = null)
    {
        var result = GetOptionalMapping(node, key, parentPath);
        if (result is null)
        {
            throw new ConfigException($"{key} section is required.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        return result;
    }

    internal static YamlMappingNode? GetOptionalMapping(YamlMappingNode node, string key, string? parentPath = null)
    {
        if (!node.Children.TryGetValue(new YamlScalarNode(key), out var child))
        {
            return null;
        }

        if (child is YamlMappingNode mapping)
        {
            return mapping;
        }

        if (IsEmptyScalar(child))
        {
            return null;
        }

        throw KindMismatch(ComposePath(parentPath, key), "mapping", child);
    }

    internal static string? GetOptionalString(YamlMappingNode node, string key, string? parentPath = null)
    {
        if (!node.Children.TryGetValue(new YamlScalarNode(key), out var child))
        {
            return null;
        }

        if (child is YamlScalarNode scalar)
        {
            return scalar.Value;
        }

        throw KindMismatch(ComposePath(parentPath, key), "scalar", child);
    }

    internal static string GetRequiredString(YamlMappingNode node, string key, string? parentPath = null)
    {
        var value = GetOptionalString(node, key, parentPath);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ConfigException($"{key} is required.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        return value;
    }

    internal static bool? GetOptionalBool(YamlMappingNode node, string key, string? parentPath = null)
    {
        var value = GetOptionalString(node, key, parentPath);
        if (value is null)
        {
            return null;
        }

        if (bool.TryParse(value, out var b)) return b;

        throw new ConfigException($"Invalid config value: {ComposePath(parentPath, key)} expected boolean true|false, got '{value}'", DiagnosticCode.ConfigInvalidValue);
    }

    internal static bool? GetOptionalBoolStrict(YamlMappingNode node, string key, string? parentPath = null)
    {
        var value = GetOptionalString(node, key, parentPath);
        if (value is null) return null;

        if (bool.TryParse(value, out var b)) return b;

        throw new ConfigException($"Invalid config value: {ComposePath(parentPath, key)} expected boolean true|false, got '{value}'", DiagnosticCode.ConfigInvalidValue);
    }

    internal static int? GetOptionalInt(YamlMappingNode node, string key)
    {
        var value = GetOptionalString(node, key);
        if (value is null)
        {
            return null;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) return i;

        throw new ConfigException($"Invalid config value: {key} expected integer, got '{value}'", DiagnosticCode.ConfigInvalidValue);
    }

    internal static int? GetOptionalIntStrict(YamlMappingNode node, string key)
    {
        var value = GetOptionalString(node, key);
        if (value is null) return null;

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
            return i;

        throw new ConfigException($"Invalid config value: {key} expected integer, got '{value}'", DiagnosticCode.ConfigInvalidValue);
    }

    internal static long? GetOptionalLong(YamlMappingNode node, string key)
    {
        var value = GetOptionalString(node, key);
        if (value is null)
        {
            return null;
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) return l;

        throw new ConfigException($"Invalid config value: {key} expected long integer, got '{value}'", DiagnosticCode.ConfigInvalidValue);
    }

    internal static long? GetOptionalLongStrict(YamlMappingNode node, string key)
    {
        var value = GetOptionalString(node, key);
        if (value is null) return null;

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
            return l;

        throw new ConfigException($"Invalid config value: {key} expected long integer, got '{value}'", DiagnosticCode.ConfigInvalidValue);
    }

    internal static double? GetOptionalDouble(YamlMappingNode node, string key)
    {
        var value = GetOptionalString(node, key);
        if (value is null)
        {
            return null;
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;

        throw new ConfigException($"Invalid config value: {key} expected double, got '{value}'", DiagnosticCode.ConfigInvalidValue);
    }

    internal static double? GetOptionalDoubleStrict(YamlMappingNode node, string key)
    {
        var value = GetOptionalString(node, key);
        if (value is null) return null;

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            return d;

        throw new ConfigException($"Invalid config value: {key} expected double, got '{value}'", DiagnosticCode.ConfigInvalidValue);
    }

    internal static IReadOnlyDictionary<string, string>? ReadStringMap(YamlMappingNode? parent, string key)
    {
        if (parent is null)
        {
            return null;
        }

        var map = GetOptionalMapping(parent, key);
        if (map is null)
        {
            return null;
        }

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in map.Children)
        {
            if (kv.Key is not YamlScalarNode k || string.IsNullOrWhiteSpace(k.Value))
            {
                continue;
            }

            if (kv.Value is not YamlScalarNode v || string.IsNullOrWhiteSpace(v.Value))
            {
                continue;
            }

            dict[k.Value.Trim()] = v.Value.Trim();
        }

        return dict.Count == 0 ? null : dict;
    }

    internal static IReadOnlyList<string>? ReadStringList(YamlMappingNode node, string key)
    {
        var seq = GetOptionalSequence(node, key);
        if (seq is null)
        {
            return null;
        }

        var list = new List<string>();
        foreach (var n in seq.Children)
        {
            if (n is YamlScalarNode s && !string.IsNullOrWhiteSpace(s.Value))
            {
                list.Add(s.Value.Trim());
            }
        }

        return list.Count == 0 ? null : list;
    }

    internal static IReadOnlyDictionary<string, object>? ReadObjectMap(YamlMappingNode mapNode)
    {
        var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in mapNode.Children)
        {
            if (kv.Key is not YamlScalarNode keyNode || string.IsNullOrWhiteSpace(keyNode.Value))
            {
                continue;
            }

            dict[keyNode.Value.Trim()] = ToObject(kv.Value);
        }

        return dict.Count == 0 ? null : dict;
    }

    internal static object ToObject(YamlNode node)
    {
        return node switch
        {
            YamlScalarNode s => ToScalarObject(s),
            YamlSequenceNode seq => seq.Children.Select(ToObject).ToList(),
            YamlMappingNode map => map.Children
                .Where(p => p.Key is YamlScalarNode ks && !string.IsNullOrWhiteSpace(ks.Value))
                .ToDictionary(
                    p => ((YamlScalarNode)p.Key).Value!,
                    p => ToObject(p.Value),
                    StringComparer.OrdinalIgnoreCase),
            _ => node.ToString()
        };
    }

    private static object ToScalarObject(YamlScalarNode scalar)
    {
        var value = scalar.Value ?? string.Empty;
        var tag = scalar.Tag.ToString();

        if (scalar.Style == ScalarStyle.Plain
            && tag is not "tag:yaml.org,2002:str" and not "!!str"
            && bool.TryParse(value, out var boolean))
        {
            return boolean;
        }

        return value;
    }

    internal static YamlSequenceNode? GetOptionalSequence(YamlMappingNode node, string key, string? parentPath = null)
    {
        if (!node.Children.TryGetValue(new YamlScalarNode(key), out var child))
        {
            return null;
        }

        if (child is YamlSequenceNode sequence)
        {
            return sequence;
        }

        if (IsEmptyScalar(child))
        {
            return null;
        }

        throw KindMismatch(ComposePath(parentPath, key), "sequence", child);
    }

    private static bool IsEmptyScalar(YamlNode node)
        => node is YamlScalarNode scalar && string.IsNullOrEmpty(scalar.Value);

    private static string ComposePath(string? parentPath, string key)
        => string.IsNullOrEmpty(parentPath) ? key : $"{parentPath}.{key}";

    private static ConfigException KindMismatch(string path, string expectedKind, YamlNode actual)
    {
        var actualKind = actual switch
        {
            YamlMappingNode => "mapping",
            YamlSequenceNode => "sequence",
            YamlScalarNode => "scalar",
            _ => actual.NodeType.ToString()
        };
        return new ConfigException(
            $"Invalid config value: {path} expected {expectedKind} node kind, got {actualKind} node kind.",
            DiagnosticCode.ConfigInvalidValue);
    }
}
