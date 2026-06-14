using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class CoverageGateTests
{
    [Fact]
    public void CoreCoveragePartition_ExcludesExperimentalAssemblies()
    {
        var script = ReadRepoFile("scripts", "checks", "coverage.sh");

        Assert.Contains("-bukit-labs", script, StringComparison.Ordinal);
        Assert.Contains("-Bukit.Importing", script, StringComparison.Ordinal);
    }

    [Fact]
    public void CoverageScript_TracksImportingAndLabsSeparately()
    {
        var script = ReadRepoFile("scripts", "checks", "coverage.sh");

        Assert.Contains("importing_coverage_report_dir", script, StringComparison.Ordinal);
        Assert.Contains("labs_coverage_report_dir", script, StringComparison.Ordinal);
        Assert.Contains("+Bukit.Importing", script, StringComparison.Ordinal);
        Assert.Contains("+bukit-labs", script, StringComparison.Ordinal);
        Assert.Contains("IMPORTING_COVERAGE_THRESHOLD", script, StringComparison.Ordinal);
        Assert.Contains("LABS_COVERAGE_THRESHOLD", script, StringComparison.Ordinal);
        Assert.Contains("print_coverage_status \"importing\"", script, StringComparison.Ordinal);
        Assert.Contains("print_coverage_status \"labs\"", script, StringComparison.Ordinal);
    }

    [Fact]
    public void CoverageScript_UsesExplicitCoreAndCliThresholdVariables()
    {
        var script = ReadRepoFile("scripts", "checks", "coverage.sh");

        Assert.Contains("CORE_COVERAGE_THRESHOLD", script, StringComparison.Ordinal);
        Assert.Contains("CLI_COVERAGE_THRESHOLD", script, StringComparison.Ordinal);
    }

    [Fact]
    public void CoverageScript_IncludesImportingTestProject()
    {
        var script = ReadRepoFile("scripts", "checks", "coverage.sh");

        Assert.Contains("tests/Bukit.Importing.Tests/Bukit.Importing.Tests.csproj", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Solution_IncludesLabsCoverageTestProject()
    {
        var solution = ReadRepoFile("bukit.slnx");

        Assert.Contains("tests/Bukit.Labs.Cli.Tests/Bukit.Labs.Cli.Tests.csproj", solution, StringComparison.Ordinal);
    }

    [Fact]
    public void CiWorkflows_UseExplicitCoreCoverageThresholdVariable()
    {
        var ciWorkflow = ReadRepoFile(".github", "workflows", "ci.yml");
        var releaseWorkflow = ReadRepoFile(".github", "workflows", "release.yml");

        Assert.Contains("CORE_COVERAGE_THRESHOLD:", ciWorkflow, StringComparison.Ordinal);
        Assert.DoesNotContain("\n      COVERAGE_THRESHOLD:", ciWorkflow, StringComparison.Ordinal);
        Assert.Contains("CORE_COVERAGE_THRESHOLD:", releaseWorkflow, StringComparison.Ordinal);
        Assert.DoesNotContain("\n      COVERAGE_THRESHOLD:", releaseWorkflow, StringComparison.Ordinal);
    }

    private static string ReadRepoFile(params string[] segments)
    {
        var path = FindRepoRoot();
        foreach (var segment in segments)
        {
            path = Path.Combine(path, segment);
        }

        return File.ReadAllText(path);
    }

    private static string FindRepoRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        for (var i = 0; i < 8; i++)
        {
            if (File.Exists(Path.Combine(dir, "bukit.slnx")))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir) ?? string.Empty;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
