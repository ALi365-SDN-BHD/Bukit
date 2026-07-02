using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Cli.Shared.Cli.Metadata;

namespace Bukit.Cli.Commands.DocsCheck;

public static class DocsCheckCommand
{
    public static Task<int> RunAsync(CliBoundCommand command)
    {
        var subCommand = command.GetArgument(0);
        if (subCommand is not "check")
        {
            Console.Error.WriteLine(subCommand is null
                ? "Usage: bukit docs check [options]"
                : $"Unknown subcommand: docs {subCommand}");
            PrintHelp();
            return Task.FromResult(2);
        }

        if (command.GetBool("--help") || command.GetBool("-h"))
        {
            PrintHelp();
            return Task.FromResult(0);
        }

        var runCli = command.GetBool("--cli");
        var runConfigFields = command.GetBool("--config-fields");
        var runFileRefs = command.GetBool("--file-refs");
        var runExamples = command.GetBool("--examples");
        var runSkills = command.GetBool("--skills");
        var runAll = !runCli && !runConfigFields && !runFileRefs && !runExamples && !runSkills;

        if (runAll)
        {
            runCli = true;
            runConfigFields = true;
            runFileRefs = true;
            runExamples = true;
            runSkills = true;
        }

        var repoRoot = FindRepoRoot();
        if (repoRoot is null)
        {
            Console.Error.WriteLine("Error: Could not find repository root (looking for bukit-core.slnx)");
            return Task.FromResult(1);
        }

        var docFiles = DocFileScanner.Scan(repoRoot);
        var registry = BukitCliSpecs.CreateRegistry();
        var issues = new List<DocsIssue>();

        if (runCli)
        {
            issues.AddRange(CliCoverageChecker.Check(repoRoot, docFiles, registry));
        }

        if (runConfigFields)
        {
            issues.AddRange(ConfigFieldChecker.Check(docFiles));
        }

        if (runFileRefs)
        {
            issues.AddRange(FileRefChecker.Check(repoRoot, docFiles));
        }

        if (runExamples)
        {
            issues.AddRange(ExampleParserChecker.Check(docFiles, registry));
        }

        if (runSkills)
        {
            issues.AddRange(SkillCliChecker.Check(docFiles, registry));
        }

        if (issues.Count == 0)
        {
            Console.WriteLine("OK docs check passed, 0 issues");
            return Task.FromResult(0);
        }

        foreach (var group in issues.GroupBy(i => i.CheckType).OrderBy(g => g.Key))
        {
            Console.WriteLine($"--- {group.Key} ---");
            foreach (var issue in group)
            {
                var severity = issue.Severity == Severity.Error ? "ERROR" : "WARN";
                var relative = string.IsNullOrEmpty(issue.FilePath)
                    ? "."
                    : Path.GetRelativePath(repoRoot, issue.FilePath).Replace('\\', '/');
                var location = issue.Line > 0 ? $"{relative}:{issue.Line}" : relative;
                Console.WriteLine($"[{severity}] {location}: {issue.Message}");
            }

            Console.WriteLine();
        }

        var errorCount = issues.Count(i => i.Severity == Severity.Error);
        var warnCount = issues.Count(i => i.Severity == Severity.Warn);
        Console.WriteLine($"docs check: errors={errorCount} warnings={warnCount}");

        return Task.FromResult(errorCount > 0 ? 1 : 0);
    }

    private static string? FindRepoRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        for (var i = 0; i < 8; i++)
        {
            if (string.IsNullOrEmpty(dir)) break;
            if (File.Exists(Path.Combine(dir, "bukit-core.slnx")))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        return null;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Usage: bukit docs check [options]");
        Console.WriteLine();
        Console.WriteLine("Check documentation consistency across README, guide/dev, and src/skills.");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --cli            Check CLI command coverage in docs");
        Console.WriteLine("  --config-fields  Check site.yaml field references in docs");
        Console.WriteLine("  --file-refs      Check file path references in docs");
        Console.WriteLine("  --examples       Check README examples parse correctly");
        Console.WriteLine("  --skills         Check skill files reference cli-reference consistently");
        Console.WriteLine();
        Console.WriteLine("Without options, all checks run.");
    }
}
