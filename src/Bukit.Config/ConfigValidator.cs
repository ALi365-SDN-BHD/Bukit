using Bukit.Shared;
using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;

namespace Bukit.Config;

public static class ConfigValidator
{
    public static void Validate(AppConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Site.Name))
        {
            throw new ConfigException("site.name is required.");
        }

        if (string.IsNullOrWhiteSpace(config.Site.Title))
        {
            throw new ConfigException("site.title is required.");
        }

        if (!string.IsNullOrWhiteSpace(config.Site.Url) &&
            !(config.Site.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
              config.Site.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConfigException("site.url must start with http:// or https:// when set.");
        }

        if (config.Site.AutoSummaryMaxLength <= 0 || config.Site.AutoSummaryMaxLength > 5000)
        {
            throw new ConfigException("site.autoSummaryMaxLength must be between 1 and 5000.");
        }

        if (string.IsNullOrWhiteSpace(config.Site.BaseUrl))
        {
            throw new ConfigException("site.baseUrl is required.");
        }

        if (!config.Site.BaseUrl.StartsWith('/'))
        {
            throw new ConfigException("site.baseUrl must start with '/'.");
        }

        var outputPathEncoding = (config.Site.OutputPathEncoding ?? "none").Trim().ToLowerInvariant();
        if (outputPathEncoding is not ("none" or "slug" or "urlencode" or "sanitize"))
        {
            throw new ConfigException("site.outputPathEncoding must be none|slug|urlencode|sanitize.");
        }

        if (config.Site.Languages is { Count: > 0 } languages)
        {
            var cleaned = languages.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
            if (cleaned.Count == 0)
            {
                throw new ConfigException("site.languages must contain at least one language.");
            }

            var dup = cleaned.GroupBy(x => x, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
            if (dup is not null)
            {
                throw new ConfigException($"site.languages has duplicate language: {dup.Key}");
            }

            var defaultLang = string.IsNullOrWhiteSpace(config.Site.DefaultLanguage) ? cleaned[0] : config.Site.DefaultLanguage.Trim();
            if (!cleaned.Contains(defaultLang, StringComparer.OrdinalIgnoreCase))
            {
                throw new ConfigException("site.defaultLanguage must be included in site.languages.");
            }
        }

        var sitemapMode = (config.Site.SitemapMode ?? "split").Trim().ToLowerInvariant();
        if (sitemapMode is not ("split" or "merged" or "index"))
        {
            throw new ConfigException("site.sitemapMode must be split|merged|index.");
        }

        var rssMode = (config.Site.RssMode ?? "split").Trim().ToLowerInvariant();
        if (rssMode is not ("split" or "merged"))
        {
            throw new ConfigException("site.rssMode must be split|merged.");
        }

        var searchMode = (config.Site.SearchMode ?? "split").Trim().ToLowerInvariant();
        if (searchMode is not ("split" or "merged" or "index"))
        {
            throw new ConfigException("site.searchMode must be split|merged|index.");
        }

        var seoRenderMode = (config.Site.Seo.RenderMode ?? "inject").Trim().ToLowerInvariant();
        if (seoRenderMode is not ("theme" or "inject" or "off"))
        {
            throw new ConfigException("site.seo.renderMode must be theme|inject|off.");
        }

        var seoDiagnostics = (config.Site.Seo.Diagnostics ?? "warn").Trim().ToLowerInvariant();
        if (seoDiagnostics is not ("off" or "warn" or "strict"))
        {
            throw new ConfigException("site.seo.diagnostics must be off|warn|strict.");
        }

        var geoAiBotMode = (config.Site.Seo.Geo.AiBotMode ?? "allow").Trim().ToLowerInvariant();
        if (geoAiBotMode is not ("allow" or "block" or "selective"))
        {
            throw new ConfigException("site.seo.geo.aiBotMode must be allow|block|selective.");
        }

        if (!string.IsNullOrWhiteSpace(config.Site.Analytics.GoogleAnalyticsId) &&
            !Regex.IsMatch(config.Site.Analytics.GoogleAnalyticsId.Trim(), "^G-[A-Z0-9]+$", RegexOptions.CultureInvariant))
        {
            throw new ConfigException("site.analytics.google_analytics_id must be a GA4 id starting with G-.");
        }

        var pluginFailMode = (config.Site.PluginFailMode ?? "strict").Trim().ToLowerInvariant();
        if (pluginFailMode is not ("strict" or "warn"))
        {
            throw new ConfigException("site.pluginFailMode must be strict|warn.");
        }

        var deriveConflictPolicy = (config.Site.DeriveConflictPolicy ?? "fail").Trim().ToLowerInvariant();
        if (deriveConflictPolicy is not ("fail" or "warn" or "last-wins"))
        {
            throw new ConfigException("site.deriveConflictPolicy must be fail|warn|last-wins.");
        }

        var externalAssemblyTrustMode = (config.Site.ExternalAssemblyTrustMode ?? "warn").Trim().ToLowerInvariant();
        if (externalAssemblyTrustMode is not ("strict" or "warn"))
        {
            throw new ConfigException("site.externalAssemblyTrustMode must be strict|warn.");
        }

        ValidateExternalAssemblyAllowlist(config.Site, externalAssemblyTrustMode);

        if (config.Site.Plugins is not null)
        {
            foreach (var kv in config.Site.Plugins)
            {
                if (string.IsNullOrWhiteSpace(kv.Key))
                {
                    throw new ConfigException("site.plugins keys must be non-empty strings.");
                }
            }
        }

        if (config.Site.Collections is not null)
        {
            ValidateCollections(config.Site.Collections);
        }

        if (config.Site.Collections is { Count: > 0 } && config.Content.Sources is { Count: > 0 })
        {
            ValidateSourcesToCollections(config.Content.Sources, config.Site.Collections);
        }

        if (config.Site.ExternalPlugins is not null)
        {
            ValidateExternalPlugins(config.Site.ExternalPlugins);
        }

        if (string.IsNullOrWhiteSpace(config.Content.Provider))
        {
            throw new ConfigException("content.provider is required.");
        }

        if (config.Content.Sources is { Count: > 0 })
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var source in config.Content.Sources)
            {
                if (string.IsNullOrWhiteSpace(source.Type))
                {
                    throw new ConfigException("content.sources[].type is required.");
                }

                var mode = (source.Mode ?? "content").Trim().ToLowerInvariant();
                if (mode is not ("content" or "data"))
                {
                    throw new ConfigException("content.sources[].mode must be content|data.");
                }

                if (!string.IsNullOrWhiteSpace(source.Name))
                {
                    if (!names.Add(source.Name.Trim()))
                    {
                        throw new ConfigException("content.sources[].name must be unique when set.");
                    }
                }

                if (source.AddToCollections is { Count: > 0 } &&
                    source.AddToCollections.Any(string.IsNullOrWhiteSpace))
                {
                    throw new ConfigException("content.sources[].addToCollections must contain non-empty collection names.");
                }

                if (source.Type.Equals("notion", StringComparison.OrdinalIgnoreCase))
                {
                    if (source.Notion is null)
                    {
                        throw new ConfigException("content.sources[].notion is required when type is notion.");
                    }

                    ValidateNotion(source.Notion);
                    continue;
                }

                if (source.Type.Equals("markdown", StringComparison.OrdinalIgnoreCase))
                {
                    if (source.Markdown is null)
                    {
                        throw new ConfigException("content.sources[].markdown is required when type is markdown.");
                    }

                    ValidateMarkdown(source.Markdown);
                    continue;
                }

                throw new ConfigException($"Unsupported content source type: {source.Type}");
            }
        }
        else if (config.Content.Provider.Equals("notion", StringComparison.OrdinalIgnoreCase))
        {
            if (config.Content.Notion is null)
            {
                throw new ConfigException("content.notion is required when provider is notion.");
            }

            ValidateNotion(config.Content.Notion);
        }

        else if (config.Content.Provider.Equals("markdown", StringComparison.OrdinalIgnoreCase))
        {
            if (config.Content.Markdown is null)
            {
                throw new ConfigException("content.markdown is required when provider is markdown.");
            }

            ValidateMarkdown(config.Content.Markdown);
        }

        ValidateMedia(config.Content.Media);

        if (!string.IsNullOrWhiteSpace(config.Site.Timezone))
        {
            if (!IsValidTimeZone(config.Site.Timezone))
            {
                throw new ConfigException($"site.timezone '{config.Site.Timezone}' is not a valid time zone identifier.");
            }
        }

        RejectPathTraversal("theme.layouts", config.Theme.Layouts);
        RejectPathTraversal("theme.assets", config.Theme.Assets);
        RejectPathTraversal("theme.static", config.Theme.Static);
        if (config.Theme.Name is not null)
        {
            RejectPathTraversal("theme.name", config.Theme.Name);
        }

        if (string.IsNullOrWhiteSpace(config.Build.Output))
        {
            throw new ConfigException("build.output is required.");
        }

        RejectPathTraversal("build.output", config.Build.Output);
        var listPageContentMode = (config.Build.ListPageContentMode ?? "auto").Trim().ToLowerInvariant();
        if (listPageContentMode is not ("auto" or "always" or "never"))
        {
            throw new ConfigException("build.listPageContentMode must be auto|always|never.");
        }

        var loggingLevel = (config.Logging.Level ?? "info").Trim().ToLowerInvariant();
        if (loggingLevel is not ("debug" or "info" or "warn" or "error"))
        {
            throw new ConfigException("logging.level must be debug|info|warn|error.");
        }

        if (config.Deploy is not null)
        {
            ValidateDeployConfig(config.Deploy);
        }

        if (string.IsNullOrWhiteSpace(config.Taxonomy.Template))
        {
            throw new ConfigException("taxonomy.template must be a non-empty string when set.");
        }

        var taxonomyOutputMode = (config.Taxonomy.OutputMode ?? "both").Trim().ToLowerInvariant();
        if (taxonomyOutputMode is not ("both" or "pages" or "data" or "fields_only"))
        {
            throw new ConfigException("taxonomy.outputMode must be both|pages|data|fields_only.");
        }

        if (config.Taxonomy.PageSize <= 0)
        {
            throw new ConfigException("taxonomy.pageSize must be a positive integer.");
        }

        if (config.Taxonomy.IndexTemplate is not null && string.IsNullOrWhiteSpace(config.Taxonomy.IndexTemplate))
        {
            throw new ConfigException("taxonomy.indexTemplate must be a non-empty string when set.");
        }

        if (config.Taxonomy.TermTemplate is not null && string.IsNullOrWhiteSpace(config.Taxonomy.TermTemplate))
        {
            throw new ConfigException("taxonomy.termTemplate must be a non-empty string when set.");
        }

        if (config.Taxonomy.ItemFields is { Count: > 0 } itemFields)
        {
            for (var i = 0; i < itemFields.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(itemFields[i]))
                {
                    throw new ConfigException($"taxonomy.itemFields[{i}] must be a non-empty string.");
                }
            }
        }

        ValidateTaxonomyKind("taxonomy.templates.tags", config.Taxonomy.Templates.Tags);
        ValidateTaxonomyKind("taxonomy.templates.categories", config.Taxonomy.Templates.Categories);

        if (config.Taxonomy.Kinds is { Count: > 0 } kinds)
        {
            for (var i = 0; i < kinds.Count; i++)
            {
                ValidateTaxonomyKindConfig($"taxonomy.kinds[{i}]", kinds[i]);
            }
        }
    }

    /// <summary>
    /// Optional theme.yaml validation. Returns a list of warnings (never throws).
    /// Returns null if no theme.yaml is found (not an error).
    /// </summary>
    public static List<string>? ValidateThemeYaml(string themeRoot)
    {
        var yamlPath = Path.Combine(themeRoot, "theme.yaml");
        if (!File.Exists(yamlPath))
            return null;

        var warnings = new List<string>();
        try
        {
            var text = File.ReadAllText(yamlPath);
            var stream = new YamlStream();
            stream.Load(new StringReader(text));

            if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root)
            {
                warnings.Add("theme.yaml is empty or not a valid mapping.");
                return warnings;
            }

            GetStringValue(root, "name", out var name);
            if (string.IsNullOrWhiteSpace(name))
                warnings.Add("theme.yaml: 'name' is missing or empty.");

            GetStringValue(root, "version", out var version);
            if (!string.IsNullOrWhiteSpace(version) && !System.Version.TryParse(version, out _))
                warnings.Add($"theme.yaml: 'version' '{version}' is not valid semver.");

            GetStringValue(root, "requires_bukit", out var requires);
            if (!string.IsNullOrWhiteSpace(requires) &&
                !requires.StartsWith(">=", StringComparison.Ordinal) &&
                !requires.StartsWith("^", StringComparison.Ordinal) &&
                !requires.StartsWith("~", StringComparison.Ordinal))
                warnings.Add($"theme.yaml: 'requires_bukit' '{requires}' should use semver range like '>=2.0.0'.");

            if (root.Children.TryGetValue(new YamlScalarNode("tags"), out var tagsNode) &&
                tagsNode is YamlSequenceNode tagsSeq &&
                tagsSeq.Children.Count == 0)
                warnings.Add("theme.yaml: 'tags' is an empty list.");
        }
        catch (Exception ex)
        {
            warnings.Add($"theme.yaml parse error: {ex.Message}");
        }

        return warnings;
    }

    private static void ValidateTaxonomyKind(string prefix, TaxonomyKindTemplateConfig kind)
    {
        if (kind.Template is not null && string.IsNullOrWhiteSpace(kind.Template))
        {
            throw new ConfigException($"{prefix}.template must be a non-empty string when set.");
        }

        if (kind.IndexTemplate is not null && string.IsNullOrWhiteSpace(kind.IndexTemplate))
        {
            throw new ConfigException($"{prefix}.indexTemplate must be a non-empty string when set.");
        }

        if (kind.TermTemplate is not null && string.IsNullOrWhiteSpace(kind.TermTemplate))
        {
            throw new ConfigException($"{prefix}.termTemplate must be a non-empty string when set.");
        }
    }

    private static void ValidateCollections(IReadOnlyDictionary<string, CollectionConfig> collections)
    {
        foreach (var (name, collection) in collections)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ConfigException("site.collections keys must be non-empty strings.");
            }

            if (string.IsNullOrWhiteSpace(collection.Permalink))
            {
                throw new ConfigException($"site.collections.{name}.permalink is required.");
            }

            if (!collection.Permalink.Contains("{slug}", StringComparison.OrdinalIgnoreCase))
            {
                throw new ConfigException($"site.collections.{name}.permalink must include {{slug}}.");
            }

            if (string.IsNullOrWhiteSpace(collection.Template))
            {
                throw new ConfigException($"site.collections.{name}.template is required.");
            }

            if (collection.Pagination.PageSize <= 0)
            {
                throw new ConfigException($"site.collections.{name}.pagination.pageSize must be a positive integer.");
            }

            if (!string.IsNullOrWhiteSpace(collection.ListRoute))
            {
                if (!collection.ListRoute.StartsWith('/'))
                {
                    throw new ConfigException($"site.collections.{name}.listRoute must start with '/'.");
                }
            }

            if (collection.FilteredLists is { Count: > 0 } filtered)
            {
                ValidateFilteredLists(name, filtered);
            }
        }
    }

    private static void ValidateFilteredLists(string collectionName, IReadOnlyList<FilteredListConfig> filteredLists)
    {
        var usedRoutes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < filteredLists.Count; i++)
        {
            var filter = filteredLists[i];
            var prefix = $"site.collections.{collectionName}.filteredLists[{i}]";

            if (string.IsNullOrWhiteSpace(filter.Field))
            {
                throw new ConfigException($"{prefix}.field is required.");
            }

            if (string.IsNullOrWhiteSpace(filter.Value))
            {
                throw new ConfigException($"{prefix}.value is required.");
            }

            if (string.IsNullOrWhiteSpace(filter.ListRoute))
            {
                throw new ConfigException($"{prefix}.listRoute is required.");
            }

            if (!filter.ListRoute.StartsWith('/'))
            {
                throw new ConfigException($"{prefix}.listRoute must start with '/'.");
            }

            if (!usedRoutes.Add(filter.ListRoute.Trim().ToLowerInvariant()))
            {
                throw new ConfigException($"{prefix}.listRoute '{filter.ListRoute}' duplicates another filtered list route.");
            }
        }
    }
    private static void ValidateSourcesToCollections(
        IReadOnlyList<ContentSourceConfig> sources,
        IReadOnlyDictionary<string, CollectionConfig> collections)
    {
        var collectionKeys = new HashSet<string>(collections.Keys, StringComparer.OrdinalIgnoreCase);
        var contentSources = new List<ContentSourceConfig>();
        foreach (var source in sources)
        {
            if ((source.Mode ?? "content").Trim().Equals("content", StringComparison.OrdinalIgnoreCase))
            {
                contentSources.Add(source);
            }
        }

        if (contentSources.Count == 0)
        {
            return;
        }

        var sourcesWithCollection = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sourcesWithoutCollection = new List<int>();
        for (var i = 0; i < contentSources.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(contentSources[i].Collection))
            {
                sourcesWithCollection.Add(contentSources[i].Collection!.Trim());
            }
            else
            {
                sourcesWithoutCollection.Add(i);
            }
        }

        if (sourcesWithoutCollection.Count == 0)
        {
            return;
        }

        var unreferencedCollections = collectionKeys.Except(sourcesWithCollection).ToList();

        if (unreferencedCollections.Count > 0)
        {
            if (sourcesWithoutCollection.Count == contentSources.Count)
            {
                throw new ConfigException(
                    "content.sources: no content source has a 'collection' field, but site.collections defines: " +
                    string.Join(", ", collectionKeys) +
                    ". Without collection assignment, content items cannot match their collection rules. " +
                    "Add 'collection: <name>' to each content source (e.g. collection: post).");
            }

            throw new ConfigException(
                "content.sources: the following site.collections have no matching content source with a 'collection' field: " +
                string.Join(", ", unreferencedCollections) +
                ". Assign them via 'collection: <name>' on each content source.");
        }
    }

    private static void ValidateExternalPlugins(IReadOnlyDictionary<string, ExternalPluginConfig> plugins)
    {
        foreach (var (name, plugin) in plugins)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ConfigException("site.externalPlugins keys must be non-empty strings.");
            }

            if (string.IsNullOrWhiteSpace(plugin.Runtime))
            {
                throw new ConfigException($"site.externalPlugins.{name}.runtime is required.");
            }

            var runtime = plugin.Runtime.Trim().ToLowerInvariant();
            if (runtime != "process" && runtime != "wasm")
            {
                throw new ConfigException($"site.externalPlugins.{name}.runtime must be process or wasm.");
            }

            if (string.IsNullOrWhiteSpace(plugin.Entry))
            {
                throw new ConfigException($"site.externalPlugins.{name}.entry is required.");
            }

            if (plugin.Hooks is null || plugin.Hooks.Count == 0)
            {
                throw new ConfigException($"site.externalPlugins.{name}.hooks must contain at least one hook.");
            }

            for (var i = 0; i < plugin.Hooks.Count; i++)
            {
                var hook = plugin.Hooks[i]?.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(hook))
                {
                    throw new ConfigException($"site.externalPlugins.{name}.hooks[{i}] must be a non-empty string.");
                }

                if (hook != "after-build" && hook != "derive-pages")
                {
                    throw new ConfigException($"site.externalPlugins.{name}.hooks[{i}] must be after-build or derive-pages.");
                }
            }

            if (plugin.TimeoutMs <= 0)
            {
                throw new ConfigException($"site.externalPlugins.{name}.timeoutMs must be a positive integer.");
            }

            if (runtime == "process")
            {
                ValidateProcessPluginOptions(name, plugin.Options);
            }

            if (runtime == "wasm")
            {
                if (!string.Equals(plugin.WasmProfile?.Trim(), "wasi-preview1", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ConfigException($"site.externalPlugins.{name}.wasmProfile must be wasi-preview1.");
                }

                if (plugin.MaxMemoryMb <= 0)
                {
                    throw new ConfigException($"site.externalPlugins.{name}.maxMemoryMb must be a positive integer.");
                }

                if (plugin.MaxMemoryMb > 512)
                {
                    throw new ConfigException($"site.externalPlugins.{name}.maxMemoryMb must be <= 512.");
                }

                var wasmFsMode = (plugin.WasmFsMode ?? "output-only").Trim().ToLowerInvariant();
                if (wasmFsMode is not ("none" or "output-only"))
                {
                    throw new ConfigException($"site.externalPlugins.{name}.wasmFsMode must be none|output-only.");
                }

                if (plugin.WasmAllowNetwork)
                {
                    throw new ConfigException($"site.externalPlugins.{name}.wasmAllowNetwork must be false in current sandbox policy.");
                }

                if (plugin.Capabilities is not null)
                {
                    for (var i = 0; i < plugin.Capabilities.Count; i++)
                    {
                        var capability = plugin.Capabilities[i]?.Trim().ToLowerInvariant();
                        if (string.IsNullOrWhiteSpace(capability))
                        {
                            throw new ConfigException($"site.externalPlugins.{name}.capabilities[{i}] must be a non-empty string.");
                        }

                        if (capability != "emit-outputs")
                        {
                            throw new ConfigException($"site.externalPlugins.{name}.capabilities[{i}] must be emit-outputs.");
                        }
                    }
                }
            }
        }
    }

    private static void ValidateProcessPluginOptions(string pluginName, IReadOnlyDictionary<string, object>? options)
    {
        if (options is null)
        {
            return;
        }

        if (options.ContainsKey("arguments"))
        {
            throw new ConfigException($"site.externalPlugins.{pluginName}.options.arguments is not allowed. Use options.processArgs.");
        }

        if (!options.TryGetValue("processArgs", out var processArgsObj) || processArgsObj is null)
        {
            return;
        }

        var processArgs = AsObjectMap(processArgsObj);
        if (processArgs is null)
        {
            throw new ConfigException($"site.externalPlugins.{pluginName}.options.processArgs must be a mapping.");
        }

        if (processArgs.TryGetValue("positionals", out var positionalsObj) && positionalsObj is not null)
        {
            if (positionalsObj is string || positionalsObj is not IEnumerable<object> positionals)
            {
                throw new ConfigException($"site.externalPlugins.{pluginName}.options.processArgs.positionals must be a sequence.");
            }

            var index = 0;
            foreach (var positional in positionals)
            {
                if (positional is null)
                {
                    throw new ConfigException($"site.externalPlugins.{pluginName}.options.processArgs.positionals[{index}] must be non-null.");
                }

                index++;
            }
        }

        if (processArgs.TryGetValue("named", out var namedObj) && namedObj is not null)
        {
            var named = AsObjectMap(namedObj);
            if (named is null)
            {
                throw new ConfigException($"site.externalPlugins.{pluginName}.options.processArgs.named must be a mapping.");
            }

            foreach (var key in named.Keys)
            {
                if (string.IsNullOrWhiteSpace(key) || !Regex.IsMatch(key, "^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$"))
                {
                    throw new ConfigException($"site.externalPlugins.{pluginName}.options.processArgs.named contains illegal key: {key}");
                }
            }
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

    private static IReadOnlyDictionary<string, object>? AsObjectMap(object value)
    {
        if (value is IReadOnlyDictionary<string, object> readOnlyMap)
        {
            return readOnlyMap;
        }

        if (value is IDictionary<string, object> map)
        {
            return new Dictionary<string, object>(map, StringComparer.OrdinalIgnoreCase);
        }

        return null;
    }

    private static void ValidateExternalAssemblyAllowlist(SiteConfig site, string trustMode)
    {
        var allowlist = site.ExternalAssemblyAllowlist;
        if (trustMode == "strict" && (allowlist is null || allowlist.Count == 0))
        {
            throw new ConfigException("site.externalAssemblyAllowlist is required when site.externalAssemblyTrustMode is strict.");
        }

        if (allowlist is null)
        {
            return;
        }

        foreach (var (fileName, hash) in allowlist)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ConfigException("site.externalAssemblyAllowlist keys must be non-empty file names.");
            }

            var normalized = fileName.Trim();
            if (normalized.Contains('/') || normalized.Contains('\\'))
            {
                throw new ConfigException($"site.externalAssemblyAllowlist key must be file name only: {fileName}");
            }

            if (string.IsNullOrWhiteSpace(hash) || !Regex.IsMatch(hash.Trim(), "^[A-Fa-f0-9]{64}$"))
            {
                throw new ConfigException($"site.externalAssemblyAllowlist.{normalized} must be a 64-char SHA256 hex string.");
            }
        }
    }

    private static void ValidateTaxonomyKindConfig(string prefix, TaxonomyKindConfig kind)
    {
        if (string.IsNullOrWhiteSpace(kind.Key))
        {
            throw new ConfigException($"{prefix}.key is required.");
        }

        if (kind.Kind is not null && string.IsNullOrWhiteSpace(kind.Kind))
        {
            throw new ConfigException($"{prefix}.kind must be a non-empty string when set.");
        }

        if (kind.Title is not null && string.IsNullOrWhiteSpace(kind.Title))
        {
            throw new ConfigException($"{prefix}.title must be a non-empty string when set.");
        }

        if (kind.SingularTitlePrefix is not null && string.IsNullOrWhiteSpace(kind.SingularTitlePrefix))
        {
            throw new ConfigException($"{prefix}.singularTitlePrefix must be a non-empty string when set.");
        }

        if (kind.Template is not null && string.IsNullOrWhiteSpace(kind.Template))
        {
            throw new ConfigException($"{prefix}.template must be a non-empty string when set.");
        }

        if (kind.IndexTemplate is not null && string.IsNullOrWhiteSpace(kind.IndexTemplate))
        {
            throw new ConfigException($"{prefix}.indexTemplate must be a non-empty string when set.");
        }

        if (kind.TermTemplate is not null && string.IsNullOrWhiteSpace(kind.TermTemplate))
        {
            throw new ConfigException($"{prefix}.termTemplate must be a non-empty string when set.");
        }
    }

    private static void ValidateNotion(NotionConfig notion)
    {
        if (string.IsNullOrWhiteSpace(notion.DatabaseId))
        {
            throw new ConfigException("content.notion.databaseId is required.");
        }

        if (notion.PageSize is < 1 or > 100)
        {
            throw new ConfigException("content.notion.pageSize must be between 1 and 100.");
        }

        if (notion.MaxItems is not null && notion.MaxItems.Value <= 0)
        {
            throw new ConfigException("content.notion.maxItems must be a positive integer when set.");
        }

        if (notion.RenderConcurrency is not null && notion.RenderConcurrency.Value <= 0)
        {
            throw new ConfigException("content.notion.renderConcurrency must be a positive integer when set.");
        }

        if (notion.MaxRps is not null && notion.MaxRps.Value <= 0)
        {
            throw new ConfigException("content.notion.maxRps must be a positive integer when set.");
        }

        if (notion.MaxRetries is not null && notion.MaxRetries.Value < 0)
        {
            throw new ConfigException("content.notion.maxRetries must be a non-negative integer when set.");
        }

        var mode = (notion.FieldPolicy.Mode ?? "whitelist").Trim().ToLowerInvariant();
        if (mode is not ("whitelist" or "all"))
        {
            throw new ConfigException("content.notion.fieldPolicy.mode must be whitelist|all.");
        }

        var filterType = (notion.FilterType ?? "checkbox_true").Trim().ToLowerInvariant();
        if (filterType is not ("checkbox_true" or "checkbox_false" or "select_equals" or "status_equals" or "rich_text_equals" or "none"))
        {
            throw new ConfigException("content.notion.filterType must be checkbox_true|checkbox_false|select_equals|status_equals|rich_text_equals|none.");
        }

        if (filterType != "none" && string.IsNullOrWhiteSpace(notion.FilterProperty))
        {
            throw new ConfigException("content.notion.filterProperty is required when filterType is not none.");
        }

        if (filterType is "select_equals" or "status_equals" or "rich_text_equals" &&
            string.IsNullOrWhiteSpace(notion.FilterValue))
        {
            throw new ConfigException("content.notion.filterValue is required for select_equals|status_equals|rich_text_equals filters.");
        }

        if (!string.IsNullOrWhiteSpace(notion.SortProperty))
        {
            var dir = (notion.SortDirection ?? "ascending").Trim().ToLowerInvariant();
            if (dir is not ("ascending" or "descending"))
            {
                throw new ConfigException("content.notion.sortDirection must be ascending|descending.");
            }
        }

        if (notion.IncludeSlugs is { Count: > 0 })
        {
            if (string.IsNullOrWhiteSpace(notion.IncludeSlugProperty))
            {
                throw new ConfigException("content.notion.includeSlugProperty is required when includeSlugs is set.");
            }
        }

        var cacheMode = (notion.CacheMode ?? "off").Trim().ToLowerInvariant();
        if (cacheMode is not ("off" or "readwrite" or "readonly"))
        {
            throw new ConfigException("content.notion.cacheMode must be off|readwrite|readonly.");
        }

        if (notion.CacheDir is not null && string.IsNullOrWhiteSpace(notion.CacheDir))
        {
            throw new ConfigException("content.notion.cacheDir must be a non-empty string when set.");
        }

        var token = EnvironmentHelper.GetNotionToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ConfigException("NOTION_TOKEN is required for notion provider and must come from environment variables.");
        }
    }

    private static void ValidateMedia(MediaConfig media)
    {
        if (string.IsNullOrWhiteSpace(media.DownloadDir))
        {
            throw new ConfigException("content.media.downloadDir must be a non-empty string.");
        }

        RejectPathTraversal("content.media.downloadDir", media.DownloadDir);

        if (string.IsNullOrWhiteSpace(media.UrlBase))
        {
            throw new ConfigException("content.media.urlBase must be a non-empty string.");
        }

        if (string.IsNullOrWhiteSpace(media.DefaultImageUrl))
        {
            throw new ConfigException("content.media.defaultImageUrl must be a non-empty string.");
        }

        if (media.FieldKeys is null)
        {
            throw new ConfigException("content.media.fieldKeys is required.");
        }

        if (media.MaxConcurrency is <= 0)
        {
            throw new ConfigException("content.media.maxConcurrency must be a positive integer when set.");
        }

        if (media.MaxRetries is < 0)
        {
            throw new ConfigException("content.media.maxRetries must be a non-negative integer when set.");
        }

        if (media.TimeoutMs is <= 0)
        {
            throw new ConfigException("content.media.timeoutMs must be a positive integer when set.");
        }

        if (media.MaxFileSizeBytes is <= 0)
        {
            throw new ConfigException("content.media.maxFileSizeBytes must be a positive integer when set.");
        }

        if (media.RetryBaseDelayMs is < 0)
        {
            throw new ConfigException("content.media.retryBaseDelayMs must be a non-negative integer when set.");
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

    private static void RejectPathTraversal(string fieldName, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;

        if (Path.IsPathRooted(value))
        {
            throw new ConfigException($"{fieldName} must be a relative path.");
        }

        var normalized = value.Replace('\\', '/');
        var segments = normalized.Split('/');
        foreach (var segment in segments)
        {
            if (segment == "..")
            {
                throw new ConfigException($"{fieldName} must not contain '..' path traversal segments.");
            }
        }
    }

    private static void ValidateMarkdown(MarkdownConfig markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown.Dir))
        {
            throw new ConfigException("content.markdown.dir is required.");
        }

        RejectPathTraversal("content.markdown.dir", markdown.Dir);

        if (markdown.MaxItems is not null && markdown.MaxItems.Value <= 0)
        {
            throw new ConfigException("content.markdown.maxItems must be a positive integer when set.");
        }

        if (markdown.IncludePaths is { Count: > 0 } includePaths)
        {
            for (var i = 0; i < includePaths.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(includePaths[i]))
                {
                    throw new ConfigException($"content.markdown.includePaths[{i}] must be a non-empty string.");
                }

                RejectPathTraversal($"content.markdown.includePaths[{i}]", includePaths[i]);
            }
        }

        if (markdown.IncludeGlobs is { Count: > 0 } includeGlobs)
        {
            for (var i = 0; i < includeGlobs.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(includeGlobs[i]))
                {
                    throw new ConfigException($"content.markdown.includeGlobs[{i}] must be a non-empty string.");
                }
            }
        }
    }

    private static void ValidateDeployConfig(DeployConfig deploy)
    {
        if (!string.IsNullOrWhiteSpace(deploy.Provider))
        {
            var provider = deploy.Provider.Trim().ToLowerInvariant();
            if (provider is not ("github-pages"))
            {
                throw new ConfigException($"deploy.provider must be github-pages (got: {deploy.Provider}).");
            }
        }

        if (!string.IsNullOrWhiteSpace(deploy.Branch) && deploy.Branch.Contains('/'))
        {
            throw new ConfigException("deploy.branch must not contain '/'.");
        }

        if (!string.IsNullOrWhiteSpace(deploy.Message) && deploy.Message.Length > 4096)
        {
            throw new ConfigException("deploy.message must be <= 4096 characters.");
        }

        if (!string.IsNullOrWhiteSpace(deploy.Cname))
        {
            var cname = deploy.Cname.Trim();
            if (!IsValidDomain(cname))
            {
                throw new ConfigException($"deploy.cname '{cname}' is not a valid domain name.");
            }
        }
    }

    private static bool IsValidDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return false;
        }

        if (domain.Length > 253)
        {
            return false;
        }

        return Regex.IsMatch(domain, @"^[A-Za-z0-9]([A-Za-z0-9\-]{0,61}[A-Za-z0-9])?(\.[A-Za-z0-9]([A-Za-z0-9\-]{0,61}[A-Za-z0-9])?)*$", RegexOptions.CultureInvariant);
    }
}
