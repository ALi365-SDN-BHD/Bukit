using System.Net.Http.Headers;
using System.Text.Json;
using Scriban;
using Bukit.Config;
using Bukit.Engine.Plugins;
using Bukit.Shared;

namespace Bukit.Cli.Commands;

public static class DoctorCommand
{
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
            Console.WriteLine(ex.Message);
            return 1;
        }

        if (config.Site.Collections is null || config.Site.Collections.Count == 0)
        {
            Console.WriteLine("✖ Migration required: site.collections is not configured");
            Console.WriteLine("  - collection 驱动路由已成为主模型，请在 site.collections 中声明每个内容集合的 permalink/template/listRoute");
            Console.WriteLine("  - 示例：site.collections.article.permalink=/articles/{slug}/, template=pages/post.html, listRoute=/articles/");
            return 1;
        }

        var (layoutsDir, assetsDir, staticDir) = Bukit.Engine.BuildPathUtils.ResolveThemeDirectories(rootDir, config.Theme);
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

        foreach (var p in requiredTemplates)
        {
            var text = await File.ReadAllTextAsync(p);
            var template = Template.Parse(text, p);
            if (template.HasErrors)
            {
                Console.WriteLine($"✖ Template parse error: {p}");
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

        var listPageContentMode = (config.Build.ListPageContentMode ?? "auto").Trim().ToLowerInvariant();
        if (listPageContentMode == "auto")
        {
            WarnHeuristicFallback(layoutsDir, "pages/index.html");
            WarnHeuristicFallback(layoutsDir, "pages/list.html");
        }

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
            Routed = Array.Empty<(Bukit.Content.ContentItem Item, Bukit.Routing.RouteInfo Route)>(),
            BodyStore = Bukit.Content.NullContentBodyStore.Instance,
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

        Console.WriteLine("✔ Doctor passed");
        return 0;
    }

    private static void WarnHeuristicFallback(string layoutsDir, string templateRelativePath)
    {
        var resolution = Bukit.Engine.TemplateCapabilitiesResolver.ResolveListPageContent(templateRelativePath, layoutsDir, "auto");
        if (!resolution.UsedHeuristic)
        {
            return;
        }

        Console.WriteLine($"⚠ Template relies on heuristic fallback: {templateRelativePath}");
        Console.WriteLine($"  - 静态分析未能直接确认 needs_page_content，原因: {resolution.Source}");
        Console.WriteLine($"  - 当前 auto 模式回退推断为 {resolution.IncludeContent.ToString().ToLowerInvariant()}");
        Console.WriteLine($"  - 建议在 layouts/bukit.templates.yaml 中声明 needs_page_content: {resolution.IncludeContent.ToString().ToLowerInvariant()}");
    }

    private static async Task<bool> CheckNotionAsync(string token, string databaseId)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        http.DefaultRequestHeaders.Add("Notion-Version", Bukit.Content.Notion.NotionApiUrls.NotionVersion);

        var url = Bukit.Content.Notion.NotionApiUrls.Database(databaseId);
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
