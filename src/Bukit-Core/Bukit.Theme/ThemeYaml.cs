using YamlDotNet.RepresentationModel;

namespace Bukit.Theme;

internal static class ThemeYaml
{
    public static YamlNode? GetNode(YamlMappingNode? map, string key)
    {
        if (map is null)
        {
            return null;
        }

        foreach (var (nodeKey, value) in map.Children)
        {
            if (nodeKey is YamlScalarNode scalar &&
                string.Equals(scalar.Value, key, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }

    public static IEnumerable<(string Key, YamlNode Value)> EnumerateMap(YamlMappingNode map)
    {
        foreach (var (key, value) in map.Children)
        {
            if (key is YamlScalarNode scalar && !string.IsNullOrWhiteSpace(scalar.Value))
            {
                yield return (scalar.Value, value);
            }
        }
    }
}
