using YamlDotNet.RepresentationModel;

namespace Bukit.Config;

internal static partial class ThemeManifestStrictValidator
{
    private static void ValidateCapabilities(YamlMappingNode root, List<string> issues)
    {
        if (!HasField(root, "capabilities"))
        {
            return;
        }

        if (!TryGetMap(root, "capabilities", out var capabilities))
        {
            issues.Add("BKT-0100: theme.yaml.capabilities must be a mapping.");
            return;
        }

        AddUnknownFields(capabilities, KnownCapabilitiesFields, "theme.yaml.capabilities", issues);
        ValidateBoolean(capabilities, "i18n", "theme.yaml.capabilities.i18n", issues);
        ValidateBoolean(capabilities, "seo", "theme.yaml.capabilities.seo", issues);
        ValidateBoolean(capabilities, "geo", "theme.yaml.capabilities.geo", issues);
        ValidateBoolean(capabilities, "dark_mode", "theme.yaml.capabilities.dark_mode", issues);
        ValidateBoolean(capabilities, "search", "theme.yaml.capabilities.search", issues);
        ValidateBoolean(capabilities, "taxonomy", "theme.yaml.capabilities.taxonomy", issues);
    }

    private static void ValidateLayouts(YamlMappingNode root, string themeRoot, List<string> issues)
    {
        if (!HasField(root, "layouts"))
        {
            return;
        }

        if (!TryGetMap(root, "layouts", out var layouts))
        {
            issues.Add("BKT-0100: theme.yaml.layouts must be a mapping.");
            return;
        }

        foreach (var (layoutName, value) in EnumerateMap(layouts))
        {
            if (value is not YamlScalarNode scalar)
            {
                issues.Add($"BKT-0100: theme.yaml.layouts[{layoutName}] must be a string path.");
                continue;
            }

            ValidateTemplatePath(scalar.Value, themeRoot, "theme.yaml.layouts", "layout", true, issues);
        }
    }

    private static void ValidateTemplates(YamlMappingNode root, string themeRoot, List<string> issues)
    {
        if (!HasField(root, "templates"))
        {
            return;
        }

        if (!TryGetMap(root, "templates", out var templates))
        {
            issues.Add("BKT-0100: theme.yaml.templates must be a mapping.");
            return;
        }

        foreach (var (templateName, value) in EnumerateMap(templates))
        {
            if (value is not YamlMappingNode template)
            {
                issues.Add($"BKT-0100: theme.yaml.templates.{templateName} must be a mapping.");
                continue;
            }

            var templatePath = $"theme.yaml.templates.{templateName}";
            AddUnknownFields(template, KnownTemplateDefinitionFields, templatePath, issues);

            if (!TryGetString(template, "template", out var templatePathValue))
            {
                issues.Add($"BKT-0100: {templatePath}.template is required.");
            }
            else
            {
                ValidateTemplatePath(templatePathValue, themeRoot, templatePath, "template", true, issues);
            }

            ValidateBoolean(template, "required", $"{templatePath}.required", issues);
            ValidateOptionalString(template, "label", $"{templatePath}.label", issues);
            ValidateOptionalStringList(template, "required_fields", $"{templatePath}.required_fields", issues);

            if (HasField(template, "accepts"))
            {
                if (!TryGetMap(template, "accepts", out var accepts))
                {
                    issues.Add($"BKT-0100: {templatePath}.accepts must be a mapping.");
                }
                else
                {
                    AddUnknownFields(accepts, KnownTemplateAcceptFields, $"{templatePath}.accepts", issues);
                    ValidateOptionalString(accepts, "type", $"{templatePath}.accepts.type", issues);
                    ValidateOptionalString(accepts, "collection", $"{templatePath}.accepts.collection", issues);
                    ValidateOptionalString(accepts, "kind", $"{templatePath}.accepts.kind", issues);
                }
            }
        }
    }

