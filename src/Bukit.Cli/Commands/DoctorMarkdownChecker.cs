using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;

namespace Bukit.Cli.Commands;

internal static class DoctorMarkdownChecker
{
    internal static void CheckMarkdownFrontMatter(DoctorCommand.DoctorContext ctx)
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

    internal static void CheckMarkdownSyntax(DoctorCommand.DoctorContext ctx)
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

    internal static void CheckMarkdownEmptyBody(DoctorCommand.DoctorContext ctx)
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
}
