using Bukit.Shared;
using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;

namespace Bukit.Config;

public static class ConfigValidator
{
    public static void Validate(AppConfig config)
    {
        I18nValidator.ValidateSite(config.Site);

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

                if (source.Type.Equals("notion", StringComparison.OrdinalIgnoreCase))
                {
                    if (source.Notion is null)
                    {
                        throw new ConfigException("content.sources[].notion is required when type is notion.", DiagnosticCode.ConfigRequiredFieldMissing);
                    }

                    ProviderValidators.ValidateNotion(source.Notion, $"{sourcePath}.notion");
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

        ProviderValidators.ValidateMedia(config.Content.Media);

        if (!string.IsNullOrWhiteSpace(config.Site.Timezone))
        {
            if (!IsValidTimeZone(config.Site.Timezone))
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
            issues.Add("BKT-0100: theme.yaml not found. Bukit 1.0 requires a theme.yaml manifest. Generate one with 'bukit theme manifest'.");
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

            GetStringValue(root, "requires_bukit", out var requires);
            if (!string.IsNullOrWhiteSpace(requires) &&
                !requires.StartsWith(">=", StringComparison.Ordinal) &&
                !requires.StartsWith("^", StringComparison.Ordinal) &&
                !requires.StartsWith("~", StringComparison.Ordinal))
                issues.Add($"BKT-0100: theme.yaml: 'requires_bukit' '{requires}' should use semver range like '>=2.0.0'.");

            if (root.Children.TryGetValue(new YamlScalarNode("tags"), out var tagsNode) &&
                tagsNode is YamlSequenceNode tagsSeq &&
                tagsSeq.Children.Count == 0)
                issues.Add("BKT-0100: theme.yaml: 'tags' is an empty list.");
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
    }

    private static bool IsValidTimeZone(string timeZoneId)
    {
        if (TryResolveTimeZone(timeZoneId, out _))
        {
            return true;
        }

        if (OperatingSystem.IsWindows()
            && TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZoneId, out var windowsTimeZoneId)
            && TryResolveTimeZone(windowsTimeZoneId, out _))
        {
            return true;
        }

        if (OperatingSystem.IsWindows()
            && TimeZoneCompatibility.TryGetWindowsTimeZoneFallback(timeZoneId, out var fallbackWindowsTimeZoneId)
            && TryResolveTimeZone(fallbackWindowsTimeZoneId, out _))
        {
            return true;
        }

        return false;
    }

    private static bool TryResolveTimeZone(string timeZoneId, out TimeZoneInfo? timeZone)
    {
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            timeZone = null;
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            timeZone = null;
            return false;
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
