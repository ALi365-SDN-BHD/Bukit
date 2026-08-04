using Bukit.Shared;
using YamlDotNet.RepresentationModel;

namespace Bukit.Config;

public static class ConfigLoader
{
    public static AppConfig Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ConfigException("Config path is required.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        if (!File.Exists(path))
        {
            throw new ConfigException($"Config file not found: {path}", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        using var reader = File.OpenText(path);
        var yaml = new YamlStream();
        try
        {
            yaml.Load(reader);
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            throw new ConfigException($"Invalid YAML syntax in config file: {path}", ex, DiagnosticCode.ConfigYamlSyntaxError);
        }

        if (yaml.Documents.Count == 0)
        {
            throw new ConfigException("Config file is empty.", DiagnosticCode.ConfigYamlSyntaxError);
        }

        if (yaml.Documents[0].RootNode is not YamlMappingNode root)
        {
            throw new ConfigException("Config root must be a mapping.", DiagnosticCode.ConfigYamlSyntaxError);
        }

        ConfigEnvironmentOverrides.Apply(root);
        ConfigStrictFieldValidator.Validate(root);

        var siteNode = ConfigYamlHelpers.GetMapping(root, "site");
        var contentNode = ConfigYamlHelpers.GetMapping(root, "content");
        var buildNode = ConfigYamlHelpers.GetOptionalMapping(root, "build");
        var themeNode = ConfigYamlHelpers.GetOptionalMapping(root, "theme");
        var taxonomyNode = ConfigYamlHelpers.GetOptionalMapping(root, "taxonomy");
        var loggingNode = ConfigYamlHelpers.GetOptionalMapping(root, "logging");

        var collections = ConfigCollectionReader.ReadCollections(siteNode);
        var site = new SiteConfig
        {
            Name = ConfigYamlHelpers.GetRequiredString(siteNode, "name"),
            Title = ConfigYamlHelpers.GetRequiredString(siteNode, "title"),
            Url = ConfigYamlHelpers.GetOptionalString(siteNode, "url"),
            Description = ConfigYamlHelpers.GetOptionalString(siteNode, "description"),
            Seo = SiteDefaultsApplier.ReadSeoConfig(siteNode),
            Analytics = SiteDefaultsApplier.ReadAnalyticsConfig(siteNode),
            AutoSummary = ConfigYamlHelpers.GetOptionalBool(siteNode, "autoSummary") ?? false,
            AutoSummaryMaxLength = ConfigYamlHelpers.GetOptionalInt(siteNode, "autoSummaryMaxLength") ?? 200,
            BaseUrl = ConfigYamlHelpers.GetOptionalString(siteNode, "baseUrl") ?? "/",
            OutputPathEncoding = ConfigYamlHelpers.GetOptionalString(siteNode, "outputPathEncoding") ?? "none",
            Language = ConfigYamlHelpers.GetOptionalString(siteNode, "language") ?? "zh-CN",
            Languages = ConfigYamlHelpers.ReadStringList(siteNode, "languages"),
            DefaultLanguage = ConfigYamlHelpers.GetOptionalString(siteNode, "defaultLanguage"),
            SitemapMode = ConfigYamlHelpers.GetOptionalString(siteNode, "sitemapMode") ?? "split",
            SearchIncludeDerived = ConfigYamlHelpers.GetOptionalBool(siteNode, "searchIncludeDerived") ?? false,
            PluginFailMode = ConfigYamlHelpers.GetOptionalString(siteNode, "pluginFailMode") ?? "strict",
            DeriveConflictPolicy = ConfigYamlHelpers.GetOptionalString(siteNode, "deriveConflictPolicy") ?? "fail",
            Timezone = ConfigYamlHelpers.GetOptionalString(siteNode, "timezone") ?? "Asia/Shanghai",
            Permalinks = ConfigYamlHelpers.ReadStringMap(siteNode, "permalinks"),
            Collections = collections,
            Plugins = SiteDefaultsApplier.ReadPluginToggles(siteNode),
            Feed = SiteDefaultsApplier.ReadFeedConfig(siteNode),
            SitemapDetail = SiteDefaultsApplier.ReadSitemapDetailConfig(siteNode),
            Pagination = SiteDefaultsApplier.ReadPaginationConfig(siteNode),
            Search = SiteDefaultsApplier.ReadSearchConfig(siteNode),
            Related = SiteDefaultsApplier.ReadRelatedConfig(siteNode),
            Menus = SiteDefaultsApplier.ReadMenus(siteNode)
        };

        var sources = ConfigCollectionReader.ReadSources(contentNode);

        if (sources is null || sources.Count == 0)
        {
            throw new ConfigException(
                "content.sources is required in Bukit 1.0. Define at least one content source. Example:\n" +
                "content:\n" +
                "  sources:\n" +
                "    - type: markdown\n" +
                "      markdown:\n" +
                "        dir: content",
                DiagnosticCode.ConfigRequiredFieldMissing);
        }

        var content = ContentConfigFactory.FromSources(
            sources,
            SiteDefaultsApplier.ReadMediaConfigFrom(contentNode),
            ConfigCollectionReader.ReadContentModelSchema(contentNode),
            ConfigCollectionReader.ReadRouteMetadata(contentNode));

        var buildReportNode = buildNode is null ? null : ConfigYamlHelpers.GetOptionalMapping(buildNode, "report");
        var build = new BuildConfig
        {
            Output = buildNode is null ? "dist" : ConfigYamlHelpers.GetOptionalString(buildNode, "output") ?? "dist",
            Clean = buildNode is null ? true : ConfigYamlHelpers.GetOptionalBoolStrict(buildNode, "clean", "build") ?? true,
            Draft = buildNode is null ? false : ConfigYamlHelpers.GetOptionalBool(buildNode, "draft") ?? false,
            ListPageContentMode = buildNode is null ? "auto" : ConfigYamlHelpers.GetOptionalString(buildNode, "listPageContentMode") ?? "auto",
            SchemaFailMode = buildNode is null ? "warn" : ConfigYamlHelpers.GetOptionalString(buildNode, "schemaFailMode") ?? "warn",
            FingerprintMode = buildNode is null ? "size-time" : ConfigYamlHelpers.GetOptionalString(buildNode, "fingerprintMode") ?? "size-time",
            PublishDotFiles = buildNode is null ? false : ConfigYamlHelpers.GetOptionalBool(buildNode, "publishDotFiles") ?? false,
            FollowSymlinks = buildNode is null ? false : ConfigYamlHelpers.GetOptionalBool(buildNode, "followSymlinks") ?? false,
            LanguageJobs = buildNode is null ? 1 : ConfigYamlHelpers.GetOptionalIntStrict(buildNode, "languageJobs") ?? 1,
            Report = new BuildReportConfig
            {
                Enabled = buildReportNode is null || (ConfigYamlHelpers.GetOptionalBool(buildReportNode, "enabled") ?? true),
                SecurityFailMode = buildReportNode is null ? "auto" : ConfigYamlHelpers.GetOptionalString(buildReportNode, "securityFailMode") ?? "auto"
            }
        };

        if (build.FollowSymlinks)
        {
            Console.Error.WriteLine("[warn] build.followSymlinks is enabled. Symlinks may point outside the project directory. Ensure all symlinks are trusted.");
        }

        var theme = new ThemeConfig
        {
            Name = themeNode is null ? null : ConfigYamlHelpers.GetOptionalString(themeNode, "name"),
            Layouts = themeNode is null ? "layouts" : ConfigYamlHelpers.GetOptionalString(themeNode, "layouts") ?? "layouts",
            Assets = themeNode is null ? "assets" : ConfigYamlHelpers.GetOptionalString(themeNode, "assets") ?? "assets",
            Static = themeNode is null ? "static" : ConfigYamlHelpers.GetOptionalString(themeNode, "static") ?? "static",
            StaticTemplate = themeNode is null ? null : ConfigYamlHelpers.GetOptionalString(themeNode, "staticTemplate"),
            Params = SiteDefaultsApplier.ReadThemeParams(themeNode),
            Shortcodes = ConfigYamlHelpers.ReadStringMap(themeNode, "shortcodes"),
            Components = SiteDefaultsApplier.ReadComponents(themeNode),
            Scss = SiteDefaultsApplier.ReadScssConfig(themeNode),
            Images = SiteDefaultsApplier.ReadImageOptimizationConfig(themeNode),
            ComponentValidation = themeNode is null ? "off" : ConfigYamlHelpers.GetOptionalString(themeNode, "componentValidation") ?? "off"
        };

        var taxonomy = SiteDefaultsApplier.ReadTaxonomyConfig(taxonomyNode);

        var logging = new LoggingConfig
        {
            Level = loggingNode is null ? "info" : ConfigYamlHelpers.GetOptionalString(loggingNode, "level") ?? "info"
        };

        var deployNode = ConfigYamlHelpers.GetOptionalMapping(root, "deploy");
        var deploy = SiteDefaultsApplier.ReadDeployConfig(deployNode);

        return new AppConfig
        {
            Site = site,
            Content = content,
            Build = build,
            Theme = theme,
            Taxonomy = taxonomy,
            Logging = logging,
            Deploy = deploy
        };
    }

}
