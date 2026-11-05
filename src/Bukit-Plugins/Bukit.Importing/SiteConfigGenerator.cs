using YamlDotNet.RepresentationModel;

namespace Bukit.Importing;

internal static class SiteConfigGenerator
{
    internal static bool Generate(
        HtmlDemoImportOptions options,
        RouteMapConfig? routeMap = null,
        HashSet<PageType>? pageTypes = null,
        string? postListSlug = null,
        PageTemplatePlan? plan = null,
        IReadOnlyList<DiscoveredPage>? discoveredPages = null)
    {
        var siteDir = HtmlDemoImporter.GetSiteDir(options);
        Directory.CreateDirectory(siteDir);
        var yamlPath = Path.Combine(siteDir, "site.yaml");
        if (File.Exists(yamlPath))
        {
            Console.WriteLine("  site.yaml 已存在，跳过生成。建议手动检查。");
            return false;
        }

        var contentPath = Path.Combine(siteDir, "content");
        var contentDir = Path.GetRelativePath(options.RootDir, contentPath)
            .Replace('\\', '/');
        if (contentDir.Equals("..", StringComparison.Ordinal) ||
            contentDir.StartsWith("../", StringComparison.Ordinal))
        {
            contentDir = "content";
        }

        // Default to all types if no page type info provided (backward compat)
        pageTypes ??= [PageType.Home, PageType.Page, PageType.PostDetail, PageType.PostList, PageType.CompanyDetail, PageType.CompanyList, PageType.ServiceDetail, PageType.ServiceList];

        var site = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = options.ThemeName,
            ["title"] = options.ThemeName,
            ["url"] = "https://example.com",
            ["description"] = "Generated from HTML Demo",
            ["baseUrl"] = string.IsNullOrWhiteSpace(options.BaseUrl) ? "/" : options.BaseUrl,
            ["language"] = options.Language,
            ["seo"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["renderMode"] = "inject"
            }
        };

        var collections = new Dictionary<string, object?>(StringComparer.Ordinal);

        if (pageTypes.Contains(PageType.Page) || pageTypes.Contains(PageType.Home) || pageTypes.Contains(PageType.Unknown))
        {
            collections["page"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["permalink"] = "/{slug}/",
                ["template"] = "pages/page.html"
            };
        }

