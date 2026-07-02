using YamlDotNet.RepresentationModel;

namespace Bukit.Config;

internal static partial class ThemeManifestStrictValidator
{
    private static readonly HashSet<string> KnownRootFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "name",
        "display_name",
        "version",
        "engine",
        "min_engine_version",
        "description",
        "extends",
        "capabilities",
        "layouts",
        "templates",
        "page_templates",
        "sections",
        "components",
        "assets",
        "tokens"
    };

    private static readonly HashSet<string> KnownSectionDefinitionFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "template",
        "schema",
        "preview",
        "description",
        "variants",
        "data",
        "plugin"
    };

    private static readonly HashSet<string> KnownSectionVariantFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "template",
        "label",
        "description"
    };

    private static readonly HashSet<string> KnownDataBindingFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "source",
        "mode",
        "limit",
        "sort",
        "filters"
    };

    private static readonly HashSet<string> KnownTemplateDefinitionFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "template",
        "required",
        "label",
        "accepts",
        "required_fields"
    };

    private static readonly HashSet<string> KnownTemplateAcceptFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "type",
        "collection",
        "kind"
    };

    private static readonly HashSet<string> KnownPageTemplateDefinitionFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "template",
        "label",
        "accepts",
        "required_fields"
    };

    private static readonly HashSet<string> KnownPageTemplateAcceptFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "type",
        "collection"
    };

    private static readonly HashSet<string> KnownComponentDefinitionFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "template",
        "props"
    };

    private static readonly HashSet<string> KnownAssetFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "css",
        "js"
    };

    private static readonly HashSet<string> KnownCapabilitiesFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "i18n",
        "seo",
        "geo",
        "dark_mode",
        "search",
        "taxonomy"
    };

    internal static void Validate(YamlMappingNode root, string themeRoot, List<string> issues)
    {
        AddUnknownRootFields(root, issues);
        ValidateOptionalString(root, "display_name", "theme.yaml.display_name", issues);
        ValidateOptionalString(root, "min_engine_version", "theme.yaml.min_engine_version", issues);
        ValidateOptionalString(root, "description", "theme.yaml.description", issues);
        ValidateOptionalString(root, "tokens", "theme.yaml.tokens", issues);
        ValidateOptionalString(root, "extends", "theme.yaml.extends", issues);

        ValidateLayouts(root, themeRoot, issues);
        ValidateCapabilities(root, issues);
        ValidateTemplates(root, themeRoot, issues);
        ValidatePageTemplates(root, themeRoot, issues);
        ValidateSections(root, themeRoot, issues);
        ValidateComponents(root, themeRoot, issues);
        ValidateAssets(root, themeRoot, issues);
        ValidateExtends(root, themeRoot, issues);
    }

    private static void AddUnknownRootFields(YamlMappingNode root, List<string> issues)
    {
        foreach (var (keyNode, _) in root.Children)
        {
            if (keyNode is not YamlScalarNode key || !TryGetStringValue(key, out var keyValue))
            {
                issues.Add("BKT-0100: theme.yaml: unknown field ''.");
                continue;
            }

            if (!KnownRootFields.Contains(keyValue))
            {
                issues.Add($"BKT-0100: theme.yaml: unknown field 'theme.yaml.{keyValue}'.");
            }
        }
    }

}
