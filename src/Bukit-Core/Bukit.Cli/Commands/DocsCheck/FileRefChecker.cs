using System.Text.RegularExpressions;

namespace Bukit.Cli.Commands.DocsCheck;

public static class FileRefChecker
{
    private static readonly Regex BacktickRefPattern = new(
        @"`([^`]+)`",
        RegexOptions.Compiled);

    private static readonly Regex StandalonePathPattern = new(
        @"\b((?:src|guide|scripts|tests|docs|\.github|examples|themes)/[^\s""'<\(\)\[\]\{\}]+\.\w+)\b",
        RegexOptions.Compiled);

    public static IReadOnlyList<DocsIssue> Check(string repoRoot, IReadOnlyList<DocFile> docFiles)
    {
        var issues = new List<DocsIssue>();
        var seen = new HashSet<(string, int, string)>();

        foreach (var docFile in docFiles)
        {
            // Skip AI context files — they use relative paths within the ai/ subtree
            if (docFile.Path.Contains("/guide/ai/", StringComparison.OrdinalIgnoreCase))
                continue;

            var lines = File.ReadAllLines(docFile.Path);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var lineNumber = i + 1;

                foreach (Match match in BacktickRefPattern.Matches(line))
                {
                    var content = match.Groups[1].Value;
                    if (IsFilePath(content) && !IsUrl(content))
                    {
                        var key = (docFile.Path, lineNumber, content);
                        if (seen.Add(key))
                        {
                            CheckPath(repoRoot, docFile.Path, lineNumber, content, issues);
                        }
                    }
                }

                foreach (Match match in StandalonePathPattern.Matches(line))
                {
                    var content = match.Groups[1].Value;
                    if (!IsUrl(content))
                    {
                        var key = (docFile.Path, lineNumber, content);
                        if (seen.Add(key))
                        {
                            CheckPath(repoRoot, docFile.Path, lineNumber, content, issues);
                        }
                    }
                }
            }
        }

