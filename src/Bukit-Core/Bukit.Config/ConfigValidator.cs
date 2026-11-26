using Bukit.Shared;

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
        SiteConfigValidator.ValidateSite(config.Site);
        ValidateBreadcrumbParent(config);
        MiniProgramConfigValidator.Validate(config);

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

                if (source.Type.Equals("cloudbase", StringComparison.OrdinalIgnoreCase))
                {
                    if (source.CloudBase is null)
                    {
                        throw new ConfigException(
                            "content.sources[].cloudbase is required when type is cloudbase.",
                            DiagnosticCode.CloudBaseConfigInvalid);
                    }

                    ProviderValidators.ValidateCloudBase(source.CloudBase, $"{sourcePath}.cloudbase", mode, config.Content.Media);
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
        ValidateContentDerivation(config);

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
        ValidateSharedAssets(config);
        SiteLanguageIdentity.Validate(config.Site);
        SeoArticleConfig.Validate(config.Site);

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

        if (config.Build.TemplateLoopLimit < 1)
        {
            throw new ConfigException("build.templateLoopLimit must be a positive integer.", DiagnosticCode.ConfigInvalidValue);
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

        var taxonomyEmptyTerms = (config.Taxonomy.EmptyTerms ?? "render").Trim().ToLowerInvariant();
        if (taxonomyEmptyTerms is not ("render" or "noindex" or "skip"))
        {
            throw new ConfigException("taxonomy.emptyTerms must be render|noindex|skip.", DiagnosticCode.ConfigInvalidValue);
        }

        if (config.Taxonomy.TieBreakField is not null && string.IsNullOrWhiteSpace(config.Taxonomy.TieBreakField))
        {
            throw new ConfigException("taxonomy.tieBreakField must be title, url, or a content field name.", DiagnosticCode.ConfigInvalidValue);
        }

        var content = config.Content;
        if (content.OrderingTieBreakField is not null && string.IsNullOrWhiteSpace(content.OrderingTieBreakField))
        {
            throw new ConfigException("content.ordering.tieBreakField must be url, title, or a content field name.", DiagnosticCode.ConfigInvalidValue);
        }

        if ((content.OrderingTieBreakOrder ?? "asc").Trim().ToLowerInvariant() is not ("asc" or "desc"))
        {
            throw new ConfigException("content.ordering.tieBreakOrder must be asc|desc.", DiagnosticCode.ConfigInvalidValue);
        }

        var taxonomyTieBreakOrder = (config.Taxonomy.TieBreakOrder ?? "asc").Trim().ToLowerInvariant();
        if (taxonomyTieBreakOrder is not ("asc" or "desc"))
        {
            throw new ConfigException("taxonomy.tieBreakOrder must be asc|desc.", DiagnosticCode.ConfigInvalidValue);
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

    private static void ValidateSharedAssets(AppConfig config)
    {
        var shared = config.Site.SharedAssets;
        if (shared.Enabled && config.Site.DefaultLanguageInSubdir)
        {
            throw new ConfigException(
                "site.sharedAssets.enabled requires a root default language; site.defaultLanguageInSubdir must be false when shared assets are enabled.",
                DiagnosticCode.ConfigInvalidValue);
        }

        var mediaPrefix = SharedAssetsPath.Normalize(config.Content.Media.UrlBase);
        if (shared.Paths is null)
        {
            if (shared.Enabled && SiteLanguageResolver.Clean(config.Site.Languages).Count >= 2)
            {
                var defaults = new List<string> { SharedAssetsPath.Normalize("assets/") };
                if (config.Theme.Scss is { Enabled: true } scss)
                {
                    var scssOutput = SharedAssetsPath.Normalize(scss.OutputDir);
                    if (!SharedAssetsPath.IsRoot(scssOutput))
                    {
                        defaults.Add(scssOutput);
                    }
                }

                if (defaults.All(prefix => SharedAssetsPath.IsInside(prefix, mediaPrefix)))
                {
                    throw new ConfigException(
                        "site.sharedAssets.paths is not set, but content.media.urlBase covers every default shared prefix. Set site.sharedAssets.paths to a prefix outside content.media.urlBase.",
                        DiagnosticCode.ConfigInvalidValue);
                }
            }

            return;
        }

        if (shared.Paths.Count == 0)
        {
            throw new ConfigException(
                "site.sharedAssets.paths must not be an empty list.",
                DiagnosticCode.ConfigInvalidValue);
        }

        for (var i = 0; i < shared.Paths.Count; i++)
        {
            var raw = shared.Paths[i] ?? string.Empty;
            var field = $"site.sharedAssets.paths[{i}]";
            if (string.IsNullOrWhiteSpace(raw))
            {
                throw new ConfigException(
                    $"{field} must not be blank.",
                    DiagnosticCode.ConfigInvalidValue);
            }

            if (SharedAssetsPath.IsAbsolute(raw))
            {
                throw new ConfigException(
                    $"{field} must be a relative output path prefix.",
                    DiagnosticCode.ConfigInvalidValue);
            }

            var normalized = SharedAssetsPath.Normalize(raw);
            if (SharedAssetsPath.ContainsParentSegment(normalized))
            {
                throw new ConfigException(
                    $"{field} must not contain '..' path traversal segments.",
                    DiagnosticCode.ConfigPathTraversal);
            }

            if (SharedAssetsPath.IsRoot(normalized))
            {
                throw new ConfigException(
                    $"{field} must not normalize to the output root.",
                    DiagnosticCode.ConfigInvalidValue);
            }

            if (SharedAssetsPath.IsInside(normalized, mediaPrefix))
            {
                throw new ConfigException(
                    $"{field} '{raw}' is inside content.media.urlBase; media sharing is not supported.",
                    DiagnosticCode.ConfigInvalidValue);
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

        // Keys are case-sensitive, as the data index stores them; the scope is lower case by rule.
        var required = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in source.DataIndex.RequiredKeys ?? Array.Empty<string>())
        {
            // The scope is the first segment; the key may itself contain dots.
            var parts = key.Split('.', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !IsDataIndexIdentifier(parts[0]) || !DataIndexKey.IsValid(parts[1]))
            {
                throw new ConfigException($"{sourcePath}.dataIndex.requiredKeys values must use scope.key: a scope matching ^[a-z][a-z0-9_]*$ and a key matching {DataIndexKey.Pattern}.", DiagnosticCode.ConfigInvalidValue);
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

    /// <summary>Names every link carries; <c>select</c> must not overwrite them.</summary>
    internal static readonly IReadOnlySet<string> ContentLinkBaseKeys =
        new HashSet<string>(["key", "title", "slug", "url", "collection"], StringComparer.OrdinalIgnoreCase);

    /// <summary>content.links, content.derivedFields, enum labels and site.queries, which read one another's outputs.</summary>
    private static void ValidateContentDerivation(AppConfig config)
    {
        var linkOutputs = ValidateContentLinks(config.Content);
        ValidateDerivedFields(config.Content.DerivedFields, linkOutputs);
        ValidateFieldEnumNames(config.Content.ModelSchema);
        ValidateQueryBackrefs(config.Site.Queries, config.Content.Links);
    }

    /// <returns>The fields the links write, as <c>collection\nfield</c>.</returns>
    private static HashSet<string> ValidateContentLinks(ContentConfig content)
    {
        var links = content.Links;
        // Two links (or backrefs) writing the same page field would overwrite each other.
        var outputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < links.Count; index++)
        {
            var link = links[index];
            var path = $"content.links[{index}]";
            if (!ContentLinkPath.TryParse(link.Field, out var field))
            {
                throw new ConfigException(
                    $"{path}.field '{link.Field}' is not a valid path; use a field name or a nested path such as days[].attractions (at most {ContentLinkPath.MaxSegments} names).",
                    DiagnosticCode.ConfigInvalidValue);
            }

            if (!ContentLinkPath.IsName(link.Target.Key))
            {
                throw new ConfigException($"{path}.target.key '{link.Target.Key}' is not a valid field name.", DiagnosticCode.ConfigInvalidValue);
            }

            ValidateContentLinkSelect(link.Select, $"{path}.select");
            ClaimContentLinkOutput(outputs, link.Collection, field.IsNested ? field.FlatName : field.GroupedName, path);
            if (link.Backref is { } backref)
            {
                var backrefPath = $"{path}.backref";
                if (!ContentLinkPath.IsName(backref.Name))
                {
                    throw new ConfigException($"{backrefPath}.name '{backref.Name}' is not a valid field name.", DiagnosticCode.ConfigInvalidValue);
                }

                if (backref.Limit < 0)
                {
                    throw new ConfigException($"{backrefPath}.limit must be 0 (no limit) or more.", DiagnosticCode.ConfigInvalidValue);
                }

                ValidateContentLinkSelect(backref.Select, $"{backrefPath}.select");
                ClaimContentLinkOutput(outputs, link.Target.Collection, backref.Name, backrefPath);
                ClaimContentLinkOutput(outputs, link.Target.Collection, backref.Name + "_count", backrefPath);
            }
        }

        return outputs;
    }

    /// <summary>
    /// <c>hasBackref</c> reads the backref count, so it must name a backref that lists pages on the
    /// queried collection; a content field that happens to end in <c>_count</c> must not match.
    /// </summary>
    private static void ValidateQueryBackrefs(IReadOnlyList<SiteQueryConfig> queries, IReadOnlyList<ContentLinkConfig> links)
    {
        foreach (var query in queries)
        {
            for (var i = 0; i < query.Where.Count; i++)
            {
                if (query.Where[i].HasBackref is not { } name ||
                    links.Any(link => link.Backref is { } backref &&
                        string.Equals(backref.Name, name, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(link.Target.Collection.Trim(), query.From, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                throw new ConfigException(
                    $"site.queries.{query.Name}.where[{i}].hasBackref '{name}' is not a content.links backref on collection '{query.From}'.",
                    DiagnosticCode.ConfigInvalidValue);
            }
        }
    }

    /// <summary>Fields that decide identity, routing or language; a derived value must not replace them.</summary>
    internal static readonly IReadOnlySet<string> DerivedFieldReservedNames = new HashSet<string>(
        ["id", "slug", "title", "collection", "type", "sourceMode", "sourceKey", "language", "locale", "draft",
         "status", "route", "publishAt", "translations", "i18nKey", "i18n_key"],
        StringComparer.OrdinalIgnoreCase);

    /// <summary><c>site.field_enums.scopes</c> holds the field scopes, so a custom enum field cannot be named scopes.</summary>
    private static void ValidateFieldEnumNames(ContentModelSchemaConfig? schema)
    {
        if (schema?.CustomFields?.FirstOrDefault(field =>
                field.Enum is { Count: > 0 } && string.Equals(field.Name, "scopes", StringComparison.OrdinalIgnoreCase)) is not null)
        {
            throw new ConfigException(
                "content.modelSchema.customFields: an enum field cannot be named 'scopes', which site.field_enums uses for field scopes.",
                DiagnosticCode.ConfigInvalidValue);
        }
    }

    private static void ValidateDerivedFields(IReadOnlyList<ContentDerivedFieldConfig> derived, HashSet<string> linkOutputs)
    {
        foreach (var item in derived)
        {
            var path = $"content.derivedFields.{item.Collection}.{item.Field}";
            if (!ContentLinkPath.IsName(item.Field) || DerivedFieldReservedNames.Contains(item.Field))
            {
                throw new ConfigException(
                    $"{path} must be a field name other than {string.Join(", ", DerivedFieldReservedNames)}.",
                    DiagnosticCode.ConfigInvalidValue);
            }

            if (item.Mode is not ("default" or "override"))
            {
                throw new ConfigException($"{path}.mode must be default or override.", DiagnosticCode.ConfigInvalidValue);
            }

            // Links run first; a derived value would replace their list or count with text.
            if (linkOutputs.Contains($"{item.Collection.Trim()}\n{item.Field}"))
            {
                throw new ConfigException(
                    $"{path} is a field that content.links writes in collection '{item.Collection}'.",
                    DiagnosticCode.ConfigInvalidValue);
            }
        }
    }

    private static void ValidateContentLinkSelect(IReadOnlyList<string> select, string path)
    {
        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in select)
        {
            if (!ContentLinkPath.IsName(name) || ContentLinkBaseKeys.Contains(name) || !selected.Add(name))
            {
                throw new ConfigException(
                    $"{path} '{name}' must be a distinct field name other than {string.Join(", ", ContentLinkBaseKeys)}.",
                    DiagnosticCode.ConfigInvalidValue);
            }
        }
    }

    /// <summary>Rejects a second content.links writer of one field in one collection.</summary>
    private static void ClaimContentLinkOutput(HashSet<string> outputs, string collection, string output, string path)
    {
        if (!outputs.Add($"{collection.Trim()}\n{output}"))
        {
            throw new ConfigException(
                $"{path} writes '{output}' in collection '{collection}', which another content.links entry already writes.",
                DiagnosticCode.ConfigInvalidValue);
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

    private static void ValidateBreadcrumbParent(AppConfig config)
    {
        var parent = config.Site.Seo.Schema.BreadcrumbParent?.Trim();
        if (string.IsNullOrEmpty(parent))
        {
            return;
        }

        // Kinds as TaxonomyPlugin names them: configured kinds, or the built-in tags/categories.
        var known = config.Taxonomy.Kinds is { Count: > 0 } kinds
            ? kinds.Select(kind => string.IsNullOrWhiteSpace(kind.Kind) ? kind.Key.Trim() : kind.Kind.Trim()).ToArray()
            : ["tags", "categories"];
        if (!known.Contains(parent, StringComparer.OrdinalIgnoreCase))
        {
            throw new ConfigException(
                $"site.seo.schema.breadcrumbParent '{parent}' is not a taxonomy kind ({string.Join("|", known)}).",
                DiagnosticCode.ConfigInvalidValue);
        }
    }
}
