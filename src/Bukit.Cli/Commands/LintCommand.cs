using Bukit.Cli.Cli.Binding;
using Bukit.Config;
using Bukit.Shared;
using System.Linq;

namespace Bukit.Cli.Commands;

public static class LintCommand
{
    public static Task<int> RunAsync(CliBoundCommand command)
    {
        var issues = new List<string>();
        try
        {
            var resolved = ConfigPathResolver.Resolve(command.GetString("--config"), command.GetString("--site"));
            var config = ConfigLoader.Load(resolved.FullConfigPath);
            ConfigValidator.Validate(config);

            LintMarkdown(config, resolved.RootDir, issues);
        }
        catch (Exception ex) when (ex is ConfigException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            issues.Add(ex.Message);
        }

        if (issues.Count == 0)
        {
            Console.WriteLine("Lint passed.");
            return Task.FromResult(0);
        }

        Console.WriteLine($"Lint found {issues.Count} issue(s):");
        foreach (var issue in issues)
        {
            Console.WriteLine($"- {issue}");
        }

        return Task.FromResult(1);
    }

    private static void LintMarkdown(AppConfig config, string rootDir, List<string> issues)
    {
        if (!HasMarkdownSource(config.Content))
        {
            return;
        }

        var contentDir = Path.Combine(rootDir, GetFirstMarkdownDir(config.Content) ?? "content");
        if (!Directory.Exists(contentDir))
        {
            issues.Add($"Markdown content directory not found: {contentDir}");
            return;
        }

        foreach (var file in Directory.GetFiles(contentDir, "*.md", SearchOption.AllDirectories))
        {
            var markdown = File.ReadAllText(file);
            if (!HasTitle(markdown))
            {
                issues.Add($"{Path.GetRelativePath(rootDir, file)} is missing front matter title and first-level heading.");
            }
        }
    }

    private static bool HasTitle(string markdown)
    {
        var normalized = markdown.Replace("\r\n", "\n");
        if (normalized.StartsWith("---\n", StringComparison.Ordinal))
        {
            var lines = normalized.Split('\n');
            for (var i = 1; i < lines.Length; i++)
            {
                if (lines[i].Trim() == "---")
                {
                    break;
                }

                if (lines[i].TrimStart().StartsWith("title:", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return normalized.Split('\n').Any(line => line.TrimStart().StartsWith("# ", StringComparison.Ordinal));
    }

    private static bool HasMarkdownSource(ContentConfig content)
    {
        if (content.Sources is null) return false;
        return content.Sources.Any(s =>
            s.Type.Equals("markdown", StringComparison.OrdinalIgnoreCase) ||
            s.Markdown is not null);
    }

    private static string? GetFirstMarkdownDir(ContentConfig content)
    {
        if (content.Sources is null) return null;
        var source = content.Sources.FirstOrDefault(s =>
            s.Type.Equals("markdown", StringComparison.OrdinalIgnoreCase) ||
            s.Markdown is not null);
        return source?.Markdown?.Dir;
    }
}
