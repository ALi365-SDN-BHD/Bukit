using Bukit.Cli;
using Bukit.Cli.Commands.DocsCheck;
using Xunit;

namespace Bukit.Cli.Tests.Commands.DocsCheck;

public sealed class SkillCliCheckerTests
{
    [Fact]
    public void Check_ReportsSkillCommandsMissingFromCliReference()
    {
        using var temp = new TempSkillDocs(
            cliReference: "Run `bukit build`.\n",
            skill: "Deploy with `bukit deploy --dry-run`.\n");

        var issues = SkillCliChecker.Check(temp.DocFiles, BukitCliSpecs.CreateRegistry());

        Assert.Contains(issues, i =>
            i.Severity == Severity.Warn &&
            i.CheckType == CheckType.Skills &&
            i.Message.Contains("bukit deploy", StringComparison.Ordinal));
    }

    [Fact]
    public void Check_ReportsHardcodedMajorVersionDrift()
    {
        using var temp = new TempSkillDocs(
            cliReference: "Old example: `bukit 2.0 build`.\n",
            skill: "Build with `bukit build`.\n");

        var issues = SkillCliChecker.Check(temp.DocFiles, BukitCliSpecs.CreateRegistry());

        Assert.Contains(issues, i =>
            i.Severity == Severity.Warn &&
            i.CheckType == CheckType.Skills &&
            i.Message.Contains("Hardcoded version", StringComparison.Ordinal));
    }

    private sealed class TempSkillDocs : IDisposable
    {
        private readonly string _root;

        public TempSkillDocs(string cliReference, string skill)
        {
            _root = Path.Combine(Path.GetTempPath(), $"bukit-skill-docs-{Guid.NewGuid():N}");
            var cliDir = Path.Combine(_root, "guide", "skills", "bukit-cli-reference");
            var otherDir = Path.Combine(_root, "guide", "skills", "using-bukit");
            Directory.CreateDirectory(cliDir);
            Directory.CreateDirectory(otherDir);

            var cliPath = Path.Combine(cliDir, "SKILL.md");
            var skillPath = Path.Combine(otherDir, "SKILL.md");
            File.WriteAllText(cliPath, cliReference);
            File.WriteAllText(skillPath, skill);

            DocFiles =
            [
                new DocFile(cliPath, DocCategory.Skills),
                new DocFile(skillPath, DocCategory.Skills)
            ];
        }

        public IReadOnlyList<DocFile> DocFiles { get; }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }
}
