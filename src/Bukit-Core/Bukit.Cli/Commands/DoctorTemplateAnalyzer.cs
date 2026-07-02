using System.Text.RegularExpressions;
using Bukit.Config;
using Bukit.Engine;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Plugins;

namespace Bukit.Cli.Commands;

internal static class DoctorTemplateAnalyzer
{
    internal static IReadOnlyList<string> CollectExplicitConfiguredTemplates(AppConfig config)
    {
        var templates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (config.Site.Collections is not null)
        {
            foreach (var (_, collection) in config.Site.Collections)
            {
                Add(collection.Template);
                Add(collection.ListTemplate);
                if (collection.FilteredLists is not null)
                {
                    foreach (var filter in collection.FilteredLists)
                    {
                        Add(filter.ListTemplate);
                    }
                }
            }
        }

        Add(config.Theme.StaticTemplate);
        if (config.Taxonomy.Kinds is not null)
        {
            foreach (var kind in config.Taxonomy.Kinds)
            {
                Add(kind.Template);
                Add(kind.IndexTemplate);
                Add(kind.TermTemplate);
            }
        }

        return templates.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();

        void Add(string? template)
        {
            if (!string.IsNullOrWhiteSpace(template))
            {
                templates.Add(template.Trim());
            }
        }
    }

    internal static IReadOnlyList<string> CollectMissingUsedTemplates(
        string layoutsDir,
        IReadOnlyList<RoutedContentDocument> routed,
        IReadOnlyList<RouteInfo> listRoutes,
        IReadOnlyList<string>? pluginRequirementTemplates = null)
    {
        return routed
            .Select(x => x.Route.Template)
            .Concat(listRoutes.Select(x => x.Template))
            .Concat(pluginRequirementTemplates ?? Array.Empty<string>())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim().Replace('\\', '/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(t => !File.Exists(Path.Combine(layoutsDir, t.Replace('/', Path.DirectorySeparatorChar))))
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static IReadOnlyList<string> CollectPluginRequirementTemplates(
        BuildContext context,
        ThemeTemplateResolver templateResolver)
    {
        var templates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kind in PluginRunner.CollectTemplateRequirementKinds(context))
        {
            templates.Add(templateResolver.ResolveKindTemplate(kind));
        }

        return templates.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }

    internal static void AnalyzeTemplateChains(string layoutsDir, string[] allHtmlFiles)
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
            foreach (Match match in Regex.Matches(text, pattern))
            {
                results.Add(match.Groups[1].Value);
            }
        }

        return results;
    }

    internal static void CheckTemplateVariables(string layoutsDir)
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

    internal static void AppendFileOrWarn(string file, System.Text.StringBuilder dst)
    {
        try { dst.Append(File.ReadAllText(file)); }
        catch (Exception ex) { Console.WriteLine($"⚠ Failed to read {file}: {ex.Message}"); }
    }

    internal static int CountOpenings(string text, string pattern)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += pattern.Length;
        }

        return count;
    }
}
