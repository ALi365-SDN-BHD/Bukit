using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Scriban;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine;
using Bukit.Engine.Plugins;
using Bukit.Routing;
using Bukit.Shared;
using Bukit.Shared.Notion;
using YamlDotNet.RepresentationModel;

namespace Bukit.Cli.Commands;

public static class DoctorCommand
{
    private sealed record DoctorContext(
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

        CheckManifestCompleteness(layoutsDir, allHtmlFiles);

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
            WarnHeuristicFallback(layoutsDir, "pages/index.html");
            WarnHeuristicFallback(layoutsDir, "pages/list.html");
        }

        Console.WriteLine();
        CheckMarkdownFrontMatter(ctx);
        CheckMarkdownSyntax(ctx);
        CheckMarkdownEmptyBody(ctx);

        Console.WriteLine();
        CheckHardcodedUrls(ctx);
        CheckHardcodedText(ctx);

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
        CheckUnreferencedTemplates(layoutsDir, allHtmlFiles, config, listRoutes);

        Console.WriteLine();
        var hasSchemaErrors = CheckSchemaFieldCompleteness(ctx, routed);
        CheckTemplateFieldsVsSchema(ctx);
        CheckExtraContentFields(ctx, routed);

        if (hasSchemaErrors)
        {
            return 1;
        }

