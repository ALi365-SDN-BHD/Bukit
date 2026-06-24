using System.Globalization;
using Bukit.Shared;
using YamlDotNet.RepresentationModel;

namespace Bukit.PluginHost;

internal static class PluginYaml
{
    internal static YamlMappingNode GetRequiredMapping(YamlMappingNode node, string key, string path)
    {
        YamlMappingNode? mapping = GetOptionalMapping(node, key);
        if (mapping is null)
        {
            throw new ConfigException($"{path} is required.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        return mapping;
    }

    internal static YamlMappingNode? GetOptionalMapping(YamlMappingNode? node, string key)
    {
        if (node is null || !node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? child))
        {
            return null;
        }

        if (child is not YamlMappingNode mapping)
        {
            throw new ConfigException($"{key} must be a mapping.", DiagnosticCode.ConfigInvalidValue);
        }

        return mapping;
    }

    internal static YamlSequenceNode? GetOptionalSequence(YamlMappingNode? node, string key)
    {
        if (node is null || !node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? child))
        {
            return null;
        }

        if (child is not YamlSequenceNode sequence)
        {
            throw new ConfigException($"{key} must be a sequence.", DiagnosticCode.ConfigInvalidValue);
        }

        return sequence;
    }

    internal static string GetRequiredString(YamlMappingNode node, string key, string path)
    {
        string? value = GetOptionalString(node, key);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ConfigException($"{path} is required.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        return value;
    }

    internal static string? GetOptionalString(YamlMappingNode? node, string key)
    {
        if (node is null || !node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? child))
        {
            return null;
        }

        if (child is not YamlScalarNode scalar)
        {
            throw new ConfigException($"{key} must be a scalar.", DiagnosticCode.ConfigInvalidValue);
        }

        return scalar.Value;
    }

    internal static bool? GetOptionalBool(YamlMappingNode? node, string key)
    {
        string? value = GetOptionalString(node, key);
        if (value is null)
        {
            return null;
        }

        if (bool.TryParse(value, out bool parsed))
        {
            return parsed;
        }

        throw new ConfigException($"{key} must be true or false.", DiagnosticCode.ConfigInvalidValue);
    }

    internal static int? GetOptionalInt(YamlMappingNode? node, string key)
    {
        string? value = GetOptionalString(node, key);
        if (value is null)
        {
            return null;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        {
            return parsed;
        }

        throw new ConfigException($"{key} must be an integer.", DiagnosticCode.ConfigInvalidValue);
    }

    internal static IReadOnlyList<string> ReadStringList(YamlMappingNode? node, string key)
    {
        YamlSequenceNode? sequence = GetOptionalSequence(node, key);
        if (sequence is null)
        {
            return [];
        }

        var values = new List<string>();
        foreach (YamlNode item in sequence.Children)
        {
            if (item is not YamlScalarNode scalar || string.IsNullOrWhiteSpace(scalar.Value))
            {
                throw new ConfigException($"{key} items must be non-empty scalars.", DiagnosticCode.ConfigInvalidValue);
            }

            values.Add(scalar.Value.Trim());
        }

        return values;
    }

    internal static string RequireKey(YamlNode keyNode, string path)
    {
        if (keyNode is not YamlScalarNode scalar || string.IsNullOrWhiteSpace(scalar.Value))
        {
            throw new ConfigException($"{path} keys must be non-empty scalars.", DiagnosticCode.ConfigInvalidValue);
        }

        return scalar.Value.Trim();
    }
}
