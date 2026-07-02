using System.Collections;
using YamlDotNet.RepresentationModel;

namespace Bukit.Config;

internal static class ConfigEnvironmentOverrides
{
    private const string Prefix = "BUKIT_";

    public static void Apply(YamlMappingNode root)
    {
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            var key = entry.Key?.ToString();
            var value = entry.Value?.ToString();
            if (string.IsNullOrWhiteSpace(key) ||
                !key.StartsWith(Prefix, StringComparison.Ordinal) ||
                !key.Contains("__", StringComparison.Ordinal) ||
                value is null)
            {
                continue;
            }

            var path = key[Prefix.Length..]
                .Split("__", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(ToConfigKey)
                .Where(segment => !string.IsNullOrWhiteSpace(segment))
                .ToArray();
            if (path.Length == 0)
            {
                continue;
            }

            SetScalar(root, path, value);
        }
    }

    private static void SetScalar(YamlMappingNode root, IReadOnlyList<string> path, string value)
    {
        var current = root;
        for (var i = 0; i < path.Count - 1; i++)
        {
            var key = new YamlScalarNode(path[i]);
            if (!current.Children.TryGetValue(key, out var child) || child is not YamlMappingNode next)
            {
                next = new YamlMappingNode();
                current.Children[key] = next;
            }

            current = next;
        }

        current.Children[new YamlScalarNode(path[^1])] = new YamlScalarNode(value);
    }

    private static string ToConfigKey(string envSegment)
    {
        var lower = envSegment.Trim().ToLowerInvariant();
        if (lower.Length == 0)
        {
            return string.Empty;
        }

        var parts = lower.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return lower;
        }

        return parts[0] + string.Concat(parts.Skip(1).Select(Capitalize));
    }

    private static string Capitalize(string value)
        => value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];
}
