namespace Bukit.Cli.Commands.DocsCheck;

public static class DocFileScanner
{
    public static IReadOnlyList<DocFile> Scan(string repoRoot)
    {
        var files = new List<DocFile>();

        foreach (var path in Directory.GetFiles(repoRoot, "README*.md", SearchOption.TopDirectoryOnly))
        {
            files.Add(new DocFile(NormalizePathForOutput(path), DocCategory.Readme));
        }

        var guideDir = Path.Combine(repoRoot, "guide");
        if (Directory.Exists(guideDir))
        {
            foreach (var path in Directory.GetFiles(guideDir, "*.md", SearchOption.AllDirectories))
            {
                files.Add(new DocFile(NormalizePathForOutput(path), DocCategory.Guide));
            }
        }

        var skillsDir = Path.Combine(repoRoot, "src", "skills");
        if (Directory.Exists(skillsDir))
        {
            foreach (var path in Directory.GetFiles(skillsDir, "SKILL.md", SearchOption.AllDirectories))
            {
                files.Add(new DocFile(NormalizePathForOutput(path), DocCategory.Skills));
            }

            foreach (var name in new[] { "AGENTS.md", "CLAUDE.md", "GEMINI.md", "copilot-instructions.md" })
            {
                var agentPath = Path.Combine(skillsDir, name);
                if (File.Exists(agentPath))
                {
                    files.Add(new DocFile(NormalizePathForOutput(agentPath), DocCategory.Skills));
                }
            }

            var readmeMdPath = Path.Combine(skillsDir, "README.md");
            if (File.Exists(readmeMdPath))
            {
                files.Add(new DocFile(NormalizePathForOutput(readmeMdPath), DocCategory.Skills));
            }
        }

        return files;
    }

    private static string NormalizePathForOutput(string path)
        => path.Replace('\\', '/');
}

public sealed record DocFile(string Path, DocCategory Category);

public enum DocCategory
{
    Readme,
    Guide,
    Skills
}
