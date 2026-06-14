using System.Diagnostics.CodeAnalysis;
using YamlDotNet.RepresentationModel;

namespace Bukit.Config;

internal static class ThemeManifestStrictValidator
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

    private static void ValidateTemplatePath(
        string? templatePath,
        string themeRoot,
        string section,
        string field,
        bool enforceLayouts,
        List<string> issues)
    {
        var value = templatePath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add($"BKT-0100: {section}.{field} must be a non-empty path.");
            return;
        }

        var rootPath = Path.GetFullPath(enforceLayouts
            ? Path.Combine(themeRoot, "layouts")
            : themeRoot);
        var normalizedRoot = EnsureTrailingSeparator(rootPath);

        if (value.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            issues.Add($"BKT-0100: {section}.{field} contains invalid path characters.");
            return;
        }

        if (Path.IsPathRooted(value) || value.StartsWith("/", StringComparison.Ordinal))
        {
            issues.Add($"BKT-0100: {section}.{field} must be a relative path within theme.");
            return;
        }

        var hasTraversal = false;
        var segments = value.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            if (segment == "..")
            {
                hasTraversal = true;
                break;
            }

            if (segment.Contains(':', StringComparison.Ordinal))
            {
                hasTraversal = true;
                break;
            }
        }

        if (hasTraversal)
        {
            issues.Add($"BKT-0100: {section}.{field} has path traversal characters.");
            return;
        }

        var candidatePath = Path.GetFullPath(Path.Combine(rootPath, value.Replace('/', Path.DirectorySeparatorChar)));
        if (!candidatePath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add($"BKT-0100: {section}.{field} is outside theme scope: {value}.");
        }
    }

    private static void ValidateAssetPath(string? assetPath, string themeRoot, string fieldPath, List<string> issues)
        => ValidateTemplatePath(assetPath, themeRoot, fieldPath, "value", false, issues);

    private static void ValidateAssetSequence(YamlSequenceNode list, string themeRoot, string fieldPath, List<string> issues)
    {
        var index = 0;
        foreach (var item in list.Children)
        {
            if (item is YamlScalarNode scalar)
            {
                ValidateTemplatePath(scalar.Value, themeRoot, fieldPath, $"[{index}]", false, issues);
            }
            else
            {
                issues.Add($"BKT-0100: {fieldPath}[{index}] must be a string.");
            }

            index++;
        }
    }

    private static void ValidateBoolean(YamlMappingNode map, string key, string path, List<string> issues)
    {
        if (!HasField(map, key))
        {
            return;
        }

        var node = TryGetNode(map, key);
        if (node is not YamlScalarNode scalar || !bool.TryParse(scalar.Value, out _))
        {
            issues.Add($"BKT-0100: {path} must be a boolean.");
        }
    }

    private static void ValidateOptionalString(YamlMappingNode map, string key, string path, List<string> issues)
    {
        if (!HasField(map, key))
        {
            return;
        }

        if (TryGetNode(map, key) is not YamlScalarNode scalar)
        {
            issues.Add($"BKT-0100: {path} must be a string.");
            return;
        }

        if (scalar.Value is null || scalar.Value.Trim().Length == 0)
        {
            issues.Add($"BKT-0100: {path} must be a non-empty string when set.");
        }
    }

    private static void ValidateOptionalStringList(YamlMappingNode map, string key, string path, List<string> issues)
    {
        var value = TryGetNode(map, key);
        if (value is null)
        {
            return;
        }

        if (value is YamlSequenceNode sequence)
        {
            for (var i = 0; i < sequence.Children.Count; i++)
            {
                if (sequence.Children[i] is not YamlScalarNode scalar || string.IsNullOrWhiteSpace(scalar.Value))
                {
                    issues.Add($"BKT-0100: {path}[{i}] must be a string.");
                }
            }

            return;
        }

        if (value is YamlScalarNode scalarValue)
        {
            if (string.IsNullOrWhiteSpace(scalarValue.Value))
            {
                issues.Add($"BKT-0100: {path} must be a non-empty string.");
            }

            return;
        }

        issues.Add($"BKT-0100: {path} must be a string or list of strings.");
    }

    private static bool IsValidThemeName(string value, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            error = "theme name is null or whitespace.";
            return false;
        }

        if (Path.IsPathRooted(value))
        {
            error = "theme name must not be an absolute path.";
            return false;
        }

        if (value == ".." || value.Contains("..", StringComparison.Ordinal))
        {
            error = "theme name must not contain '..' segments.";
            return false;
        }

        if (value.Contains('/') || value.Contains('\\'))
        {
            error = "theme name must not contain path separators.";
            return false;
        }

        foreach (var ch in value)
        {
            if (ch < 32)
            {
                error = "theme name contains control characters.";
                return false;
            }
        }

        if (IsWindowsDeviceName(value))
        {
            error = $"theme name '{value}' is a reserved Windows device name.";
            return false;
        }

        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch) || ch is '_' || ch is '-' || ch is '.')
            {
                continue;
            }

            error = $"theme name '{value}' contains invalid character '{ch}'. Only [A-Za-z0-9_-.] are allowed.";
            return false;
        }

        return true;
    }

    private static bool IsWindowsDeviceName(string value)
    {
        var segment = value.Trim().ToLowerInvariant();
        if (segment is "con" or "prn" or "aux" or "nul")
        {
            return true;
        }

        if (segment.Length == 4 && segment.StartsWith("com", StringComparison.Ordinal) && char.IsDigit(segment[3]))
        {
            return true;
        }

        if (segment.Length == 4 && segment.StartsWith("lpt", StringComparison.Ordinal) && char.IsDigit(segment[3]))
        {
            return true;
        }

        return false;
    }

    private static bool TryGetMap(YamlMappingNode root, string key, [NotNullWhen(true)] out YamlMappingNode? map)
    {
        map = null;
        if (!TryGetNode(root, key, out var node))
        {
            return false;
        }

        map = node as YamlMappingNode;
        return map is not null;
    }

    private static bool TryGetString(YamlMappingNode map, string key, out string? value)
    {
        value = null;
        if (!TryGetNode(map, key, out var node) || node is not YamlScalarNode scalar)
        {
            return false;
        }

        value = scalar.Value;
        return true;
    }

    private static bool TryGetNode(YamlMappingNode root, string key, [NotNullWhen(true)] out YamlNode? node)
    {
        foreach (var (nodeKey, nodeValue) in root.Children)
        {
            if (nodeKey is YamlScalarNode scalar && string.Equals(scalar.Value, key, StringComparison.OrdinalIgnoreCase))
            {
                node = nodeValue;
                return true;
            }
        }

        node = null;
        return false;
    }

    private static bool HasField(YamlMappingNode map, string key)
        => TryGetNode(map, key, out _);

    private static YamlNode? TryGetNode(YamlMappingNode root, string key)
        => TryGetNode(root, key, out var node)
            ? node
            : null;

    private static IEnumerable<(string Key, YamlNode Value)> EnumerateMap(YamlMappingNode map)
    {
        foreach (var (keyNode, value) in map.Children)
        {
            if (keyNode is YamlScalarNode key && key.Value is string keyValue && !string.IsNullOrWhiteSpace(keyValue))
            {
                yield return (keyValue, value);
            }
        }
    }

    private static void AddUnknownFields(YamlMappingNode map, HashSet<string> allowed, string path, List<string> issues)
    {
        foreach (var (key, _) in map.Children)
        {
            if (key is not YamlScalarNode scalar || scalar.Value is not string keyValue)
            {
                issues.Add($"BKT-0100: {path} contains an unknown non-scalar field key.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(keyValue))
            {
                issues.Add($"BKT-0100: {path}: unknown field ''.");
                continue;
            }

            if (!allowed.Contains(keyValue))
            {
                issues.Add($"BKT-0100: unknown field '{path}.{keyValue}'.");
            }
        }
    }

    private static bool TryGetStringValue(YamlScalarNode node, [NotNullWhen(true)] out string value)
    {
        if (node.Value is string nodeValue)
        {
            value = nodeValue;
            return !string.IsNullOrWhiteSpace(value);
        }

        value = string.Empty;
        return false;
    }

    private static string EnsureTrailingSeparator(string value)
    {
        var normalized = value.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return normalized + Path.DirectorySeparatorChar;
    }
}