        if (pageTypes.Contains(PageType.PostDetail) || pageTypes.Contains(PageType.PostList))
        {
            var post = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["permalink"] = "/insights/{slug}/",
                ["template"] = "pages/post.html"
            };
            if (pageTypes.Contains(PageType.PostList))
            {
                var fallbackName = !string.IsNullOrWhiteSpace(postListSlug)
                    ? ThemeGenerator.SanitizeTemplateName(postListSlug)
                    : "insights";
                post["listRoute"] = "/insights/";
                post["listTemplate"] = ListTemplate(PageType.PostList, fallbackName, plan, discoveredPages);
                AddListTitle(post, PageType.PostList, discoveredPages);
            }
            collections["post"] = post;
        }

        if (pageTypes.Contains(PageType.CompanyDetail) || pageTypes.Contains(PageType.CompanyList))
        {
            var company = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["permalink"] = "/companies/{slug}/",
                ["template"] = "pages/company.html"
            };
            if (pageTypes.Contains(PageType.CompanyList))
            {
                company["listRoute"] = "/companies/";
                company["listTemplate"] = ListTemplate(PageType.CompanyList, "companies", plan, discoveredPages);
                AddListTitle(company, PageType.CompanyList, discoveredPages);
            }
            collections["company"] = company;
        }

        if (pageTypes.Contains(PageType.ServiceDetail) || pageTypes.Contains(PageType.ServiceList))
        {
            var service = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["permalink"] = "/services/{slug}/",
                ["template"] = "pages/service.html"
            };
            if (pageTypes.Contains(PageType.ServiceList))
            {
                service["listRoute"] = "/services/";
                service["listTemplate"] = ListTemplate(PageType.ServiceList, "services", plan, discoveredPages);
                AddListTitle(service, PageType.ServiceList, discoveredPages);
            }
            collections["service"] = service;
        }

        site["collections"] = collections;

        // Route-map routes for pages are written into each page's own front matter (`url:`);
        // Core has no site-level page list.

        var root = new Dictionary<string, object?>(StringComparer.Ordinal) { ["site"] = site };

        var content = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (options.ContentSource.Equals("notion", StringComparison.OrdinalIgnoreCase) &&
            options.BuildSource.Equals("notion", StringComparison.OrdinalIgnoreCase))
        {
            content["sources"] = BuildNotionSources(options);
        }
        else
        {
            // Each collection reads only its own folder; a shared folder made every
            // page a post (and a company, and a service) as well.
            var sources = new List<object?>();
            void AddSource(string collection, string folder)
            {
                Directory.CreateDirectory(Path.Combine(siteDir, "content", folder));
                sources.Add(BuildMarkdownSource(collection, $"{contentDir}/{folder}"));
            }

            if (collections.ContainsKey("page"))
                AddSource("page", "pages");
            if (collections.ContainsKey("post"))
                AddSource("post", "posts");
            if (collections.ContainsKey("company"))
                AddSource("company", "companies");
            if (collections.ContainsKey("service"))
                AddSource("service", "services");
            content["sources"] = sources;
        }

        root["content"] = content;
        root["build"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["output"] = "dist",
            ["clean"] = true
        };
        root["theme"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = options.ThemeName
        };

        File.WriteAllText(yamlPath, YamlEmitter.Emit(root));
        return true;
    }

    private static List<object?> BuildNotionSources(HtmlDemoImportOptions options)
    {
        // Core reads the token from NOTION_TOKEN only; a tokenEnv key is rejected by
        // the strict config validator, so it must not be emitted here.
        var databaseTargets = string.IsNullOrWhiteSpace(options.NotionDatabaseId)
            ? ReadNotionDatabaseTargets(options)
            : [];

        var sources = new List<object?>();
        if (databaseTargets.Count > 0)
        {
            foreach (var target in databaseTargets)
            {
                sources.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["type"] = "notion",
                    ["name"] = target.Key,
                    ["mode"] = target.Mode,
                    ["collection"] = target.Collection,
                    ["notion"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["databaseId"] = target.DatabaseId,
                        ["filterProperty"] = "Published",
                        ["filterType"] = "checkbox_true",
                        ["sortProperty"] = "Title",
                        ["sortDirection"] = "ascending"
                    }
                });
            }

            return sources;
        }

        var databaseId = !string.IsNullOrWhiteSpace(options.NotionDatabaseId)
            ? options.NotionDatabaseId
            : "${NOTION_DATABASE_ID}";
        sources.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = "notion",
            ["collection"] = "page",
            ["notion"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["databaseId"] = databaseId,
                ["filterProperty"] = "Published",
                ["filterType"] = "checkbox_true",
                ["sortProperty"] = "Title",
                ["sortDirection"] = "ascending"
            }
        });
        return sources;
    }

    private static Dictionary<string, object?> BuildMarkdownSource(string collection, string contentDir)
        => new(StringComparer.Ordinal)
        {
            ["type"] = "markdown",
            ["name"] = collection,
            ["collection"] = collection,
            ["markdown"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["dir"] = contentDir
            }
        };

    // The list route shows the list page's own heading instead of Core's default title.
    private static void AddListTitle(
        Dictionary<string, object?> collection,
        PageType listType,
        IReadOnlyList<DiscoveredPage>? discoveredPages)
    {
        var listPage = discoveredPages?.FirstOrDefault(p => p.Type == listType);
        if (listPage is null)
            return;

        var h1 = System.Text.RegularExpressions.Regex.Match(
            listPage.UniqueBody, @"<h1[^>]*>(.*?)</h1>",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
        var title = h1.Success
            ? System.Net.WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(h1.Groups[1].Value, "<[^>]+>", "")).Trim()
            : listPage.Title?.Trim();
        if (!string.IsNullOrWhiteSpace(title))
            collection["listTitle"] = title;
    }

    // A list route renders the list page's own template, the same file ThemeGenerator wrote.
    private static string ListTemplate(
        PageType listType,
        string fallbackName,
        PageTemplatePlan? plan,
        IReadOnlyList<DiscoveredPage>? discoveredPages)
    {
        var listPage = discoveredPages?.FirstOrDefault(p => p.Type == listType);
        return plan is not null && listPage is not null
            ? plan.PathFor(listPage)
            : $"pages/{fallbackName}.html";
    }

    private static List<SiteConfigNotionTarget> ReadNotionDatabaseTargets(HtmlDemoImportOptions options)
    {
        var mapPath = ResolveNotionDatabaseMapPath(options);
        if (mapPath is null || !File.Exists(mapPath))
            return [];

        var stream = new YamlStream();
        using var reader = File.OpenText(mapPath);
        stream.Load(reader);
        if (stream.Documents.Count == 0 ||
            stream.Documents[0].RootNode is not YamlMappingNode root ||
            GetMap(root, "databases") is not { } databases)
            return [];

        var targets = new List<SiteConfigNotionTarget>();
        foreach (var kv in databases.Children)
        {
            if (kv.Key is not YamlScalarNode keyNode ||
                string.IsNullOrWhiteSpace(keyNode.Value) ||
                kv.Value is not YamlMappingNode map)
                continue;

            var key = keyNode.Value.Trim();
            if (!IsBuildNotionCollectionKey(key))
                continue;

            var collection = GetScalar(map, "collection") ?? InferCollection(key);
            var databaseId = GetScalar(map, "databaseId");
            if (string.IsNullOrWhiteSpace(databaseId))
                databaseId = $"${{NOTION_{key.ToUpperInvariant()}_DATABASE_ID}}";

            targets.Add(new SiteConfigNotionTarget(key, collection, databaseId, IsBuildNotionDataCollectionKey(key) ? "data" : "content"));
        }

        return targets;
    }

    private static string? ResolveNotionDatabaseMapPath(HtmlDemoImportOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.NotionDatabaseMap))
        {
            return Path.IsPathRooted(options.NotionDatabaseMap)
                ? options.NotionDatabaseMap
                : Path.Combine(HtmlDemoImporter.GetSiteDir(options), options.NotionDatabaseMap);
        }

        var defaultMap = Path.Combine(HtmlDemoImporter.GetSiteDir(options), "notion-seed", "notion-database-map.yaml");
        return File.Exists(defaultMap) ? defaultMap : null;
    }

    private static bool IsBuildNotionCollectionKey(string key)
        => key.Equals("pages", StringComparison.OrdinalIgnoreCase) ||
           key.Equals("navigation", StringComparison.OrdinalIgnoreCase) ||
           key.Equals("posts", StringComparison.OrdinalIgnoreCase) ||
           key.Equals("companies", StringComparison.OrdinalIgnoreCase) ||
           key.Equals("services", StringComparison.OrdinalIgnoreCase);

    private static bool IsBuildNotionDataCollectionKey(string key)
        => key.Equals("navigation", StringComparison.OrdinalIgnoreCase);

    private static string InferCollection(string key)
        => key.ToLowerInvariant() switch
        {
            "pages" => "page",
            "navigation" => "navigation",
            "posts" => "post",
            "companies" => "company",
            "services" => "service",
            _ => key.TrimEnd('s')
        };

    private static YamlMappingNode? GetMap(YamlMappingNode map, string key)
        => map.Children.FirstOrDefault(kv =>
            kv.Key is YamlScalarNode scalar && scalar.Value == key).Value as YamlMappingNode;

    private static string? GetScalar(YamlMappingNode map, string key)
        => map.Children.FirstOrDefault(kv =>
            kv.Key is YamlScalarNode scalar && scalar.Value == key).Value is YamlScalarNode value
            ? value.Value
            : null;

    private sealed record SiteConfigNotionTarget(string Key, string Collection, string DatabaseId, string Mode);
}
