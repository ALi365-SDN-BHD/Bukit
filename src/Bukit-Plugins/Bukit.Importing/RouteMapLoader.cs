using YamlDotNet.RepresentationModel;

namespace Bukit.Importing;

internal static class RouteMapLoader
{
    internal static RouteMapConfig? Load(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        YamlStream yaml;
        try
        {
            yaml = new YamlStream();
            using var reader = File.OpenText(path);
            yaml.Load(reader);
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            throw Invalid(path, $"Failed to parse route map '{path}': {ex.Message}");
        }

        if (yaml.Documents.Count == 0 || yaml.Documents[0].RootNode == null)
            throw Invalid(path, $"Route map '{path}' is empty.");

        var root = yaml.Documents[0].RootNode;
        YamlSequenceNode pagesSeq;

        if (root is YamlMappingNode mapping)
        {
            if (!mapping.Children.TryGetValue("pages", out var pagesNode) ||
                pagesNode is not YamlSequenceNode seq)
            {
                throw Invalid(path, $"Route map '{path}' is missing the 'pages' sequence.");
            }
            pagesSeq = seq;
        }
        else if (root is YamlSequenceNode directSeq)
        {
            pagesSeq = directSeq;
        }
        else
        {
            throw Invalid(path, $"Route map '{path}' has unsupported structure.");
        }

        var config = new RouteMapConfig();
        foreach (var node in pagesSeq.Children)
        {
            if (node is not YamlMappingNode item)
                continue;

            var page = new RouteMapPage
            {
                Source = ReadString(item, "source"),
                Route = ReadString(item, "route"),
                Type = ReadString(item, "type"),
                Template = ReadString(item, "template"),
                Slug = ReadOptionalString(item, "slug"),
                Description = ReadOptionalString(item, "description")
            };

            if (string.IsNullOrWhiteSpace(page.Source))
                continue;

            if (!IsSafeTemplateName(page.Template))
            {
                throw Invalid(path,
                    $"Route map entry '{page.Source}' has an invalid 'template' value '{page.Template}'. " +
                    "Use a simple name (letters, digits, '-' or '_'), not a path.");
            }

            if (!IsSafeSlug(page.Slug))
            {
                throw Invalid(path,
                    $"Route map entry '{page.Source}' has an invalid 'slug' value '{page.Slug}'. " +
                    "A slug must be a single path segment.");
            }

            config.Pages.Add(page);
        }

        return config;
    }

    private static ImportException Invalid(string path, string message)
        => new(ImportErrorKind.UserInput, message);

    private static bool IsSafeTemplateName(string template)
        => string.IsNullOrWhiteSpace(template) ||
           template.All(c => char.IsLetterOrDigit(c) || c is '-' or '_');

    private static bool IsSafeSlug(string? slug)
        => string.IsNullOrWhiteSpace(slug) ||
           (!Path.IsPathRooted(slug) &&
            slug.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '/', '\\']) < 0 &&
            slug is not "." and not ".." &&
            string.Equals(slug, slug.Trim(), StringComparison.Ordinal));

    private static string ReadString(YamlMappingNode node, string key)
    {
        if (!node.Children.TryGetValue(key, out var valueNode))
            return "";
        return valueNode is YamlScalarNode scalar ? scalar.Value ?? "" : "";
    }

    private static string? ReadOptionalString(YamlMappingNode node, string key)
    {
        if (!node.Children.TryGetValue(key, out var valueNode))
            return null;
        if (valueNode is not YamlScalarNode scalar)
            return null;
        var val = scalar.Value;
        return string.IsNullOrWhiteSpace(val) ? null : val;
    }
}
