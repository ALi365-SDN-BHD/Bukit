using YamlDotNet.RepresentationModel;

namespace Bukit.Theme;

public static class ThemeManifestYamlValidator
{
    /// <summary>
    /// 1.0 theme.yaml validation. Returns a non-empty list of issues when validation fails.
    /// Missing theme.yaml is a hard error in 1.0.
    /// </summary>
    public static List<string> Validate(string themeRoot)
    {
        var yamlPath = Path.Combine(themeRoot, "theme.yaml");
        var issues = new List<string>();

        if (!File.Exists(yamlPath))
        {
            issues.Add("BKT-0100: theme.yaml not found. Bukit Core 1.0 requires a theme.yaml manifest under the active theme root.");
            return issues;
        }

        try
        {
            var text = File.ReadAllText(yamlPath);
            var stream = new YamlStream();
            stream.Load(new StringReader(text));

            if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root)
            {
                issues.Add("BKT-0101: theme.yaml is empty or not a valid mapping.");
                return issues;
            }

            GetStringValue(root, "name", out var name);
            if (string.IsNullOrWhiteSpace(name))
                issues.Add("BKT-0100: theme.yaml: 'name' is missing or empty (required in 1.0).");

            GetStringValue(root, "version", out var version);
            if (string.IsNullOrWhiteSpace(version))
                issues.Add("BKT-0100: theme.yaml: 'version' is missing (required in 1.0).");
            else if (!System.Version.TryParse(version, out _))
                issues.Add($"BKT-0100: theme.yaml: 'version' '{version}' is not valid semver.");

            GetStringValue(root, "engine", out var engine);
            if (string.IsNullOrWhiteSpace(engine))
                issues.Add("BKT-0100: theme.yaml: 'engine' is missing (required in 1.0). Set to 'bukit'.");
            else if (!engine.Equals("bukit", StringComparison.OrdinalIgnoreCase))
                issues.Add($"BKT-0100: theme.yaml: 'engine' must be 'bukit', got '{engine}'.");


            ThemeManifestStrictValidator.Validate(root, themeRoot, issues);
        }
        catch (Exception ex)
        {
            issues.Add($"BKT-0101: theme.yaml parse error: {ex.Message}");
        }

        return issues;
    }

    private static void GetStringValue(YamlMappingNode node, string key, out string? value)
    {
        value = null;
        if (node.Children.TryGetValue(new YamlScalarNode(key), out var child) && child is YamlScalarNode scalar)
        {
            value = scalar.Value;
        }
    }
}
