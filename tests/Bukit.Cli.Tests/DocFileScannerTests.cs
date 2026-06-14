using Bukit.Cli.Commands.DocsCheck;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class DocFileScannerTests : IDisposable
{
    private readonly string _tempDir;

    public DocFileScannerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bukit-doc-file-scanner-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_tempDir, recursive: true);
    }

    [Fact]
    public void Scan_FindsReadmesGuideFilesAndSkillsArtifacts()
    {
        File.WriteAllText(Path.Combine(_tempDir, "README.md"), "# Root");
        File.WriteAllText(Path.Combine(_tempDir, "README.zh.md"), "# Root ZH");

        var guideDir = Path.Combine(_tempDir, "guide", "dev");
        Directory.CreateDirectory(guideDir);
        File.WriteAllText(Path.Combine(guideDir, "cli.md"), "# CLI");

        var skillsDir = Path.Combine(_tempDir, "src", "skills", "example");
        Directory.CreateDirectory(skillsDir);
        File.WriteAllText(Path.Combine(skillsDir, "SKILL.md"), "# Skill");

        var skillsRoot = Path.Combine(_tempDir, "src", "skills");
        File.WriteAllText(Path.Combine(skillsRoot, "AGENTS.md"), "# Agents");
        File.WriteAllText(Path.Combine(skillsRoot, "README.md"), "# Skills");

        var files = DocFileScanner.Scan(_tempDir);

        Assert.Contains(files, file => file.Path.EndsWith("README.md", StringComparison.Ordinal) && file.Category == DocCategory.Readme);
        Assert.Contains(files, file => file.Path.EndsWith("README.zh.md", StringComparison.Ordinal) && file.Category == DocCategory.Readme);
        Assert.Contains(files, file => file.Path.EndsWith("guide/dev/cli.md", StringComparison.Ordinal) && file.Category == DocCategory.Guide);
        Assert.Contains(files, file => file.Path.EndsWith("src/skills/example/SKILL.md", StringComparison.Ordinal) && file.Category == DocCategory.Skills);
        Assert.Contains(files, file => file.Path.EndsWith("src/skills/AGENTS.md", StringComparison.Ordinal) && file.Category == DocCategory.Skills);
        Assert.Contains(files, file => file.Path.EndsWith("src/skills/README.md", StringComparison.Ordinal) && file.Category == DocCategory.Skills);
    }

    [Fact]
    public void Scan_WithoutGuideOrSkillsDirectories_ReturnsOnlyTopLevelReadmes()
    {
        File.WriteAllText(Path.Combine(_tempDir, "README.md"), "# Root");

        var files = DocFileScanner.Scan(_tempDir);

        Assert.Single(files);
        Assert.Equal(DocCategory.Readme, files[0].Category);
        Assert.EndsWith("README.md", files[0].Path, StringComparison.Ordinal);
    }
}