        return issues;
    }

    private static readonly HashSet<string> ThemeRelativePrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "layouts/", "partials/", "pages/", "assets/", "static/",
    };

    private static readonly HashSet<string> BuildOutputPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "dist/", "feed/", ".bukit/", ".cache/", "_debug/",
    };

    private static readonly HashSet<string> GeneratedFilePrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "notion-seed/", "_debug/", "docs/research/", "docs/design-references/",
        "docs/intent.md", "docs/ai_guide.md", "docs/getting-started.md", "docs/configuration.md",
        "schemas/",
    };

    private static readonly HashSet<string> ProjectExamplePrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "content/", "data/", "sites/", "zh-CN/", "en-US/", "examples/", "themes/",
    };

    private static readonly HashSet<string> ThirdPartyPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "src/Scriban/",
    };

    private static readonly HashSet<string> GuideAiRelativePrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "docs/ai-demo-to-bukit/", "docs/notion_schema.md", "skills/",
    };

    private static readonly HashSet<string> RepoSourcePrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "src/", "guide/", "scripts/", "tests/", "docs/", ".github/", "examples/", "themes/",
    };

    private static bool IsFilePath(string path)
    {
        return path.Contains('/') && Path.HasExtension(path);
    }

    private static bool IsDirectoryPath(string path)
    {
        return path.Contains('/') && !Path.HasExtension(path) && !path.EndsWith("/");
    }

    private static bool IsUrl(string path)
    {
        return path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldSkipReferencedPath(string path)
    {
        if (path.Contains('<') || path.Contains('{'))
            return true;

        if (path.StartsWith('/'))
            return true;

        if (path.Contains('*'))
            return true;

        if (path.StartsWith("bash ") || path.StartsWith("guide/ai/"))
            return true;

        // CSS class paths like .animate-in/.animate-visible
        if (path.StartsWith('.'))
            return true;

        // Paths containing CJK characters (likely non-existent translated filenames)
        if (path.Any(c => c >= 0x4E00 && c <= 0x9FFF))
            return true;

        // Paths referencing docs/ for generated/expected content (not repo paths)
        if (path.StartsWith("docs/"))
            return true;

        foreach (var prefix in ThemeRelativePrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        foreach (var prefix in BuildOutputPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        foreach (var prefix in GeneratedFilePrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        foreach (var prefix in ProjectExamplePrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        foreach (var prefix in ThirdPartyPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        foreach (var prefix in GuideAiRelativePrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Handle patterns like "docs/index.html" (docs/ prefix outside RepoSourcePrefixes)
        if (path.StartsWith("docs/", StringComparison.OrdinalIgnoreCase))
            return true;

        if (IsBuildOutputPath(path))
            return true;

        // YAML value patterns like "site.url: https://example.com" or "template: pages/post.html"
        if (path.Contains(": "))
            return true;

        // Command patterns like "dotnet publish src/..." or "bash scripts/..."
        if (path.StartsWith("dotnet ") || path.StartsWith("pwsh ") || path.StartsWith("--config "))
            return true;

        // Script commands with arguments like "scripts/test-all.sh Release"
        if (path.StartsWith("scripts/") && path.Count(c => c == ' ') > 0)
            return true;

        // Example path placeholder
        if (path.Contains("path/to/"))
            return true;

        // Config field concatenation with '/' like "site.name/site.title/site.base_url"
        if (path.Contains('/') && !path.StartsWith("src/") && !path.StartsWith("guide/")
            && !path.StartsWith("scripts/") && !path.StartsWith("tests/") && !path.StartsWith("docs/"))
        {
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 2)
            {
                // Check if most segments look like config field paths (contain dots)
                var fieldLikeCount = segments.Count(s => s.Contains('.'));
                if (fieldLikeCount >= 2)
                    return true;
            }
        }

        // URL-like patterns containing "=https://"
        if (path.Contains("=http"))
            return true;

        // Paths with brackets indicating shell commands
        if (path.Contains('[') || path.Contains(']'))
            return true;

        // Paths with shell redirect
        if (path.Contains(" > ") || path.Contains(" >~"))
            return true;

        return false;
    }

    private static string SanitizePath(string path)
    {
        // Strip surrounding double or single quotes
        if (path.Length > 1)
        {
            if ((path[0] == '"' && path[^1] == '"')
                || (path[0] == '\'' && path[^1] == '\''))
            {
                path = path[1..^1];
            }
        }

        // Strip leading ./
        if (path.StartsWith("./"))
            path = path[2..];

        // Strip trailing non-ASCII content (e.g., Chinese text after a backtick path)
        var asciiEnd = path.Length;
        while (asciiEnd > 0 && path[asciiEnd - 1] > 127)
            asciiEnd--;
        if (asciiEnd < path.Length)
            path = path[..asciiEnd];

        // Strip trailing backtick
        while (path.EndsWith('`'))
            path = path[..^1];

        // Strip fullwidth colon that may appear between path and Chinese text
        if (path.EndsWith('：'))
            path = path[..^1];

        // Strip trailing punctuation that isn't part of the path
        path = path.TrimEnd('.', ',', ';', ':', ')', ']', '}');

        // Trim trailing Chinese characters (、，）
        path = path.TrimEnd('、', '，', '）', '：');

        // If path contains Chinese enumeration separator (、), split and take first
        var enumIdx = path.IndexOf('、');
        if (enumIdx > 0)
            path = path[..enumIdx];

        // If path contains fullwidth parenthesis （, split and take before it
        var parenIdx = path.IndexOf('（');
        if (parenIdx > 0)
            path = path[..parenIdx];

        // If path contains fullwidth colon ：, split and take before it
        var colonIdx = path.IndexOf('：');
        if (colonIdx > 0)
            path = path[..colonIdx];

        // Strip trailing backtick that may remain after Chinese separator splitting
        // (StandalonePathPattern can concatenate paths like `a.cs`、`b.cs` into a single match;
        //  splitting at 、 leaves a trailing backtick: a.cs`)
        while (path.EndsWith('`'))
            path = path[..^1];

        return path;
    }

    private static bool IsBuildOutputPath(string path)
    {
        if (path.EndsWith("/index.html", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var prefix in RepoSourcePrefixes)
            {
                if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return true;
        }
        return false;
    }

    private static void CheckPath(string repoRoot, string docFilePath, int lineNumber, string path, List<DocsIssue> issues)
    {
        path = SanitizePath(path);

        if (string.IsNullOrEmpty(path))
            return;

        if (!IsFilePath(path) && !IsDirectoryPath(path))
            return;

        if (ShouldSkipReferencedPath(path))
            return;

        var resolvedPath = Path.GetFullPath(Path.Combine(repoRoot, path));
        if (!File.Exists(resolvedPath) && !Directory.Exists(resolvedPath))
        {
            issues.Add(new DocsIssue(
                docFilePath,
                lineNumber,
                Severity.Error,
                CheckType.FileRefs,
                $"File reference not found: {path}"));
        }
    }
}
