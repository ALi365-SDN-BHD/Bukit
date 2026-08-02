using Bukit.Shared;
using YamlDotNet.RepresentationModel;

namespace Bukit.Config;

public sealed record ConfigValidationOptions
{
    public bool ValidateProviderSecrets { get; init; } = true;
}

public static class ConfigValidator
{
    public static void Validate(AppConfig config)
        => Validate(config, new ConfigValidationOptions());

    public static void Validate(AppConfig config, ConfigValidationOptions options)
    {
        I18nValidator.ValidateSite(config.Site);

        if (config.Site.Analytics.Csp is not null && !config.Build.Report.Enabled)
        {
            throw new ConfigException(
                "site.analytics.csp requires build.report.enabled: true.",
                DiagnosticCode.ConfigInvalidValue);
        }

        if (config.Site.Collections is not null)
        {
            CollectionsValidator.ValidateCollections(config.Site.Collections);
        }

        if (config.Site.Collections is { Count: > 0 } && config.Content.Sources is { Count: > 0 })
        {
            CollectionsValidator.ValidateSourcesToCollections(config.Content.Sources, config.Site.Collections);
        }

        if (config.Content.Sources is not { Count: > 0 })
        {
            throw new ConfigException("content.sources is required in Bukit 1.0.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        if (config.Content.Sources is { Count: > 0 })
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < config.Content.Sources.Count; i++)
            {
                var source = config.Content.Sources[i];
                var sourcePath = $"content.sources[{i}]";
                if (string.IsNullOrWhiteSpace(source.Type))
                {
                    throw new ConfigException("content.sources[].type is required.", DiagnosticCode.ConfigRequiredFieldMissing);
                }

                var mode = (source.Mode ?? "content").Trim().ToLowerInvariant();
                if (mode is not ("content" or "data"))
                {
                    throw new ConfigException("content.sources[].mode must be content|data.", DiagnosticCode.ConfigInvalidValue);
                }

                if (!string.IsNullOrWhiteSpace(source.Name))
                {
                    if (!names.Add(source.Name.Trim()))
                    {
                        throw new ConfigException("content.sources[].name must be unique when set.", DiagnosticCode.ConfigInvalidValue);
                    }
                }

                if (source.AddToCollections is { Count: > 0 } &&
                    source.AddToCollections.Any(string.IsNullOrWhiteSpace))
                {
                    throw new ConfigException("content.sources[].addToCollections must contain non-empty collection names.", DiagnosticCode.ConfigInvalidValue);
                }

                ValidateDataIndex(source, sourcePath, mode);

                if (source.Type.Equals("notion", StringComparison.OrdinalIgnoreCase))
                {
                    if (source.Notion is null)
                    {
                        throw new ConfigException("content.sources[].notion is required when type is notion.", DiagnosticCode.ConfigRequiredFieldMissing);
                    }

                    ProviderValidators.ValidateNotion(source.Notion, $"{sourcePath}.notion", options.ValidateProviderSecrets);
                    continue;
                }

                if (source.Type.Equals("markdown", StringComparison.OrdinalIgnoreCase))
                {
                    if (source.Markdown is null)
                    {
                        throw new ConfigException("content.sources[].markdown is required when type is markdown.", DiagnosticCode.ConfigRequiredFieldMissing);
                    }

                    ProviderValidators.ValidateMarkdown(source.Markdown, $"{sourcePath}.markdown");
                    continue;
                }

                throw new ConfigException($"Unsupported content source type: {source.Type}", DiagnosticCode.ConfigInvalidValue);
            }
        }

        ValidateRouteMetadata(config.Content);

        ProviderValidators.ValidateMedia(config.Content.Media);

        if (!string.IsNullOrWhiteSpace(config.Site.Timezone))
        {
            if (!TimeZoneResolver.TryResolve(config.Site.Timezone, out _))
            {
                throw new ConfigException($"site.timezone '{config.Site.Timezone}' is not a valid time zone identifier.", DiagnosticCode.ConfigInvalidValue);
            }
        }

        ProviderValidators.RejectPathTraversal("theme.layouts", config.Theme.Layouts);
        ProviderValidators.RejectPathTraversal("theme.assets", config.Theme.Assets);
        ProviderValidators.RejectPathTraversal("theme.static", config.Theme.Static);
        if (config.Theme.Name is not null)
        {
            ProviderValidators.RejectPathTraversal("theme.name", config.Theme.Name);
        }

        ValidateScss(config.Theme.Scss);

        var componentValidation = (config.Theme.ComponentValidation ?? "off").Trim().ToLowerInvariant();
        if (componentValidation is not ("off" or "warn" or "strict"))
        {
            throw new ConfigException("theme.componentValidation must be off|warn|strict.", DiagnosticCode.ConfigInvalidValue);
        }

        if (string.IsNullOrWhiteSpace(config.Build.Output))
        {
            throw new ConfigException("build.output is required.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        ProviderValidators.RejectPathTraversal("build.output", config.Build.Output);
        var listPageContentMode = (config.Build.ListPageContentMode ?? "auto").Trim().ToLowerInvariant();
        if (listPageContentMode is not ("auto" or "always" or "never"))
        {
            throw new ConfigException("build.listPageContentMode must be auto|always|never.", DiagnosticCode.ConfigInvalidValue);
        }

        var fingerprintMode = (config.Build.FingerprintMode ?? "size-time").Trim().ToLowerInvariant();
        if (fingerprintMode is not ("size-time" or "sha256"))
        {
            throw new ConfigException("build.fingerprintMode must be size-time|sha256.", DiagnosticCode.ConfigInvalidValue);
        }

        var securityFailMode = (config.Build.Report.SecurityFailMode ?? "auto").Trim().ToLowerInvariant();
        if (securityFailMode is not ("auto" or "off" or "warn" or "strict"))
        {
            throw new ConfigException("build.report.securityFailMode must be auto|off|warn|strict.", DiagnosticCode.ConfigInvalidValue);
        }

        var loggingLevel = (config.Logging.Level ?? "info").Trim().ToLowerInvariant();
        if (loggingLevel is not ("debug" or "info" or "warn" or "error"))
        {
            throw new ConfigException("logging.level must be debug|info|warn|error.", DiagnosticCode.ConfigInvalidValue);
        }

        if (config.Deploy is not null)
        {
            ProviderValidators.ValidateDeployConfig(config.Deploy);
        }

        var taxonomyOutputMode = (config.Taxonomy.OutputMode ?? "both").Trim().ToLowerInvariant();
        if (taxonomyOutputMode is not ("both" or "pages" or "data" or "fields_only"))
        {
            throw new ConfigException("taxonomy.outputMode must be both|pages|data|fields_only.", DiagnosticCode.ConfigInvalidValue);
        }

        if (config.Taxonomy.PageSize <= 0)
        {
            throw new ConfigException("taxonomy.pageSize must be a positive integer.", DiagnosticCode.ConfigInvalidValue);
        }

        if (config.Taxonomy.ItemFields is { Count: > 0 } itemFields)
        {
            for (var i = 0; i < itemFields.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(itemFields[i]))
                {
                    throw new ConfigException($"taxonomy.itemFields[{i}] must be a non-empty string.", DiagnosticCode.ConfigInvalidValue);
                }
            }
        }

        if (config.Taxonomy.Kinds is { Count: > 0 } kinds)
        {
            for (var i = 0; i < kinds.Count; i++)
            {
                ValidateTaxonomyKindConfig($"taxonomy.kinds[{i}]", kinds[i]);
            }
        }
    }

    private static void ValidateScss(ScssConfig? scss)
    {
        if (scss is null)
        {
            return;
        }

        if (scss.EntryPoint is not null)
        {
            if (string.IsNullOrWhiteSpace(scss.EntryPoint))
            {
                throw new ConfigException(
                    "theme.scss.entryPoint must be a non-empty relative .scss path when set.",
                    DiagnosticCode.ConfigInvalidValue);
            }

            ProviderValidators.RejectPathTraversal("theme.scss.entryPoint", scss.EntryPoint);
            RejectDriveRelativePath("theme.scss.entryPoint", scss.EntryPoint);
            if (!string.Equals(Path.GetExtension(scss.EntryPoint), ".scss", StringComparison.OrdinalIgnoreCase))
            {
                throw new ConfigException(
                    "theme.scss.entryPoint must reference a .scss file.",
                    DiagnosticCode.ConfigInvalidValue);
            }
        }

        if (string.IsNullOrWhiteSpace(scss.OutputDir))
        {
            throw new ConfigException(
                "theme.scss.outputDir must be a non-empty relative path.",
                DiagnosticCode.ConfigInvalidValue);
        }

        ProviderValidators.RejectPathTraversal("theme.scss.outputDir", scss.OutputDir);
        RejectDriveRelativePath("theme.scss.outputDir", scss.OutputDir);
    }

    private static void RejectDriveRelativePath(string fieldName, string value)
    {
        if (value.Length >= 2 && char.IsLetter(value[0]) && value[1] == ':')
        {
            throw new ConfigException(
                $"{fieldName} must be a relative path without a drive prefix.",
                DiagnosticCode.ConfigPathTraversal);
        }
    }

    private static void ValidateDataIndex(ContentSourceConfig source, string sourcePath, string mode)
    {
        if (source.DataIndex is null)
        {
            return;
        }

        if (mode != "data")
        {
            throw new ConfigException($"{sourcePath}.dataIndex requires mode: data.", DiagnosticCode.ConfigInvalidValue);
        }

        var sourceName = source.Name?.Trim() ?? string.Empty;
        if (!IsDataIndexIdentifier(sourceName))
        {
            throw new ConfigException($"{sourcePath}.name must match ^[a-z][a-z0-9_]*$ when dataIndex is configured.", DiagnosticCode.ConfigInvalidValue);
        }

        var fields = new Dictionary<string, string>
        {
            ["scopeField"] = source.DataIndex.ScopeField,
            ["keyField"] = source.DataIndex.KeyField,
            ["valueField"] = source.DataIndex.ValueField,
            ["valueTypeField"] = source.DataIndex.ValueTypeField
        };
        foreach (var (fieldName, value) in fields)
        {
            if (!IsDataIndexIdentifier(value))
            {
                throw new ConfigException($"{sourcePath}.dataIndex.{fieldName} must match ^[a-z][a-z0-9_]*$.", DiagnosticCode.ConfigInvalidValue);
            }
        }

        var required = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in source.DataIndex.RequiredKeys ?? Array.Empty<string>())
        {
            var parts = key.Split('.', StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !IsDataIndexIdentifier(parts[0]) || !IsDataIndexIdentifier(parts[1]))
            {
                throw new ConfigException($"{sourcePath}.dataIndex.requiredKeys values must use scope.key with safe identifiers.", DiagnosticCode.ConfigInvalidValue);
            }

            if (!required.Add($"{parts[0]}.{parts[1]}"))
            {
                throw new ConfigException($"{sourcePath}.dataIndex.requiredKeys contains duplicate key '{key}'.", DiagnosticCode.ConfigInvalidValue);
            }
        }
    }

