using YamlDotNet.RepresentationModel;

namespace Bukit.Importing;

internal static class ImportSiteUseService
{
    internal static ImportDiagnostic Apply(HtmlDemoImportOptions options)
    {
        string siteYamlPath = Path.Combine(HtmlDemoImporter.GetSiteDir(options), "site.yaml");
        if (!File.Exists(siteYamlPath))
        {
            throw new ImportException(
                ImportErrorKind.UserInput,
                $"无法应用 --use，site.yaml 不存在: {siteYamlPath}");
        }

        var stream = new YamlStream();
        using (var reader = File.OpenText(siteYamlPath))
        {
            stream.Load(reader);
        }

        if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root)
        {
            throw new ImportException(
                ImportErrorKind.UserInput,
                $"无法应用 --use，site.yaml 不是 YAML mapping: {siteYamlPath}");
        }

        var theme = GetOrCreateMapping(root, "theme");
        SetScalar(theme, "name", options.ThemeName);

        string tempPath = siteYamlPath + ".tmp";
        using (var writer = File.CreateText(tempPath))
        {
            stream.Save(writer, assignAnchors: false);
        }

        File.Move(tempPath, siteYamlPath, overwrite: true);
        return new ImportDiagnostic(
            ImportDiagnosticSeverity.Info,
            "import.useApplied",
            $"site.yaml theme.name 已指向 {options.ThemeName}",
            siteYamlPath);
    }

    private static YamlMappingNode GetOrCreateMapping(YamlMappingNode root, string key)
    {
        foreach (var child in root.Children)
        {
            if (child.Key is YamlScalarNode scalar &&
                string.Equals(scalar.Value, key, StringComparison.Ordinal))
            {
                if (child.Value is YamlMappingNode mapping)
                    return mapping;

                root.Children.Remove(child.Key);
                break;
            }
        }

        var created = new YamlMappingNode();
        root.Add(key, created);
        return created;
    }

    private static void SetScalar(YamlMappingNode mapping, string key, string value)
    {
        YamlNode? existingKey = null;
        foreach (var child in mapping.Children)
        {
            if (child.Key is YamlScalarNode scalar &&
                string.Equals(scalar.Value, key, StringComparison.Ordinal))
            {
                existingKey = child.Key;
                break;
            }
        }

        if (existingKey is null)
        {
            mapping.Add(key, value);
        }
        else
        {
            mapping.Children[existingKey] = new YamlScalarNode(value);
        }
    }
}