    private static void ValidatePageTemplates(YamlMappingNode root, string themeRoot, List<string> issues)
    {
        if (!HasField(root, "page_templates"))
        {
            return;
        }

        if (!TryGetMap(root, "page_templates", out var pageTemplates))
        {
            issues.Add("BKT-0100: theme.yaml.page_templates must be a mapping.");
            return;
        }

        foreach (var (templateName, value) in EnumerateMap(pageTemplates))
        {
            if (value is not YamlMappingNode pageTemplate)
            {
                issues.Add($"BKT-0100: theme.yaml.page_templates.{templateName} must be a mapping.");
                continue;
            }

            var templatePath = $"theme.yaml.page_templates.{templateName}";
            AddUnknownFields(pageTemplate, KnownPageTemplateDefinitionFields, templatePath, issues);

            if (!TryGetString(pageTemplate, "template", out var templatePathValue))
            {
                issues.Add($"BKT-0100: {templatePath}.template is required.");
            }
            else
            {
                ValidateTemplatePath(templatePathValue, themeRoot, templatePath, "template", true, issues);
            }

            ValidateOptionalString(pageTemplate, "label", $"{templatePath}.label", issues);
            ValidateOptionalStringList(pageTemplate, "required_fields", $"{templatePath}.required_fields", issues);

            if (HasField(pageTemplate, "accepts"))
            {
                if (!TryGetMap(pageTemplate, "accepts", out var accepts))
                {
                    issues.Add($"BKT-0100: {templatePath}.accepts must be a mapping.");
                }
                else
                {
                    AddUnknownFields(accepts, KnownPageTemplateAcceptFields, $"{templatePath}.accepts", issues);
                    ValidateOptionalString(accepts, "type", $"{templatePath}.accepts.type", issues);
                    ValidateOptionalString(accepts, "collection", $"{templatePath}.accepts.collection", issues);
                }
            }
        }
    }

    private static void ValidateSections(YamlMappingNode root, string themeRoot, List<string> issues)
    {
        if (!HasField(root, "sections"))
        {
            return;
        }

        if (!TryGetMap(root, "sections", out var sections))
        {
            issues.Add("BKT-0100: theme.yaml.sections must be a mapping.");
            return;
        }

        foreach (var (sectionName, value) in EnumerateMap(sections))
        {
            if (value is not YamlMappingNode section)
            {
                issues.Add($"BKT-0100: theme.yaml.sections.{sectionName} must be a mapping.");
                continue;
            }

            var sectionPath = $"theme.yaml.sections.{sectionName}";
            AddUnknownFields(section, KnownSectionDefinitionFields, sectionPath, issues);

            if (!TryGetString(section, "template", out var templatePathValue))
            {
                issues.Add($"BKT-0100: {sectionPath}.template is required.");
            }
            else
            {
                ValidateTemplatePath(templatePathValue, themeRoot, sectionPath, "template", true, issues);
            }

            ValidateOptionalString(section, "schema", $"{sectionPath}.schema", issues);
            ValidateOptionalString(section, "preview", $"{sectionPath}.preview", issues);
            ValidateOptionalString(section, "description", $"{sectionPath}.description", issues);
            ValidateOptionalString(section, "plugin", $"{sectionPath}.plugin", issues);

            if (HasField(section, "data"))
            {
                if (!TryGetMap(section, "data", out var data))
                {
                    issues.Add($"BKT-0100: {sectionPath}.data must be a mapping.");
                }
                else
                {
                    AddUnknownFields(data, KnownDataBindingFields, $"{sectionPath}.data", issues);
                    ValidateOptionalString(data, "source", $"{sectionPath}.data.source", issues);
                    ValidateOptionalString(data, "mode", $"{sectionPath}.data.mode", issues);
                    ValidateOptionalString(data, "sort", $"{sectionPath}.data.sort", issues);

                    var limitNode = TryGetNode(data, "limit");
                    if (limitNode is not null && (limitNode is not YamlScalarNode limit || !int.TryParse(limit.Value, out _)))
                    {
                        issues.Add($"BKT-0100: {sectionPath}.data.limit must be an integer when set.");
                    }

                    var filtersNode = TryGetNode(data, "filters");
                    if (filtersNode is YamlMappingNode or YamlSequenceNode)
                    {
                        // allowed
                    }
                    else if (filtersNode is not null)
                    {
                        issues.Add($"BKT-0100: {sectionPath}.data.filters must be an object or array.");
                    }
                }
            }

            if (HasField(section, "variants"))
            {
                if (!TryGetMap(section, "variants", out var variants))
                {
                    issues.Add($"BKT-0100: {sectionPath}.variants must be a mapping.");
                }
                else
                {
                    foreach (var (variantName, variantValue) in EnumerateMap(variants))
                    {
                        if (variantValue is not YamlMappingNode variant)
                        {
                            issues.Add($"BKT-0100: {sectionPath}.variants.{variantName} must be a mapping.");
                            continue;
                        }

                        var variantPath = $"{sectionPath}.variants.{variantName}";
                        AddUnknownFields(variant, KnownSectionVariantFields, variantPath, issues);

                        if (!TryGetString(variant, "template", out var variantTemplate))
                        {
                            issues.Add($"BKT-0100: {variantPath}.template is required.");
                        }
                        else
                        {
                            ValidateTemplatePath(variantTemplate, themeRoot, variantPath, "template", true, issues);
                        }

                        ValidateOptionalString(variant, "label", $"{variantPath}.label", issues);
                        ValidateOptionalString(variant, "description", $"{variantPath}.description", issues);
                    }
                }
            }
        }
    }