        Console.WriteLine("✔ Doctor passed");
        return 0;
    }

    private static void CheckManifestCompleteness(string layoutsDir, string[] allHtmlFiles)
    {
        var manifestPath = Path.Combine(layoutsDir, "bukit.templates.yaml");
        if (!File.Exists(manifestPath))
        {
            Console.WriteLine("⚠ No bukit.templates.yaml found. Run 'bukit template sync' to auto-generate it.");
            return;
        }

        try
        {
            var yaml = File.ReadAllText(manifestPath);
            var stream = new YamlDotNet.RepresentationModel.YamlStream();
            stream.Load(new StringReader(yaml));
            if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlDotNet.RepresentationModel.YamlMappingNode root)
            {
                return;
            }

            if (!root.Children.TryGetValue(new YamlDotNet.RepresentationModel.YamlScalarNode("templates"), out var templatesNode) ||
                templatesNode is not YamlDotNet.RepresentationModel.YamlMappingNode templatesMap)
            {
                Console.WriteLine("⚠ bukit.templates.yaml exists but has no 'templates' section.");
                return;
            }

            var declaredPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in templatesMap.Children)
            {
                if (kv.Key is YamlDotNet.RepresentationModel.YamlScalarNode keyNode)
                {
                    declaredPaths.Add(keyNode.Value ?? "");
                }
            }

            var actualPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in allHtmlFiles)
            {
                actualPaths.Add(Path.GetRelativePath(layoutsDir, file).Replace('\\', '/'));
            }

            var missingDeclarations = actualPaths.Except(declaredPaths).ToList();
            var staleDeclarations = declaredPaths.Except(actualPaths).ToList();

            if (missingDeclarations.Count == 0 && staleDeclarations.Count == 0)
            {
                Console.WriteLine("✔ Template manifest matches actual files");
            }
            else
            {
                if (missingDeclarations.Count > 0)
                {
                    Console.WriteLine($"⚠ {missingDeclarations.Count} template(s) not in bukit.templates.yaml:");
                    foreach (var t in missingDeclarations.OrderBy(x => x))
                        Console.WriteLine($"  - {t}");
                }

                if (staleDeclarations.Count > 0)
                {
                    Console.WriteLine($"⚠ {staleDeclarations.Count} stale declaration(s) in bukit.templates.yaml:");
                    foreach (var t in staleDeclarations.OrderBy(x => x))
                        Console.WriteLine($"  - {t}");
                }

                Console.WriteLine("  Run 'bukit template sync' to fix.");
            }
        }
        catch
        {
            Console.WriteLine("⚠ Could not parse bukit.templates.yaml for completeness check.");
        }
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

    private static List<string> ExtractDirectives(string text, string directiveType)
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

    private static void CheckUnreferencedTemplates(
        string layoutsDir,
        string[] allHtmlFiles,
        AppConfig config,
        IReadOnlyList<Bukit.Routing.RouteInfo> listRoutes)
    {
        var usedTemplates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        usedTemplates.Add(Path.Combine("layouts", "base.html"));

        if (config.Site.Collections is not null)
        {
            foreach (var (_, collection) in config.Site.Collections)
            {
                if (!string.IsNullOrWhiteSpace(collection.Template))
                {
                    usedTemplates.Add(collection.Template.Trim());
                }

                if (!string.IsNullOrWhiteSpace(collection.ListTemplate))
                {
                    usedTemplates.Add(collection.ListTemplate.Trim());
                }
            }
        }

        usedTemplates.Add("pages/index.html");
        usedTemplates.Add("pages/list.html");
        usedTemplates.Add("pages/post.html");
        usedTemplates.Add("pages/page.html");

        var taxonomyTemplate = config.Taxonomy.Template ?? "pages/taxonomy-term.html";
        usedTemplates.Add(taxonomyTemplate);
        if (config.Taxonomy.IndexTemplate is not null)
        {
            usedTemplates.Add(config.Taxonomy.IndexTemplate);
        }

        if (config.Taxonomy.TermTemplate is not null)
        {
            usedTemplates.Add(config.Taxonomy.TermTemplate);
        }

        if (config.Taxonomy.Templates.Tags.Template is not null)
        {
            usedTemplates.Add(config.Taxonomy.Templates.Tags.Template);
        }

        if (config.Taxonomy.Templates.Categories.Template is not null)
        {
            usedTemplates.Add(config.Taxonomy.Templates.Categories.Template);
        }

        foreach (var listRoute in listRoutes)
        {
            if (!string.IsNullOrWhiteSpace(listRoute.Template))
            {
                usedTemplates.Add(listRoute.Template);
            }
        }

        foreach (var file in allHtmlFiles)
        {
            var relative = Path.GetRelativePath(layoutsDir, file).Replace('\\', '/');
            var text = File.ReadAllText(file);
            var layoutRefs = ExtractDirectives(text, "layout");
            var includeRefs = ExtractDirectives(text, "include");

            if (includeRefs.Count > 0 || layoutRefs.Count > 0)
            {
                usedTemplates.Add(relative);
            }

            foreach (var layoutRef in layoutRefs)
            {
                if (!string.IsNullOrWhiteSpace(layoutRef))
                {
                    usedTemplates.Add(layoutRef.Trim());
                }
            }

            foreach (var includeRef in includeRefs)
            {
                if (!string.IsNullOrWhiteSpace(includeRef))
                {
                    usedTemplates.Add(includeRef.Trim());
                }
            }
        }

        foreach (var used in usedTemplates.ToList())
        {
            foreach (var file in allHtmlFiles)
            {
                var relative = Path.GetRelativePath(layoutsDir, file).Replace('\\', '/');
                if (used.Equals(relative, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
            }
        }

        var actualFiles = new HashSet<string>(
            allHtmlFiles.Select(f => Path.GetRelativePath(layoutsDir, f).Replace('\\', '/')),
            StringComparer.OrdinalIgnoreCase);

        var unreferenced = actualFiles
            .Where(f => !usedTemplates.Contains(f))
            .OrderBy(f => f)
            .ToList();

        if (unreferenced.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"⚠ {unreferenced.Count} template(s) appear unreferenced by any route:");
            foreach (var t in unreferenced)
            {
                Console.WriteLine($"  - {t}");
            }

            Console.WriteLine("  These may be unused and safe to remove.");
        }
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

    private static void CheckMarkdownFrontMatter(DoctorContext ctx)
    {
        var contentDir = ctx.Config.Content.Markdown?.Dir ?? "content";
        var absDir = Path.GetFullPath(Path.Combine(ctx.RootDir, contentDir));
        if (!Directory.Exists(absDir))
        {
            return;
        }

        var mdFiles = Directory.GetFiles(absDir, "*.md", SearchOption.AllDirectories);
        if (mdFiles.Length == 0)
        {
            return;
        }

        var issues = new List<string>();
        foreach (var file in mdFiles)
        {
            var text = File.ReadAllText(file);
            var relative = Path.GetRelativePath(ctx.RootDir, file).Replace('\\', '/');

            var normalized = text.Replace("\r\n", "\n");
            if (!normalized.StartsWith("---\n", StringComparison.Ordinal) && normalized.TrimStart() != "---")
            {
                continue;
            }

            var lines = normalized.Split('\n');
            if (lines.Length < 3 || lines[0].Trim() != "---")
            {
                issues.Add($"{relative}: malformed front matter start");
                continue;
            }

            var end = -1;
            for (var i = 1; i < lines.Length; i++)
            {
                if (lines[i].Trim() == "---")
                {
                    end = i;
                    break;
                }
            }

            if (end <= 0)
            {
                issues.Add($"{relative}: unclosed front matter (missing closing ---)");
                continue;
            }

            if (end == 1)
            {
                issues.Add($"{relative}: empty front matter block");
                continue;
            }

            var frontMatterYaml = string.Join("\n", lines.Skip(1).Take(end - 1));
            try
            {
                var stream = new YamlStream();
                stream.Load(new StringReader(frontMatterYaml));
                if (stream.Documents.Count == 0)
                {
                    issues.Add($"{relative}: empty front matter");
                    continue;
                }

                if (stream.Documents[0].RootNode is not YamlMappingNode root || root.Children.Count == 0)
                {
                    issues.Add($"{relative}: front matter has no key-value pairs");
                }
            }
            catch (Exception)
            {
                issues.Add($"{relative}: failed to parse YAML front matter");
            }
        }

        if (issues.Count > 0)
        {
            Console.WriteLine($"⚠ {issues.Count} Markdown front matter issue(s) found:");
            foreach (var issue in issues)
            {
                Console.WriteLine($"  - {issue}");
            }
        }
    }

    private static void CheckMarkdownSyntax(DoctorContext ctx)
    {
        var contentDir = ctx.Config.Content.Markdown?.Dir ?? "content";
        var absDir = Path.GetFullPath(Path.Combine(ctx.RootDir, contentDir));
        if (!Directory.Exists(absDir))
        {
            return;
        }

        var mdFiles = Directory.GetFiles(absDir, "*.md", SearchOption.AllDirectories);
        if (mdFiles.Length == 0)
        {
            return;
        }

        var suggestions = new List<string>();
        foreach (var file in mdFiles)
        {
            var text = File.ReadAllText(file);
            var relative = Path.GetRelativePath(ctx.RootDir, file).Replace('\\', '/');

            var body = text;
            if (text.StartsWith("---", StringComparison.Ordinal))
            {
                var normalized = text.Replace("\r\n", "\n");
                var lines = normalized.Split('\n');
                var end = -1;
                for (var i = 1; i < lines.Length; i++)
                {
                    if (lines[i].Trim() == "---")
                    {
                        end = i;
                        break;
                    }
                }

                if (end > 0)
                {
                    body = string.Join("\n", lines.Skip(end + 1));
                }
            }

            var bodyLines = body.Replace("\r\n", "\n").Split('\n');
            var fenceCount = 0;
            var lastFenceLine = 0;
            for (var i = 0; i < bodyLines.Length; i++)
            {
                if (bodyLines[i].TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    fenceCount++;
                    lastFenceLine = i + 1;
                }
            }

            if (fenceCount % 2 != 0)
            {
                suggestions.Add($"{relative}: line {lastFenceLine}: unclosed code block ({fenceCount} fence(s) found)");
            }

            var emptyLinkRegex = new Regex(@"\[.*?\]\(\s*\)");
            for (var i = 0; i < bodyLines.Length; i++)
            {
                var m = emptyLinkRegex.Match(bodyLines[i]);
                if (m.Success)
                {
                    suggestions.Add($"{relative}: line {i + 1}: empty link detected `{m.Value}`");
                    break;
                }
            }

            var emptyImgRegex = new Regex(@"!\[.*?\]\(\s*\)");
            for (var i = 0; i < bodyLines.Length; i++)
            {
                var m = emptyImgRegex.Match(bodyLines[i]);
                if (m.Success)
                {
                    suggestions.Add($"{relative}: line {i + 1}: empty image link detected `{m.Value}`");
                    break;
                }
            }
        }

        if (suggestions.Count > 0)
        {
            Console.WriteLine($"⚠ {suggestions.Count} Markdown syntax suggestion(s):");
            foreach (var s in suggestions)
            {
                Console.WriteLine($"  - {s}");
            }
        }
    }

    private static void CheckMarkdownEmptyBody(DoctorContext ctx)
    {
        var contentDir = ctx.Config.Content.Markdown?.Dir ?? "content";
        var absDir = Path.GetFullPath(Path.Combine(ctx.RootDir, contentDir));
        if (!Directory.Exists(absDir))
        {
            return;
        }

        var mdFiles = Directory.GetFiles(absDir, "*.md", SearchOption.AllDirectories);
        if (mdFiles.Length == 0)
        {
            return;
        }

        var emptyFiles = new List<string>();
        foreach (var file in mdFiles)
        {
            var text = File.ReadAllText(file);
            var relative = Path.GetRelativePath(ctx.RootDir, file).Replace('\\', '/');

            var body = text;
            if (text.StartsWith("---", StringComparison.Ordinal))
            {
                var normalized = text.Replace("\r\n", "\n");
                var lines = normalized.Split('\n');
                var end = -1;
                for (var i = 1; i < lines.Length; i++)
                {
                    if (lines[i].Trim() == "---")
                    {
                        end = i;
                        break;
                    }
                }

                if (end > 0)
                {
                    body = string.Join("\n", lines.Skip(end + 1));
                }
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                emptyFiles.Add(relative);
            }
        }

        if (emptyFiles.Count > 0)
        {
            Console.WriteLine($"⚠ {emptyFiles.Count} Markdown file(s) have empty body:");
            foreach (var f in emptyFiles)
            {
                Console.WriteLine($"  - {f}");
            }
        }
    }

    private static void CheckHardcodedUrls(DoctorContext ctx)
    {
        var issues = new List<string>();
        foreach (var file in ctx.AllHtmlFiles)
        {
            var text = File.ReadAllText(file);
            var relative = Path.GetRelativePath(ctx.LayoutsDir, file).Replace('\\', '/');

            var withoutComments = RemoveHtmlComments(text);
            var withoutScriban = RemoveScribanBlocks(withoutComments);

            var lines = withoutScriban.Replace("\r\n", "\n").Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var absUrlMatch = Regex.Match(line, @"(href|src)\s*=\s*""(https?://[^""]+)""", RegexOptions.IgnoreCase);
                if (absUrlMatch.Success)
                {
                    var attr = absUrlMatch.Groups[1].Value;
                    var url = absUrlMatch.Groups[2].Value;
                    if (!url.Contains("{{", StringComparison.Ordinal) &&
                        !url.Contains("{%", StringComparison.Ordinal) &&
                        !line.TrimStart().StartsWith("xmlns", StringComparison.OrdinalIgnoreCase) &&
                        !line.TrimStart().StartsWith("xsi:", StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add($"{relative}: line {i + 1}: {attr}=\"{url}\" (consider using site.url)");
                        break;
                    }
                }

                var rootRelMatch = Regex.Match(line, @"(href|src)\s*=\s*""/([^""]+)""", RegexOptions.IgnoreCase);
                if (rootRelMatch.Success)
                {
                    var attr = rootRelMatch.Groups[1].Value;
                    var path = rootRelMatch.Groups[2].Value;
                    if (!path.Contains("{{", StringComparison.Ordinal) &&
                        !path.Contains("{%", StringComparison.Ordinal) &&
                        !path.StartsWith("/", StringComparison.Ordinal))
                    {
                        issues.Add($"{relative}: line {i + 1}: {attr}=\"/{path}\" (consider using site.base_url)");
                        break;
                    }
                }
            }
        }

        if (issues.Count > 0)
        {
            Console.WriteLine($"⚠ {issues.Count} hardcoded URL(s) in templates:");
            foreach (var issue in issues)
            {
                Console.WriteLine($"  - {issue}");
            }
        }
    }

    private static void CheckHardcodedText(DoctorContext ctx)
    {
        var issues = new List<string>();
        var copyrightRegex = new Regex(@"©\s*20\d{2}", RegexOptions.IgnoreCase);
        var copyrightRegex2 = new Regex(@"Copyright\s+20\d{2}", RegexOptions.IgnoreCase);

        foreach (var file in ctx.AllHtmlFiles)
        {
            var text = File.ReadAllText(file);
            var relative = Path.GetRelativePath(ctx.LayoutsDir, file).Replace('\\', '/');

            var withoutComments = RemoveHtmlComments(text);
            var cleaned = RemoveScribanBlocks(withoutComments);
            var withoutScripts = RemoveTagContent(cleaned, "script");
            var withoutStyles = RemoveTagContent(withoutScripts, "style");
            var plainText = ExtractHtmlText(withoutStyles);

            var lines = plainText.Replace("\r\n", "\n").Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    continue;
                }

                var m1 = copyrightRegex.Match(trimmed);
                if (m1.Success)
                {
                    issues.Add($"{relative}: line {i + 1}: \"{m1.Value}\" (hardcoded year, consider {{{{ site.now }}}})");
                    break;
                }

                var m2 = copyrightRegex2.Match(trimmed);
                if (m2.Success)
                {
                    issues.Add($"{relative}: line {i + 1}: \"{m2.Value}\" (hardcoded year, consider {{{{ site.now }}}})");
                    break;
                }

                if (trimmed.Length > 20 && !trimmed.Contains("{{", StringComparison.Ordinal) && !trimmed.Contains("{%", StringComparison.Ordinal))
                {
                    var snippet = trimmed.Length > 60 ? trimmed[..57] + "..." : trimmed;
                    issues.Add($"{relative}: line {i + 1}: hardcoded text snippet ({trimmed.Length} chars): \"{snippet}\"");
                    break;
                }
            }
        }

        if (issues.Count > 0)
        {
            Console.WriteLine($"⚠ {issues.Count} hardcoded text issue(s) in templates:");
            foreach (var issue in issues)
            {
                Console.WriteLine($"  - {issue}");
            }
        }
    }

    private static string RemoveScribanBlocks(string text)
    {
        var result = Regex.Replace(text, @"\{\{.*?\}\}", " ", RegexOptions.Singleline);
        result = Regex.Replace(result, @"\{%[^%]*?%\}", " ", RegexOptions.Singleline);
        return result;
    }

    private static string RemoveHtmlComments(string text)
    {
        return Regex.Replace(text, @"<!--.*?-->", " ", RegexOptions.Singleline);
    }

    private static string RemoveTagContent(string text, string tag)
    {
        return Regex.Replace(text, $@"<{tag}\b[^>]*>.*?</{tag}>", " ", RegexOptions.Singleline | RegexOptions.IgnoreCase);
    }

    private static string ExtractHtmlText(string text)
    {
        text = Regex.Replace(text, @"<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    private static bool CheckSchemaFieldCompleteness(DoctorContext ctx, IReadOnlyList<(ContentItem Item, RouteInfo Route)> routed)
    {
        var collections = ctx.Config.Site.Collections;
        if (collections is null || collections.Count == 0)
        {
            return false;
        }

        var hasErrors = false;
        var allErrors = new List<string>();
        var allWarnings = new List<string>();

        foreach (var (collectionName, collectionConfig) in collections)
        {
            var schema = collectionConfig.Schema;
            if (schema is null || schema.Count == 0)
            {
                continue;
            }

            var template = collectionConfig.Template?.Trim();
            var collectionItems = string.IsNullOrWhiteSpace(template)
                ? routed
                : routed.Where(r => r.Route.Template?.Trim() == template).ToList();

            foreach (var (item, _) in collectionItems)
            {
                var errors = ContentSchemaValidator.Validate(item.Meta, schema, item.Id);
                foreach (var err in errors)
                {
                    var detail = $"{err.SourcePath ?? item.Id} (collection: {collectionName}): {err.Message}";
                    if (err.Code == "required")
                    {
                        hasErrors = true;
                        allErrors.Add(detail);
                    }
                    else
                    {
                        allWarnings.Add(detail);
                    }
                }
            }
        }

        if (hasErrors)
        {
            Console.WriteLine($"✖ {allErrors.Count} schema validation error(s):");
            foreach (var e in allErrors)
            {
                Console.WriteLine($"  - {e}");
            }
        }

        if (allWarnings.Count > 0)
        {
            Console.WriteLine($"⚠ {allWarnings.Count} schema validation warning(s):");
            foreach (var w in allWarnings)
            {
                Console.WriteLine($"  - {w}");
            }
        }

        return hasErrors;
    }

    private static void CheckTemplateFieldsVsSchema(DoctorContext ctx)
    {
        var collections = ctx.Config.Site.Collections;
        if (collections is null || collections.Count == 0)
        {
            return;
        }

        var mismatches = new List<string>();
        foreach (var (collectionName, collectionConfig) in collections)
        {
            var schemaFieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (collectionConfig.Schema is { Count: > 0 })
            {
                foreach (var f in collectionConfig.Schema)
                {
                    if (!string.IsNullOrWhiteSpace(f.Name))
                    {
                        schemaFieldNames.Add(f.Name.Trim());
                    }
                }
            }

            var templates = new List<string>();
            if (!string.IsNullOrWhiteSpace(collectionConfig.Template))
                templates.Add(collectionConfig.Template.Trim());
            if (!string.IsNullOrWhiteSpace(collectionConfig.ListTemplate))
                templates.Add(collectionConfig.ListTemplate.Trim());

            foreach (var templatePath in templates)
            {
                var capabilities = TemplateCapabilitiesResolver.GetCapabilities(templatePath, ctx.LayoutsDir);
                if (capabilities?.Fields is null || capabilities.Fields.Count == 0)
                {
                    continue;
                }

                foreach (var field in capabilities.Fields)
                {
                    if (string.IsNullOrWhiteSpace(field.Key) || field.Key == "tags" || field.Key == "categories")
                    {
                        continue;
                    }

                    if (!schemaFieldNames.Contains(field.Key))
                    {
                        mismatches.Add($"{collectionName} → {templatePath}: template declares field '{field.Key}' but collection schema has no such field");
                    }
                }
            }
        }

        if (mismatches.Count > 0)
        {
            Console.WriteLine($"⚠ Template fields vs schema mismatch:");
            foreach (var m in mismatches)
            {
                Console.WriteLine($"  - {m}");
            }
        }
    }

    private static void CheckExtraContentFields(DoctorContext ctx, IReadOnlyList<(ContentItem Item, RouteInfo Route)> routed)
    {
        var collections = ctx.Config.Site.Collections;
        if (collections is null || collections.Count == 0)
        {
            return;
        }

        var reservedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "title", "slug", "type", "publishat", "language", "tags", "categories",
            "summary", "route", "url", "outputpath", "template", "source", "sourcepath",
            "bodyfingerprint", "draft"
        };

        var extraFields = new List<string>();
        var totalExtras = 0;
        var filesWithExtras = 0;

        foreach (var (collectionName, collectionConfig) in collections)
        {
            var schemaFieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (collectionConfig.Schema is { Count: > 0 })
            {
                foreach (var f in collectionConfig.Schema)
                {
                    if (!string.IsNullOrWhiteSpace(f.Name))
                    {
                        schemaFieldNames.Add(f.Name.Trim());
                    }
                }
            }

            var template = collectionConfig.Template?.Trim();
            var collectionItems = string.IsNullOrWhiteSpace(template)
                ? routed
                : routed.Where(r => r.Route.Template?.Trim() == template).ToList();

            foreach (var (item, _) in collectionItems)
            {
                var fileExtras = new List<string>();
                foreach (var kv in item.Meta)
                {
                    if (reservedKeys.Contains(kv.Key))
                    {
                        continue;
                    }

                    if (!schemaFieldNames.Contains(kv.Key))
                    {
                        fileExtras.Add(kv.Key);
                    }
                }

                if (fileExtras.Count > 0)
                {
                    filesWithExtras++;
                    totalExtras += fileExtras.Count;
                    var fileId = item.Meta.TryGetValue("sourcePath", out var sp) && sp is string s ? s : item.Id;
                    extraFields.Add($"{fileId}: field(s) [{string.Join(", ", fileExtras)}] not in collection schema");
                }
            }
        }

        if (extraFields.Count > 0)
        {
            Console.WriteLine($"ℹ Extra fields in content not declared in schema:");
            foreach (var e in extraFields)
            {
                Console.WriteLine($"  - {e}");
            }

            Console.WriteLine($"  ({totalExtras} extra field(s) total across {filesWithExtras} file(s))");
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