    private static void ValidateRouteMetadata(ContentConfig content)
    {
        var routeMetadata = content.RouteMetadata;
        if (routeMetadata is null)
        {
            return;
        }

        var fields = new Dictionary<string, string>
        {
            ["source"] = routeMetadata.Source,
            ["routeField"] = routeMetadata.RouteField,
            ["titleField"] = routeMetadata.TitleField,
            ["summaryField"] = routeMetadata.SummaryField,
            ["seoTitleField"] = routeMetadata.SeoTitleField,
            ["seoDescriptionField"] = routeMetadata.SeoDescriptionField
        };
        foreach (var (fieldName, value) in fields)
        {
            if (!IsRouteMetadataIdentifier(value))
            {
                throw new ConfigException($"content.routeMetadata.{fieldName} must match ^[a-z][a-z0-9_]*$.", DiagnosticCode.ConfigInvalidValue);
            }
        }

        var requiredRoutes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var route in routeMetadata.RequiredRoutes)
        {
            if (string.IsNullOrWhiteSpace(route) ||
                !route.StartsWith("/", StringComparison.Ordinal) ||
                !route.EndsWith("/", StringComparison.Ordinal))
            {
                throw new ConfigException("content.routeMetadata.requiredRoutes values must start and end with '/'.", DiagnosticCode.ConfigInvalidValue);
            }

            if (!requiredRoutes.Add(route))
            {
                throw new ConfigException($"content.routeMetadata.requiredRoutes contains duplicate route '{route}'.", DiagnosticCode.ConfigInvalidValue);
            }
        }

