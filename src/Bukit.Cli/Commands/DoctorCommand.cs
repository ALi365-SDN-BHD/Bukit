using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Scriban;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Bukit.Shared.Notion;

namespace Bukit.Cli.Commands;

public static class DoctorCommand
{
    internal sealed record DoctorContext(
        string RootDir,
        AppConfig Config,
        string LayoutsDir,
        string[] AllHtmlFiles);
    public static async Task<int> RunAsync(ArgReader reader)
    {
        var resolved = ConfigPathResolver.Resolve(reader);
        var rootDir = resolved.RootDir;
        var config = ConfigLoader.Load(resolved.FullConfigPath);

        var siteUrl = reader.GetOption("--site-url");
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

        if (config.Site.Collections is null || config.Site.Collections.Count == 0)
        {
            Console.WriteLine("✖ Migration required: site.collections is not configured");
            Console.WriteLine("  - collection 驱动路由已成为主模型，请在 site.collections 中声明每个内容集合的 permalink/template/listRoute");
            Console.WriteLine("  - post/page 默认规则仍作为兼容层保留，但不再是新项目的推荐主路径");
            Console.WriteLine("  - 示例：site.collections.article.permalink=/articles/{slug}/, template=pages/post.html, listRoute=/articles/");
            return 1;
        }

        var (layoutsDir, assetsDir, staticDir, _, _, _, _) = Bukit.Engine.BuildPathUtils.ResolveThemeDirectories(rootDir, config.Theme);
        if (!Directory.Exists(layoutsDir))
        {
            Console.WriteLine($"✖ Layouts dir not found: {layoutsDir}");
            return 1;
        }

        var requiredTemplates = new[]
        {
            Path.Combine(layoutsDir, "layouts", "base.html"),
            Path.Combine(layoutsDir, "pages", "page.html"),
            Path.Combine(layoutsDir, "pages", "post.html"),
            Path.Combine(layoutsDir, "pages", "index.html"),
            Path.Combine(layoutsDir, "pages", "list.html")
        };

        var missing = requiredTemplates.Where(p => !File.Exists(p)).ToList();
        if (missing.Count > 0)
        {
            Console.WriteLine("✖ Missing templates:");
            foreach (var p in missing)
            {
                Console.WriteLine($"  - {p}");
            }

            return 1;
        }

        Console.WriteLine("✔ Templates present");

        var allHtmlFiles = Directory.GetFiles(layoutsDir, "*.html", SearchOption.AllDirectories);
        foreach (var p in allHtmlFiles)
        {
            var text = await File.ReadAllTextAsync(p);
            var template = Template.Parse(text, p);
            if (template.HasErrors)
            {
                var relative = Path.GetRelativePath(layoutsDir, p);
                Console.WriteLine($"✖ Template parse error: {relative}");
                foreach (var m in template.Messages)
                {
                    Console.WriteLine($"  - {m}");
                }
                return 1;
            }
        }

        Console.WriteLine("✔ Templates parse");

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
        AnalyzeTemplateChains(layoutsDir, allHtmlFiles);

        Console.WriteLine();
        Console.WriteLine("--- Template variable spell check ---");
        CheckTemplateVariables(layoutsDir);

        var ctx = new DoctorContext(rootDir, config, layoutsDir, allHtmlFiles);

        Console.WriteLine();
        CheckThemeParamsConsistency(ctx);

        var themeRoot = Path.Combine(rootDir, "themes", config.Theme.Name ?? "starter");
        if (Directory.Exists(themeRoot))
        {
            var yamlWarnings = ConfigValidator.ValidateThemeYaml(themeRoot);
            if (yamlWarnings is { Count: > 0 })
            {
                Console.WriteLine("⚠ theme.yaml warnings:");
                foreach (var w in yamlWarnings)
                    Console.WriteLine($"  - {w}");
            }
        }

        var listPageContentMode = (config.Build.ListPageContentMode ?? "auto").Trim().ToLowerInvariant();
        if (listPageContentMode == "auto")
        {
            DoctorManifestChecker.WarnHeuristicFallback(layoutsDir, "pages/index.html");
            DoctorManifestChecker.WarnHeuristicFallback(layoutsDir, "pages/list.html");
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
            Config = config,
            RootDir = rootDir,
            OutputDir = Path.Combine(rootDir, config.Build.Output),
            BaseUrl = config.Site.BaseUrl,
            LayoutsDir = layoutsDir,
            Routed = Array.Empty<(Bukit.Engine.Abstractions.Content.ContentItem Item, Bukit.Engine.Abstractions.Routing.RouteInfo Route)>(),
            BodyStore = Bukit.Engine.Abstractions.Content.NullContentBodyStore.Instance,
            Logger = new ConsoleLogger(LogLevel.Info)
        };

        var plugins = PluginRegistry.GetAllPlugins(pluginContext).Select(x => x.Plugin).ToList();
        Console.WriteLine($"✔ Plugins discovered: {plugins.Count}");

        if (config.Content.Provider.Equals("notion", StringComparison.OrdinalIgnoreCase) && config.Content.Notion is not null)
        {
            var token = EnvironmentHelper.GetNotionToken();
            if (string.IsNullOrWhiteSpace(token))
            {
                Console.WriteLine("✖ NOTION_TOKEN not set");
                return 1;
            }

            var ok = await CheckNotionAsync(token, config.Content.Notion.DatabaseId);
            if (!ok)
            {
                return 1;
            }
        }

        IReadOnlyList<(ContentItem Item, RouteInfo Route)> routed;
        try
        {
            routed = await RouteInventoryValidator.BuildContentRoutesAsync(
                config,
                rootDir,
                isCi: false,
                new ConsoleLogger(LogLevel.Error));
            RouteInventoryValidator.ValidateContentRoutes(routed);
            Console.WriteLine("✔ Routes valid");
        }
        catch (Exception ex) when (ex is ConfigException or ContentException)
        {
            Console.WriteLine("✖ Route inventory error");
            Console.WriteLine(ex.Message);
            return 1;
        }

        var listRoutes = SiteEngine.GetListRoutes(config.Site.Collections);
        DoctorManifestChecker.CheckUnreferencedTemplates(layoutsDir, allHtmlFiles, config, listRoutes);

        Console.WriteLine();
        var hasSchemaErrors = DoctorSchemaChecker.CheckSchemaFieldCompleteness(ctx, routed);
        DoctorSchemaChecker.CheckTemplateFieldsVsSchema(ctx);
        DoctorSchemaChecker.CheckExtraContentFields(ctx, routed);

        if (hasSchemaErrors)
        {
            return 1;
        }

        Console.WriteLine("✔ Doctor passed");
        return 0;
    }

