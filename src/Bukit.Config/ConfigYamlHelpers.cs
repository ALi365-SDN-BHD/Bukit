using System.Globalization;
using Bukit.Shared;
using YamlDotNet.RepresentationModel;

namespace Bukit.Config;

internal static class ConfigYamlHelpers
{
    internal static YamlMappingNode GetMapping(YamlMappingNode node, string key)
    {
        var result = GetOptionalMapping(node, key);
        if (result is null)
        {
            throw new ConfigException($"{key} section is required.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        return result;
    }

    internal static YamlMappingNode? GetOptionalMapping(YamlMappingNode node, string key)
    {
        if (!node.Children.TryGetValue(new YamlScalarNode(key), out var child))
        {
            return null;
        }

        return child as YamlMappingNode;
    }

    internal static string? GetOptionalString(YamlMappingNode node, string key)
    {
        if (!node.Children.TryGetValue(new YamlScalarNode(key), out var child))
        {
            return null;
        }

        if (child is not YamlScalarNode scalar)
        {
            return null;
        }

        return scalar.Value;
    }

    internal static string GetRequiredString(YamlMappingNode node, string key)
    {
        var value = GetOptionalString(node, key);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ConfigException($"{key} is required.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        return value;
    }

    internal static bool? GetOptionalBool(YamlMappingNode node, string key)
    {
        var value = GetOptionalString(node, key);
        if (value is null)
        {
            return null;
        }

        if (bool.TryParse(value, out var b))
        {
            return b;
        }

        if (value.Equals("yes", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (value.Equals("no", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return null;
    }

    internal static bool? GetOptionalBoolStrict(YamlMappingNode node, string key)
    {
        var value = GetOptionalString(node, key);
        if (value is null) return null;

        if (bool.TryParse(value, out var b)) return b;
        if (value.Equals("yes", StringComparison.OrdinalIgnoreCase)) return true;
        if (value.Equals("no", StringComparison.OrdinalIgnoreCase)) return false;

        throw new ConfigException($"Invalid config value: {key} expected boolean, got '{value}'", DiagnosticCode.ConfigInvalidValue);
    }

    internal static int? GetOptionalInt(YamlMappingNode node, string key)
    {
        var value = GetOptionalString(node, key);
        if (value is null)
        {
            return null;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
        {
            return i;
        }

        return null;
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

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
        {
            return l;
        }

        return null;
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

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
        {
            return d;
        }

        return null;
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
            YamlScalarNode s => s.Value ?? string.Empty,
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

    internal static YamlSequenceNode? GetOptionalSequence(YamlMappingNode node, string key)
    {
        if (!node.Children.TryGetValue(new YamlScalarNode(key), out var child))
        {
            return null;
        }

        return child as YamlSequenceNode;
    }
}