        var source = content.Sources?.FirstOrDefault(candidate =>
            string.Equals(candidate.Name?.Trim(), routeMetadata.Source.Trim(), StringComparison.OrdinalIgnoreCase));
        if (source is null)
        {
            throw new ConfigException($"content.routeMetadata.source references unknown data source '{routeMetadata.Source}'.", DiagnosticCode.ConfigInvalidValue);
        }

        if (!string.Equals(source.Mode?.Trim(), "data", StringComparison.OrdinalIgnoreCase))
        {
            throw new ConfigException("content.routeMetadata.source must reference a source with mode: data.", DiagnosticCode.ConfigInvalidValue);
        }

        if (source.DataIndex is not null)
        {
            throw new ConfigException("content.routeMetadata.source must not declare dataIndex because route metadata is reserved for engine routing and is not exposed through template data bindings.", DiagnosticCode.ConfigInvalidValue);
        }
    }

    private static bool IsRouteMetadataIdentifier(string? value)
        => value is not null &&
           string.Equals(value, value.Trim(), StringComparison.Ordinal) &&
           IsDataIndexIdentifier(value);

    private static bool IsDataIndexIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        if (text[0] is < 'a' or > 'z')
        {
            return false;
        }

        return text.Skip(1).All(ch => ch is >= 'a' and <= 'z' or >= '0' and <= '9' or '_');
    }

    /// <summary>
    /// 1.0 theme.yaml validation. Returns a non-empty list of issues when validation fails.
    /// Missing theme.yaml is a hard error in 1.0.
    /// </summary>
    public static List<string> ValidateThemeYaml(string themeRoot)
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

    private static void ValidateTaxonomyKindConfig(string prefix, TaxonomyKindConfig kind)
    {
        if (string.IsNullOrWhiteSpace(kind.Key))
        {
            throw new ConfigException($"{prefix}.key is required.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        if (kind.Kind is not null && string.IsNullOrWhiteSpace(kind.Kind))
        {
            throw new ConfigException($"{prefix}.kind must be a non-empty string when set.", DiagnosticCode.ConfigInvalidValue);
        }

        if (kind.Title is not null && string.IsNullOrWhiteSpace(kind.Title))
        {
            throw new ConfigException($"{prefix}.title must be a non-empty string when set.", DiagnosticCode.ConfigInvalidValue);
        }

        if (kind.Description is not null && string.IsNullOrWhiteSpace(kind.Description))
        {
            throw new ConfigException($"{prefix}.description must be a non-empty string when set.", DiagnosticCode.ConfigInvalidValue);
        }

        if (kind.SingularTitlePrefix is not null && string.IsNullOrWhiteSpace(kind.SingularTitlePrefix))
        {
            throw new ConfigException($"{prefix}.singularTitlePrefix must be a non-empty string when set.", DiagnosticCode.ConfigInvalidValue);
        }

        if (kind.Template is not null && string.IsNullOrWhiteSpace(kind.Template))
        {
            throw new ConfigException($"{prefix}.template must be a non-empty string when set.", DiagnosticCode.ConfigInvalidValue);
        }

        if (kind.IndexTemplate is not null && string.IsNullOrWhiteSpace(kind.IndexTemplate))
        {
            throw new ConfigException($"{prefix}.indexTemplate must be a non-empty string when set.", DiagnosticCode.ConfigInvalidValue);
        }

        if (kind.TermTemplate is not null && string.IsNullOrWhiteSpace(kind.TermTemplate))
        {
            throw new ConfigException($"{prefix}.termTemplate must be a non-empty string when set.", DiagnosticCode.ConfigInvalidValue);
        }

        if (kind.RoutePrefix is not null)
        {
            ValidateTaxonomyRoutePrefix($"{prefix}.routePrefix", kind.RoutePrefix);
        }
    }

    private static void ValidateTaxonomyRoutePrefix(string fieldName, string routePrefix)
    {
        if (string.IsNullOrWhiteSpace(routePrefix))
        {
            throw new ConfigException($"{fieldName} must be a non-empty internal URL path when set.", DiagnosticCode.ConfigInvalidValue);
        }

        var value = routePrefix.Trim();
        if (value.Any(char.IsControl))
        {
            throw new ConfigException($"{fieldName} must not contain control characters.", DiagnosticCode.ConfigInvalidValue);
        }

        if (!value.StartsWith("/", StringComparison.Ordinal))
        {
            throw new ConfigException($"{fieldName} must start with '/'.", DiagnosticCode.ConfigInvalidValue);
        }

        if (value.StartsWith("//", StringComparison.Ordinal) || value.Contains("://", StringComparison.Ordinal))
        {
            throw new ConfigException($"{fieldName} must be an internal URL path.", DiagnosticCode.ConfigInvalidValue);
        }

        if (value.Contains('\\'))
        {
            throw new ConfigException($"{fieldName} must not contain backslashes.", DiagnosticCode.ConfigInvalidValue);
        }

        if (value.Contains('?') || value.Contains('#'))
        {
            throw new ConfigException($"{fieldName} must not contain query strings or fragments.", DiagnosticCode.ConfigInvalidValue);
        }

        foreach (var segment in value.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment is "." or "..")
            {
                throw new ConfigException($"{fieldName} must not contain '..' path traversal segments.", DiagnosticCode.ConfigPathTraversal);
            }

            string decoded;
            try
            {
                decoded = Uri.UnescapeDataString(segment);
            }
            catch
            {
                throw new ConfigException($"{fieldName} must contain valid percent-encoding.", DiagnosticCode.ConfigInvalidValue);
            }

            if (decoded is "." or "..")
            {
                throw new ConfigException($"{fieldName} must not contain '..' path traversal segments.", DiagnosticCode.ConfigPathTraversal);
            }

            if (decoded.Contains('/') || decoded.Contains('\\'))
            {
                throw new ConfigException($"{fieldName} must not contain encoded slashes.", DiagnosticCode.ConfigInvalidValue);
            }
        }
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
