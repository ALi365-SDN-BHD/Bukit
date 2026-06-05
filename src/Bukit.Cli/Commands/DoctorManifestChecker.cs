using YamlDotNet.RepresentationModel;
using Bukit.Config;
using Bukit.Engine;
using Bukit.Engine.Abstractions.Routing;

namespace Bukit.Cli.Commands;

internal static class DoctorManifestChecker
{
    public static void CheckManifestCompleteness(string layoutsDir, string[] allHtmlFiles)
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
            var stream = new YamlStream();
            stream.Load(new StringReader(yaml));
            if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root)
            {
                return;
            }

            if (!root.Children.TryGetValue(new YamlScalarNode("templates"), out var templatesNode) ||
                templatesNode is not YamlMappingNode templatesMap)
            {
                Console.WriteLine("⚠ bukit.templates.yaml exists but has no 'templates' section.");
                return;
            }

            var declaredPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in templatesMap.Children)
            {
                if (kv.Key is YamlScalarNode keyNode)
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

    public static void CheckUnreferencedTemplates(
        string layoutsDir,
        string[] allHtmlFiles,
        AppConfig config,
        IReadOnlyList<RouteInfo> listRoutes)
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

        if (config.Taxonomy.Template is not null)
        {
            usedTemplates.Add(config.Taxonomy.Template);
        }
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
            var layoutRefs = DoctorTemplateAnalyzer.ExtractDirectives(text, "layout");
            var includeRefs = DoctorTemplateAnalyzer.ExtractDirectives(text, "include");

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

    public static void WarnHeuristicFallback(string layoutsDir, string templateRelativePath)
    {
        var resolution = TemplateCapabilitiesResolver.ResolveListPageContent(templateRelativePath, layoutsDir, "auto");
        if (!resolution.UsedHeuristic)
        {
            return;
        }

        Console.WriteLine($"⚠ Template relies on heuristic fallback: {templateRelativePath}");
        Console.WriteLine($"  - 静态分析未能直接确认 needs_page_content，原因: {resolution.Source}");
        Console.WriteLine($"  - 当前 auto 模式回退推断为 {resolution.IncludeContent.ToString().ToLowerInvariant()}");
        Console.WriteLine($"  - 建议在 layouts/bukit.templates.yaml 中声明 needs_page_content: {resolution.IncludeContent.ToString().ToLowerInvariant()}");
    }
}