    private static void AnalyzeTemplateChains(string layoutsDir, string[] allHtmlFiles)
    {
        foreach (var file in allHtmlFiles.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var relative = Path.GetRelativePath(layoutsDir, file).Replace('\\', '/');
            var text = File.ReadAllText(file);

            var layoutRefs = ExtractDirectives(text, "layout");
            var includeRefs = ExtractDirectives(text, "include");

            if (layoutRefs.Count > 0 || includeRefs.Count > 0)
            {
                Console.Write($"  {relative}");

                if (layoutRefs.Count > 0)
                {
                    Console.Write($"  layout → [{string.Join(", ", layoutRefs)}]");
                }

                if (includeRefs.Count > 0)
                {
                    Console.Write($"  include → [{string.Join(", ", includeRefs)}]");
                }

                Console.WriteLine();
            }
        }
    }

    internal static List<string> ExtractDirectives(string text, string directiveType)
    {
        var results = new List<string>();
        var patterns = new[]
        {
            $@"\{{% ?{directiveType} ?""([^""]+)"" ?%}}",
            $@"\{{% ?{directiveType} ?'([^']+)' ?%}}",
            $@"\{{{{\s*{directiveType}\s+""([^""]+)""\s*}}}}",
            $@"\{{{{\s*{directiveType}\s+'([^']+)'\s*}}}}"
        };

        foreach (var pattern in patterns)
        {
            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(text, pattern))
            {
                results.Add(match.Groups[1].Value);
            }
        }

