using Bukit.Cli.Shared;
using System.Text.Json;
using Scriban;
using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;

namespace Bukit.Cli.Commands;

public static class DoctorCommand
{
    internal sealed record DoctorContext(
        string RootDir,
        AppConfig Config,
        string LayoutsDir,
        string[] AllHtmlFiles);
    public static async Task<int> RunAsync(CliBoundCommand command)
    {
        var resolved = ConfigPathResolver.Resolve(command.GetString("--config"), command.GetString("--site"));
        var rootDir = resolved.RootDir;
        var config = ConfigLoader.Load(resolved.FullConfigPath);

        var siteUrl = command.GetString("--site-url");
        if (!string.IsNullOrWhiteSpace(siteUrl))
        {
            config = config with { Site = config.Site with { Url = siteUrl } };
        }

        try
        {
            ConfigValidator.Validate(config);
            Console.WriteLine("✔ Config loaded");
        }
        catch (ConfigException ex)
        {
            Console.WriteLine("✖ Config error");
            Console.WriteLine(Bukit.Shared.DiagnosticExceptionFormatter.Format(ex));
            return 1;
        }

        if (!CheckOutputDirectorySafety(config, rootDir))
        {
            return 1;
        }

        CheckFollowSymlinksSafety(config);

        var (layoutsDir, assetsDir, staticDir, _, _, _, _) = Bukit.Engine.BuildPathUtils.ResolveThemeDirectories(rootDir, config.Theme);
        if (!Directory.Exists(layoutsDir))
        {
            Console.WriteLine($"✖ Layouts dir not found: {layoutsDir}");
            return 1;
        }

        ThemeBootstrapResult bootstrap;
        try
        {
            bootstrap = ThemeBootstrapper.BootstrapRequired(config, rootDir, new ConsoleLogger(LogLevel.Warn));
        }
        catch (ConfigException ex)
        {
            Console.WriteLine("✖ Theme manifest invalid");
            Console.WriteLine(Bukit.Shared.DiagnosticExceptionFormatter.Format(ex));
            return 1;
        }

        var templateResolver = new ThemeTemplateResolver(bootstrap.Manifest);
        IReadOnlyList<string> requiredTemplates;
        try
        {
            requiredTemplates = templateResolver.GetRequiredTemplatePaths();
        }
        catch (ConfigException ex)
        {
            Console.WriteLine("✖ Theme template config error");
            Console.WriteLine(ex.Message);
            return 1;
        }

        var missing = requiredTemplates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(t => Path.Combine(layoutsDir, t.Replace('/', Path.DirectorySeparatorChar)))
            .Where(p => !File.Exists(p))
            .ToList();
        if (missing.Count > 0)
        {
            Console.WriteLine("✖ Missing templates:");
            foreach (var p in missing)
            {
                Console.WriteLine($"  - {DoctorPathHelpers.ToRelativeTemplatePath(layoutsDir, p)}");
            }

            return 1;
        }

        Console.WriteLine("✔ Required theme templates present");

        var allHtmlFiles = Directory.GetFiles(layoutsDir, "*.html", SearchOption.AllDirectories);
        foreach (var p in allHtmlFiles)
        {
            var text = await File.ReadAllTextAsync(p);
            var template = Template.Parse(text, p);
            if (template.HasErrors)
            {
                Console.WriteLine($"✖ Template parse error: {DoctorPathHelpers.ToRelativeTemplatePath(layoutsDir, p)}");
                foreach (var m in template.Messages)
                {
                    Console.WriteLine($"  - {m}");
                }
                return 1;
            }
        }

        Console.WriteLine("✔ Templates parse");

        var scribanFiles = Directory.GetFiles(layoutsDir, "*.scriban", SearchOption.AllDirectories);
        foreach (var p in scribanFiles)
        {
            var text = await File.ReadAllTextAsync(p);
            var template = Template.Parse(text, p);
            if (template.HasErrors)
            {
                Console.WriteLine($"✖ Template parse error: {DoctorPathHelpers.ToRelativeTemplatePath(layoutsDir, p)}");
                foreach (var m in template.Messages)
                {
                    Console.WriteLine($"  - {m}");
                }
                return 1;
            }
        }

        if (scribanFiles.Length > 0)
        {
            Console.WriteLine($"✔ Scriban templates parse ({scribanFiles.Length} files)");
        }

        var allTemplates = Directory.GetFiles(layoutsDir, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".scriban", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        foreach (var p in allTemplates)
        {
            var text = await File.ReadAllTextAsync(p);
            var openDouble = DoctorTemplateAnalyzer.CountOpenings(text, "{{");
            var closeDouble = DoctorTemplateAnalyzer.CountOpenings(text, "}}");
            if (openDouble != closeDouble)
            {
                var relative = DoctorPathHelpers.ToRelativeTemplatePath(layoutsDir, p);
                Console.WriteLine($"⚠ Unmatched {{{{/}}}} in {relative}: {openDouble} opens, {closeDouble} closes");
            }

            var openPercent = DoctorTemplateAnalyzer.CountOpenings(text, "{%");
            var closePercent = DoctorTemplateAnalyzer.CountOpenings(text, "%}");
            if (openPercent != closePercent)
            {
                var relative = DoctorPathHelpers.ToRelativeTemplatePath(layoutsDir, p);
                Console.WriteLine($"⚠ Unmatched {{%/ %}} in {relative}: {openPercent} opens, {closePercent} closes");
            }

            var openHash = DoctorTemplateAnalyzer.CountOpenings(text, "{#");
            var closeHash = DoctorTemplateAnalyzer.CountOpenings(text, "#}");
            if (openHash != closeHash)
            {
                var relative = DoctorPathHelpers.ToRelativeTemplatePath(layoutsDir, p);
                Console.WriteLine($"⚠ Unmatched {{#/#}} in {relative}: {openHash} opens, {closeHash} closes");
            }
        }

        try
        {
            Bukit.Engine.TemplateCapabilitiesResolver.ValidateManifest(layoutsDir);
            Console.WriteLine("✔ Template capabilities manifest valid");
        }
        catch (ConfigException ex)
        {
            Console.WriteLine("✖ Template capabilities manifest error");
            Console.WriteLine(ex.Message);
            return 1;
        }

        DoctorManifestChecker.CheckManifestCompleteness(layoutsDir, allHtmlFiles);

        Console.WriteLine();
        Console.WriteLine("--- Template chain analysis ---");
        DoctorTemplateAnalyzer.AnalyzeTemplateChains(layoutsDir, allHtmlFiles);

        Console.WriteLine();
        Console.WriteLine("--- Known-context template variable check ---");
        DoctorTemplateAnalyzer.CheckTemplateVariables(layoutsDir);

        Console.WriteLine();
        DoctorTemplateChecker.CheckIncludeExistence(new DoctorContext(rootDir, config, layoutsDir, allHtmlFiles));

        var ctx = new DoctorContext(rootDir, config, layoutsDir, allHtmlFiles);

        Console.WriteLine();
        DoctorTemplateChecker.CheckThemeParamsConsistency(ctx);

        var themeRoot = Path.Combine(rootDir, "themes", config.Theme.Name ?? "starter");
        if (Directory.Exists(themeRoot))
        {
            var yamlIssues = ConfigValidator.ValidateThemeYaml(themeRoot);
            if (yamlIssues.Count > 0)
            {
                Console.WriteLine("✖ theme.yaml issues (1.0 requires a valid theme.yaml manifest):");
                foreach (var issue in yamlIssues)
                    Console.WriteLine($"  - {issue}");

                return 1;
            }
        }

        var listPageContentMode = (config.Build.ListPageContentMode ?? "auto").Trim().ToLowerInvariant();
        if (listPageContentMode == "auto")
        {
            foreach (var template in requiredTemplates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                DoctorManifestChecker.WarnHeuristicFallback(layoutsDir, template);
            }
        }

        Console.WriteLine();
        DoctorMarkdownChecker.CheckMarkdownFrontMatter(ctx);
        DoctorMarkdownChecker.CheckMarkdownSyntax(ctx);
        DoctorMarkdownChecker.CheckMarkdownEmptyBody(ctx);

        Console.WriteLine();
        DoctorTemplateChecker.CheckHardcodedUrls(ctx);
        DoctorTemplateChecker.CheckHardcodedText(ctx);

        if (!Directory.Exists(assetsDir))
        {
            Console.WriteLine($"⚠ Assets dir not found: {assetsDir}");
        }
        else
        {
            Console.WriteLine("✔ Assets dir present");
        }

        if (!Directory.Exists(staticDir))
        {
            Console.WriteLine($"⚠ Static dir not found: {staticDir}");
        }
        else
        {
            Console.WriteLine("✔ Static dir present");
        }

        DoctorThemeChecker.CheckThemeAssetDirs(config, rootDir);
        DoctorThemeChecker.CheckThemeAssetContent(config, rootDir);

        var cacheDir = Path.Combine(rootDir, ".cache");
        if (Directory.Exists(cacheDir))
        {
            var manifests = Directory.GetFiles(cacheDir, "build-manifest*.json");
            foreach (var m in manifests)
            {
                try
                {
                    using var stream = File.OpenRead(m);
                    _ = await JsonDocument.ParseAsync(stream);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠ Invalid manifest json: {m} ({ex.Message})");
                }
            }
        }

        var pluginContext = new BuildContext
        {
            RootDir = rootDir,
            OutputDir = Path.Combine(rootDir, config.Build.Output),
            BaseUrl = config.Site.BaseUrl,
            LayoutsDir = layoutsDir,
            RoutedDocuments = Array.Empty<Bukit.Engine.Abstractions.Content.RoutedContentDocument>(),
            BodyStore = Bukit.Engine.Abstractions.Content.NullContentBodyStore.Instance,
            TemplateResolver = templateResolver.ResolveKindTemplate,
            Logger = new ConsoleLogger(LogLevel.Info)
        };

        var plugins = PluginRegistry.GetAllPlugins(pluginContext, config).Select(x => x.Plugin).ToList();
        Console.WriteLine($"✔ Plugins discovered: {plugins.Count}");

        var issues = 0;

        var discoveredPluginNames = plugins
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (config.Site.Plugins is { Count: > 0 })
        {
            foreach (var key in config.Site.Plugins.Keys)
            {
                if (!discoveredPluginNames.Contains(key))
                {
                    Console.WriteLine($"⚠ Unknown plugin '{key}' in site.plugins configuration");
                    issues++;
                }
            }
        }

        if (HasNotionSource(config.Content) && config.Content.Sources is not null)
        {
            var token = EnvironmentHelper.GetNotionToken();
            if (string.IsNullOrWhiteSpace(token))
            {
                Console.WriteLine("✖ NOTION_TOKEN not set");
                return 1;
            }

            var notionSource = config.Content.Sources.FirstOrDefault(s =>
                s.Type.Equals("notion", StringComparison.OrdinalIgnoreCase) && s.Notion is not null);
            if (notionSource?.Notion is null)
            {
                Console.WriteLine("✖ Notion source configuration not found");
                return 1;
            }

            var ok = await DoctorNotionChecker.CheckNotionAsync(token, notionSource.Notion.DatabaseId);
            if (!ok)
            {
                return 1;
            }

            await DoctorNotionChecker.CheckNotionConnectivityAsync(token);

            if (command.GetString("--notion-schema") is not null)
            {
                await DoctorNotionChecker.CheckNotionSchemaAsync(token, notionSource.Notion);
            }
        }
        else if (config.Content.Sources is { Count: > 0 })
        {
            var hasNotionSource = config.Content.Sources.Any(s =>
                s.Type.Equals("notion", StringComparison.OrdinalIgnoreCase) ||
                (s.Notion is not null));
            if (hasNotionSource)
            {
                var token = EnvironmentHelper.GetNotionToken();
                if (string.IsNullOrWhiteSpace(token))
                {
                    Console.WriteLine("⚠ Notion integration configured but NOTION_TOKEN environment variable is not set");
                    issues++;
                }
                else
                {
                    Console.WriteLine("✔ Notion integration configured. Token found.");
                    await DoctorNotionChecker.CheckNotionConnectivityAsync(token);
                }
            }
        }

        IReadOnlyList<Bukit.Engine.Abstractions.Content.RoutedContentDocument> routedDocuments;
        try
        {
            routedDocuments = await RouteInventoryValidator.BuildContentRoutesAsync(
                config,
                rootDir,
                isCi: false,
                new ConsoleLogger(LogLevel.Error),
                templateResolver);
            RouteInventoryValidator.ValidateContentRoutes(routedDocuments);
            Console.WriteLine("✔ Routes valid");
        }
        catch (Exception ex) when (ex is ConfigException or ContentException)
        {
            Console.WriteLine("✖ Route inventory error");
            Console.WriteLine(ex.Message);
            return 1;
        }

        IReadOnlyList<RouteInfo> listRoutes;
        IReadOnlyList<string> pluginRequirementTemplates;
        try
        {
            var routedPluginContext = new BuildContext
            {
                RootDir = rootDir,
                OutputDir = Path.Combine(rootDir, config.Build.Output),
                BaseUrl = config.Site.BaseUrl,
                LayoutsDir = layoutsDir,
                RoutedDocuments = routedDocuments,
                BodyStore = Bukit.Engine.Abstractions.Content.NullContentBodyStore.Instance,
                TemplateResolver = templateResolver.ResolveKindTemplate,
                Logger = new ConsoleLogger(LogLevel.Info)
            };
            listRoutes = SiteEngine.GetListRoutes(
                routedPluginContext,
                routedDocuments,
                config.Site.Collections,
                config.Site.OutputPathEncoding,
                templateResolver);
            pluginRequirementTemplates = DoctorTemplateAnalyzer.CollectPluginRequirementTemplates(routedPluginContext, config, templateResolver);
        }
        catch (ConfigException ex)
        {
            Console.WriteLine("✖ Plugin template requirement error");
            Console.WriteLine(ex.Message);
            return 1;
        }

        var missingUsedTemplates = DoctorTemplateAnalyzer.CollectMissingUsedTemplates(layoutsDir, routedDocuments, listRoutes, pluginRequirementTemplates);
        if (missingUsedTemplates.Count > 0)
        {
            Console.WriteLine("✖ Missing used templates:");
            foreach (var template in missingUsedTemplates)
            {
                Console.WriteLine($"  - {template}");
            }

            return 1;
        }

        DoctorManifestChecker.CheckUnreferencedTemplates(layoutsDir, allHtmlFiles, config, listRoutes, templateResolver);

        Console.WriteLine();
        var hasSchemaErrors = DoctorSchemaChecker.CheckSchemaFieldCompleteness(ctx, routedDocuments);
        DoctorSchemaChecker.CheckTemplateFieldsVsSchema(ctx);
        DoctorSchemaChecker.CheckExtraContentFields(ctx, routedDocuments);

        if (hasSchemaErrors)
        {
            return 1;
        }

        Console.WriteLine();
        try
        {
            var factory = new DefaultContentProviderFactory();
            var contentPipeline = new ContentPipeline(factory, new ConsoleLogger(LogLevel.Error));
            var contentResult = await contentPipeline.ExecuteAsync(config, rootDir, new ConfigOverrides(), Path.Combine(rootDir, ".cache", "media"));
            PrintDataModuleSummary(contentResult.Documents);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Data modules: (unavailable — {ex.Message})");
        }

        Console.WriteLine("✔ Doctor passed");
        return 0;
    }

    private static void PrintDataModuleSummary(IReadOnlyList<ContentDocument> documents)
    {
        if (documents.Count == 0)
        {
            Console.WriteLine("Data modules: (none)");
            return;
        }

        var byType = new Dictionary<string, List<ContentDocument>>(StringComparer.OrdinalIgnoreCase);
        foreach (var document in documents)
        {
            if (!ContentFieldReader.IsDataItem(document))
            {
                continue;
            }

            var type = ContentFieldReader.GetContentType(document, "module");
            if (!byType.ContainsKey(type))
            {
                byType[type] = new List<ContentDocument>();
            }

            byType[type].Add(document);
        }

        if (byType.Count == 0)
        {
            Console.WriteLine("Data modules: (none)");
            return;
        }

        Console.WriteLine("Data modules:");
        foreach (var (type, moduleItems) in byType.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            var source = ContentFieldReader.GetText(moduleItems.First().CustomFields, "sourceKey") ?? "unknown";
            var sourceMode = ContentFieldReader.GetText(moduleItems.First().CustomFields, "sourceMode") ?? "unknown";

            var languages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in moduleItems)
            {
                var language = ContentFieldReader.GetText(item, "language");
                if (!string.IsNullOrWhiteSpace(language))
                {
                    languages.Add(language);
                }
            }

            var languageText = languages.Count == 0 ? "-" : languages.Count == 1 ? languages.First() : "mixed";
            var fieldCount = moduleItems
                .Select(item => item.CustomFields?.Count ?? 0)
                .DefaultIfEmpty(0)
                .Max();

            var fields = moduleItems
                .SelectMany(item => item.CustomFields?.Keys ?? Array.Empty<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(field => field, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var fieldsText = fields.Length > 0 ? $"[{string.Join(", ", fields)}]" : "";

            Console.WriteLine($"  {type,-14} ×{moduleItems.Count}  source={source,-10}  mode={sourceMode,-8}  lang={languageText,-6}  fields={fieldCount}  {fieldsText}");
        }
    }

    private static void CheckFollowSymlinksSafety(AppConfig config)
    {
        if (config.Build.FollowSymlinks)
        {
            Console.WriteLine("⚠ build.followSymlinks is enabled. Ensure all symlinks are within the project directory and trusted. Consider disabling in CI environments.");
        }
    }

    private static bool CheckOutputDirectorySafety(AppConfig config, string rootDir)
    {
        var outputDir = Path.GetFullPath(Path.Combine(rootDir, config.Build.Output));
        var exists = Directory.Exists(outputDir);
        var markerExists = File.Exists(Path.Combine(outputDir, ".bukit-output-marker"));
        var cleanRequested = config.Build.Clean;
        var nonEmpty = exists && Directory.EnumerateFileSystemEntries(outputDir).Any();

        Console.WriteLine("Output directory safety:");
        Console.WriteLine($"  - output: {outputDir}");
        Console.WriteLine($"  - exists: {(exists ? "yes" : "no")}");
        Console.WriteLine($"  - marker exists: {(markerExists ? "yes" : "no")}");
        Console.WriteLine($"  - clean requested: {(cleanRequested ? "yes" : "no")}");

        if (!exists)
        {
            Console.WriteLine("  - result: ok (directory will be created)");
            return true;
        }
        else if (!nonEmpty)
        {
            Console.WriteLine("  - result: ok (directory is empty)");
            return true;
        }
        else if (!cleanRequested)
        {
            Console.WriteLine("  - result: ok (clean not requested, existing files will be overwritten)");
            return true;
        }
        else if (markerExists)
        {
            Console.WriteLine("  - result: ok (directory has Bukit marker, will be cleaned)");
            return true;
        }
        else
        {
            Console.WriteLine("  - result: refuse (no Bukit marker, clean would be blocked)");
            Console.WriteLine("  - fix: review and move or remove the existing files,");
            Console.WriteLine("         or set build.output to a dedicated empty output directory,");
            Console.WriteLine("         then rerun the build; a successful build creates .bukit-output-marker automatically.");
            return false;
        }
    }

    private static bool HasNotionSource(ContentConfig content)
    {
        if (content.Sources is null) return false;
        return content.Sources.Any(s =>
            s.Type.Equals("notion", StringComparison.OrdinalIgnoreCase) ||
            s.Notion is not null);
    }

}
