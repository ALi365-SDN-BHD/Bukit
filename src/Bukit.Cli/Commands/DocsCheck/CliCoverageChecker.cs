using System.Text.RegularExpressions;
using Bukit.Cli.Shared.Cli.Metadata;

namespace Bukit.Cli.Commands.DocsCheck;

public static partial class CliCoverageChecker
{
    [GeneratedRegex(@"\bbukit\s+([a-z][a-z0-9-]*(?:\s+[a-z][a-z0-9-]+)?)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BukitCommandRegex();

    [GeneratedRegex(@"dotnet\s+run.*--project\s+\S*Bukit\.Cli\S*.*--\s+([a-z][a-z0-9-]*(?:\s+[a-z][a-z0-9-]+)?)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DotnetRunRegex();

    public static IReadOnlyList<DocsIssue> Check(string repoRoot, IReadOnlyList<DocFile> docFiles, CliCommandRegistry registry)
    {
        var issues = new List<DocsIssue>();

        var canonicalPaths = new HashSet<string>(
            CommandPathExtractor.ExtractAllCommandPaths(registry),
            StringComparer.OrdinalIgnoreCase);

        var topLevelCommands = new HashSet<string>(
            registry.Commands.Select(c => c.Name),
            StringComparer.OrdinalIgnoreCase);

        var coveredPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var bukitRegex = BukitCommandRegex();
        var dotnetRegex = DotnetRunRegex();

        foreach (var docFile in docFiles)
        {
            string text;
            try
            {
                text = File.ReadAllText(docFile.Path);
            }
            catch
            {
                continue;
            }

            var lines = text.Replace("\r\n", "\n").Split('\n');

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var lineNumber = i + 1;

                ScanLine(bukitRegex, line, docFile.Path, lineNumber, canonicalPaths, topLevelCommands, coveredPaths, issues);
                ScanLine(dotnetRegex, line, docFile.Path, lineNumber, canonicalPaths, topLevelCommands, coveredPaths, issues);
            }
        }

        foreach (var path in canonicalPaths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            if (!coveredPaths.Contains(path))
            {
                issues.Add(new DocsIssue(
                    repoRoot,
                    0,
                    Severity.Warn,
                    CheckType.Cli,
                    $"CLI command '{path}' has no documentation coverage"));
            }
        }

        return issues;
    }

    private static void ScanLine(Regex regex, string line, string filePath, int lineNumber, HashSet<string> canonicalPaths, HashSet<string> topLevelCommands, HashSet<string> coveredPaths, List<DocsIssue> issues)
    {
        foreach (Match match in regex.Matches(line))
        {
            var rawPath = match.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                continue;
            }

            var words = rawPath.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0 || !topLevelCommands.Contains(words[0]))
            {
                continue;
            }

            var matchedPath = TryMatchPath(words, canonicalPaths);
            if (matchedPath is null)
            {
                continue;
            }

            coveredPaths.Add(matchedPath);
        }
    }

    private static string? TryMatchPath(string[] words, HashSet<string> canonicalPaths)
    {
        for (var len = words.Length; len >= 1; len--)
        {
            var candidate = string.Join(" ", words.Take(len));
            if (canonicalPaths.Contains(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
