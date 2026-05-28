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

    private static bool IsFilePath(string path)
    {
        return path.Contains('/') && Path.HasExtension(path);
    }

    private static bool IsUrl(string path)
    {
        return path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    private static void CheckPath(string repoRoot, string docFilePath, int lineNumber, string path, List<DocsIssue> issues)
    {
        var resolvedPath = Path.GetFullPath(Path.Combine(repoRoot, path));
        if (!File.Exists(resolvedPath))
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