        return results;
    }

    private static void CheckThemeParamsConsistency(DoctorContext ctx)
    {
        var themeParams = ctx.Config.Theme.Params;

        var allContent = new System.Text.StringBuilder();
        foreach (var file in ctx.AllHtmlFiles)
        {
            try { allContent.Append(File.ReadAllText(file)); }
            catch { }
        }

        var combined = allContent.ToString();

        if (themeParams is { Count: > 0 })
        {
            var unused = new List<string>();

            foreach (var kv in themeParams)
            {
                var searchPattern = $"site.theme.params.{kv.Key}";
                if (!combined.Contains(searchPattern, StringComparison.OrdinalIgnoreCase))
                {
                    searchPattern = $"site.params.{kv.Key}";
                    if (!combined.Contains(searchPattern, StringComparison.OrdinalIgnoreCase))
                    {
                        unused.Add(kv.Key);
                    }
                }
            }

            if (unused.Count > 0)
            {
                Console.WriteLine($"⚠ {unused.Count} theme param(s) declared but not used in templates:");
                foreach (var key in unused)
                {
                    Console.WriteLine($"  - {key}");
                }
            }
        }

        var referencedParams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var paramRegex = new Regex(@"site\.theme\.params\.(\w+)", RegexOptions.IgnoreCase);
        foreach (Match m in paramRegex.Matches(combined))
        {
            if (m.Groups.Count > 1)
                referencedParams.Add(m.Groups[1].Value);
        }

        var paramRegex2 = new Regex(@"site\.params\.(\w+)", RegexOptions.IgnoreCase);
        foreach (Match m in paramRegex2.Matches(combined))
        {
            if (m.Groups.Count > 1)
                referencedParams.Add(m.Groups[1].Value);
        }

        if (themeParams is null || themeParams.Count == 0)
        {
            if (referencedParams.Count > 0)
            {
                Console.WriteLine($"⚠ Templates reference {referencedParams.Count} theme param(s) not declared in config:");
                foreach (var key in referencedParams.OrderBy(x => x))
                    Console.WriteLine($"  - {key}");
            }

            return;
        }

        var declaredKeys = new HashSet<string>(themeParams.Keys, StringComparer.OrdinalIgnoreCase);
        var undeclaredRefs = referencedParams.Where(r => !declaredKeys.Contains(r)).OrderBy(x => x).ToList();
        if (undeclaredRefs.Count > 0)
        {
            Console.WriteLine($"⚠ Templates reference {undeclaredRefs.Count} theme param(s) not declared in config:");
            foreach (var key in undeclaredRefs)
                Console.WriteLine($"  - {key}");
        }
    }

    private static void CheckTemplateVariables(string layoutsDir)
    {
        var warnings = Bukit.Engine.ScribanTemplateLinter.LintDirectory(layoutsDir, "");

        if (warnings.Count == 0)
        {
            Console.WriteLine("✔ No unknown template variables detected");
            return;
        }

        foreach (var w in warnings)
        {
            Console.WriteLine($"⚠ {w.Template}: {w.Message}");
        }
    }

    private static async Task<bool> CheckNotionAsync(string token, string databaseId)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        http.DefaultRequestHeaders.Add("Notion-Version", Bukit.Shared.Notion.NotionApiUrls.NotionVersion);

        var url = Bukit.Shared.Notion.NotionApiUrls.Database(databaseId);
        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync(url);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✖ Notion request failed: {ex.Message}");
            return false;
        }

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("✔ Notion database reachable");
            return true;
        }

        Console.WriteLine($"✖ Notion database check failed: {(int)response.StatusCode} {response.ReasonPhrase}");
        return false;
    }
}
