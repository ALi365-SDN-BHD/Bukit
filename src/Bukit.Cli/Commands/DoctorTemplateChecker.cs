using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Text;

namespace Bukit.Cli.Commands;

internal static class DoctorTemplateChecker
{
    internal static void CheckHardcodedUrls(DoctorCommand.DoctorContext ctx)
    {
        var issues = new List<string>();
        foreach (var file in ctx.AllHtmlFiles)
        {
            var text = File.ReadAllText(file);
            var relative = ToRelativeTemplatePath(ctx.LayoutsDir, file);

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

    internal static void CheckHardcodedText(DoctorCommand.DoctorContext ctx)
    {
        var issues = new List<string>();
        var copyrightRegex = new Regex(@"©\s*20\d{2}", RegexOptions.IgnoreCase);
        var copyrightRegex2 = new Regex(@"Copyright\s+20\d{2}", RegexOptions.IgnoreCase);

        foreach (var file in ctx.AllHtmlFiles)
        {
            var text = File.ReadAllText(file);
            var relative = ToRelativeTemplatePath(ctx.LayoutsDir, file);

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

    internal static string RemoveScribanBlocks(string text)
    {
        var result = Regex.Replace(text, @"\{\{.*?\}\}", " ", RegexOptions.Singleline);
        result = Regex.Replace(result, @"\{%[^%]*?%\}", " ", RegexOptions.Singleline);
        return result;
    }

    internal static string RemoveHtmlComments(string text)
    {
        return Regex.Replace(text, @"<!--.*?-->", " ", RegexOptions.Singleline);
    }

    internal static string RemoveTagContent(string text, string tag)
    {
        return Regex.Replace(text, $@"<{tag}\b[^>]*>.*?</{tag}>", " ", RegexOptions.Singleline | RegexOptions.IgnoreCase);
    }

    internal static string ExtractHtmlText(string text)
    {
        text = Regex.Replace(text, @"<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    internal static void CheckIncludeExistence(DoctorCommand.DoctorContext ctx)
    {
        Console.WriteLine("--- Include file existence check ---");
        var issues = 0;
        foreach (var file in ctx.AllHtmlFiles)
        {
            var text = File.ReadAllText(file);
            var includeRefs = DoctorTemplateAnalyzer.ExtractDirectives(text, "include");
            foreach (var includePath in includeRefs)
            {
                var resolved = Path.Combine(ctx.LayoutsDir, includePath);
                if (!File.Exists(resolved))
                {
                    var relative = ToRelativeTemplatePath(ctx.LayoutsDir, file);
                    Console.WriteLine($"  ⚠ {relative}: include \"{includePath}\" not found");
                    issues++;
                }
            }
        }
        if (issues == 0) Console.WriteLine("  ✔ All includes exist");
    }

    internal static void CheckTemplateContextCorrectness(DoctorCommand.DoctorContext ctx)
    {
        Console.WriteLine("--- Template context correctness check ---");
        var issues = 0;

        var listRouteTemplates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (ctx.Config.Site.Collections is { Count: > 0 })
        {
            foreach (var kv in ctx.Config.Site.Collections)
            {
                if (!string.IsNullOrWhiteSpace(kv.Value.ListTemplate))
                    listRouteTemplates.Add(kv.Value.ListTemplate);
            }
        }

        var taxonomyTemplates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(ctx.Config.Taxonomy.Template))
            taxonomyTemplates.Add(ctx.Config.Taxonomy.Template);
        if (!string.IsNullOrWhiteSpace(ctx.Config.Taxonomy.TermTemplate))
            taxonomyTemplates.Add(ctx.Config.Taxonomy.TermTemplate);
        if (!string.IsNullOrWhiteSpace(ctx.Config.Taxonomy.IndexTemplate))
            taxonomyTemplates.Add(ctx.Config.Taxonomy.IndexTemplate);

        foreach (var file in ctx.AllHtmlFiles)
        {
            var relative = ToRelativeTemplatePath(ctx.LayoutsDir, file);
            var text = File.ReadAllText(file);
            if (listRouteTemplates.Contains(relative) && text.Contains("page.title"))
            {
                Console.WriteLine($"  ⚠ {relative}: list template uses 'page.title' — use 'this.title' instead");
                issues++;
            }
            if (taxonomyTemplates.Contains(relative) && text.Contains("page.title"))
            {
                Console.WriteLine($"  ⚠ {relative}: taxonomy template uses 'page.title' — use 'term.title' or 'this.title' instead");
                issues++;
            }
        }

        if (issues == 0) Console.WriteLine("  ✔ Template context usage appears correct");
    }

    internal static void CheckThemeParamsConsistency(DoctorCommand.DoctorContext ctx)
    {
        var themeParams = ctx.Config.Theme.Params;

        var allContent = new StringBuilder();
        foreach (var file in ctx.AllHtmlFiles)
        {
            DoctorTemplateAnalyzer.AppendFileOrWarn(file, allContent);
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

    private static string ToRelativeTemplatePath(string layoutsDir, string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return filePath;
        }

        if (!Path.IsPathRooted(filePath))
        {
            return filePath.Replace('\\', '/');
        }

        return Path.GetRelativePath(layoutsDir, filePath)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace('\\', '/');
    }
}