    private static void ValidateComponents(YamlMappingNode root, string themeRoot, List<string> issues)
    {
        if (!HasField(root, "components"))
        {
            return;
        }

        if (!TryGetMap(root, "components", out var components))
        {
            issues.Add("BKT-0100: theme.yaml.components must be a mapping.");
            return;
        }

        foreach (var (componentName, value) in EnumerateMap(components))
        {
            if (value is not YamlMappingNode component)
            {
                issues.Add($"BKT-0100: theme.yaml.components.{componentName} must be a mapping.");
                continue;
            }

            var componentPath = $"theme.yaml.components.{componentName}";
            AddUnknownFields(component, KnownComponentDefinitionFields, componentPath, issues);

            if (!TryGetString(component, "template", out var templatePathValue))
            {
                issues.Add($"BKT-0100: {componentPath}.template is required.");
            }
            else
            {
                ValidateTemplatePath(templatePathValue, themeRoot, componentPath, "template", true, issues);
            }

            if (HasField(component, "props") && !TryGetMap(component, "props", out _))
            {
                issues.Add($"BKT-0100: {componentPath}.props must be a mapping.");
            }
        }
    }

    private static void ValidateAssets(YamlMappingNode root, string themeRoot, List<string> issues)
    {
        if (!HasField(root, "assets"))
        {
            return;
        }

        if (!TryGetMap(root, "assets", out var assets))
        {
            issues.Add("BKT-0100: theme.yaml.assets must be a mapping.");
            return;
        }

        AddUnknownFields(assets, KnownAssetFields, "theme.yaml.assets", issues);

        if (TryGetNode(assets, "css") is YamlScalarNode cssScalar)
        {
            ValidateAssetPath(cssScalar.Value, themeRoot, "theme.yaml.assets.css", issues);
        }
        else if (TryGetNode(assets, "css") is YamlSequenceNode cssSeq)
        {
            ValidateAssetSequence(cssSeq, themeRoot, "theme.yaml.assets.css", issues);
        }
        else if (HasField(assets, "css"))
        {
            issues.Add("BKT-0100: theme.yaml.assets.css must be a string or array.");
        }

        var jsNode = TryGetNode(assets, "js");
        if (jsNode is YamlScalarNode jsScalar)
        {
            ValidateAssetPath(jsScalar.Value, themeRoot, "theme.yaml.assets.js", issues);
        }
        else if (jsNode is YamlSequenceNode jsSeq)
        {
            ValidateAssetSequence(jsSeq, themeRoot, "theme.yaml.assets.js", issues);
        }
        else if (HasField(assets, "js"))
        {
            issues.Add("BKT-0100: theme.yaml.assets.js must be a string or array.");
        }
    }

    private static void ValidateExtends(YamlMappingNode root, string themeRoot, List<string> issues)
    {
        if (!TryGetString(root, "extends", out var extendsName) || string.IsNullOrWhiteSpace(extendsName))
        {
            return;
        }

        var trimmedExtends = extendsName.Trim();
        if (!IsValidThemeName(trimmedExtends, out var sanitizeError))
        {
            issues.Add($"BKT-0100: theme.yaml.extends '{extendsName}' is invalid: {sanitizeError}");
            return;
        }

        var themesRoot = Path.GetDirectoryName(themeRoot);
        if (string.IsNullOrWhiteSpace(themesRoot))
        {
            issues.Add($"BKT-0100: theme.yaml.extends cannot be resolved from '{themeRoot}'.");
            return;
        }

        var parentThemeRoot = Path.Combine(themesRoot, trimmedExtends);
        var parentThemeManifest = Path.Combine(parentThemeRoot, "theme.yaml");

        if (!File.Exists(parentThemeManifest))
        {
            issues.Add($"BKT-0100: theme.yaml extends parent theme '{trimmedExtends}' not found at '{parentThemeManifest}'.");
        }
    }
}
