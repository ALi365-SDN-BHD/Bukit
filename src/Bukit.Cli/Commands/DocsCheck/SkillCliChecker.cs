using System.Text.RegularExpressions;
using Bukit.Cli.Cli.Metadata;

namespace Bukit.Cli.Commands.DocsCheck;

public static class SkillCliChecker
{
    public static IReadOnlyList<DocsIssue> Check(IReadOnlyList<DocFile> docFiles, CliCommandRegistry registry)
    {
        var issues = new List<DocsIssue>();

        var cliRefFile = docFiles.FirstOrDefault(f =>
            f.Path.Replace('\\', '/').EndsWith("bukit-cli-reference/SKILL.md", StringComparison.OrdinalIgnoreCase));

        if (cliRefFile == null)
            return issues;

        var topLevelCommands = new HashSet<string>(
            registry.Commands.Select(c => c.Name),
            StringComparer.OrdinalIgnoreCase);

        var cliRefContent = File.ReadAllText(cliRefFile.Path);
        var cliRefCommands = ExtractCommands(cliRefContent, topLevelCommands);

        CheckVersionDrift(issues, cliRefFile.Path, cliRefContent);

        foreach (var docFile in docFiles)
        {
            if (docFile.Category != DocCategory.Skills)
                continue;
            if (docFile == cliRefFile)
                continue;

            var content = File.ReadAllText(docFile.Path);
            var commands = ExtractCommands(content, topLevelCommands);

            foreach (var cmd in commands)
            {
                if (!cliRefCommands.Contains(cmd))
                {
                    var line = FindLineNumber(content, cmd);
                    issues.Add(new DocsIssue(
                        docFile.Path,
                        line,
                        Severity.Warn,
                        CheckType.Skills,
                        $"CLI command 'bukit {cmd}' is not documented in bukit-cli-reference/SKILL.md"));
                }
            }
        }

        return issues;
    }

    private static HashSet<string> ExtractCommands(string content, HashSet<string> topLevelCommands)
    {
        var commands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var regex = new Regex(@"\bbukit\s+([a-z][a-z0-9-]*(?:\s+[a-z][a-z0-9-]+)?)", RegexOptions.IgnoreCase);
        foreach (Match match in regex.Matches(content))
        {
            var rawPath = match.Groups[1].Value.Trim();
            var words = rawPath.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0 || !topLevelCommands.Contains(words[0]))
            {
                continue;
            }

            commands.Add(rawPath);
        }
        return commands;
    }

    private static int FindLineNumber(string content, string command)
    {
        var pattern = $"bukit\\s+{Regex.Escape(command)}";
        var match = Regex.Match(content, pattern, RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return content[..match.Index].Count(c => c == '\n') + 1;
        }
        return 0;
    }

    private static void CheckVersionDrift(List<DocsIssue> issues, string filePath, string content)
    {
        var regex = new Regex(@"bukit\s+2\.", RegexOptions.IgnoreCase);
        var lines = content.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (regex.IsMatch(lines[i]))
            {
                issues.Add(new DocsIssue(
                    filePath,
                    i + 1,
                    Severity.Warn,
                    CheckType.Skills,
                    "Hardcoded version 'bukit 2.' found — may be stale, verify against current release version"));
            }
        }
    }
}
