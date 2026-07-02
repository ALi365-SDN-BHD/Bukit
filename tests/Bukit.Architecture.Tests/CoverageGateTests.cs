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
        Assert.Contains("tests/Bukit.Theme.Tests remains intentionally outside the coverage gate", script, StringComparison.Ordinal);
    }

    [Fact]
    public void CoverageScript_CollectsEveryCoverageProjectIntoIsolatedResultsDirectories()
    {
        var script = ReadRepoFile("scripts", "checks", "coverage.sh");

        Assert.Contains("coverage_solution_test_projects=(", script, StringComparison.Ordinal);
        Assert.Contains("tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj", script, StringComparison.Ordinal);
        Assert.Contains("tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj", script, StringComparison.Ordinal);
        Assert.Contains("tests/Bukit.Rendering.Tests/Bukit.Rendering.Tests.csproj", script, StringComparison.Ordinal);
        Assert.Contains("tests/Bukit.Routing.Tests/Bukit.Routing.Tests.csproj", script, StringComparison.Ordinal);
        Assert.Contains("tests/Bukit.Shared.Tests/Bukit.Shared.Tests.csproj", script, StringComparison.Ordinal);
        Assert.Contains("project_results_dir", script, StringComparison.Ordinal);
        Assert.Contains("expected_coverage_file_count", script, StringComparison.Ordinal);
        Assert.Contains("-mindepth 3 -maxdepth 3", script, StringComparison.Ordinal);
    }

    [Fact]
    public void CoverageScript_WritesCompleteSummaryContract()
    {
        var script = ReadRepoFile("scripts", "checks", "coverage.sh");

        foreach (var key in new[]
        {
            "overall",
            "core",
            "cli",
            "importing",
            "labs",
            "core_blocking",
            "cli_blocking",
            "importing_blocking",
            "labs_blocking",
            "core_baseline",
            "cli_baseline",
            "importing_baseline",
            "labs_baseline",
            "core_threshold",
            "cli_threshold",
            "importing_threshold",
            "labs_threshold"
        })
        {
            Assert.Contains($"printf \"{key}=%s\\n\"", script, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CoverageBaselineParserContract_IsCoveredBySchemaGate()
    {
        var schemaGate = ReadRepoFile("scripts", "checks", "coverage-baseline-schema.sh");

        Assert.Contains("core-blocking-false", schemaGate, StringComparison.Ordinal);
        Assert.Contains("cli-blocking-false", schemaGate, StringComparison.Ordinal);
        Assert.Contains("missing-importing-baseline", schemaGate, StringComparison.Ordinal);
        Assert.Contains("missing-labs-baseline", schemaGate, StringComparison.Ordinal);
        Assert.Contains("core-minimum-above-100", schemaGate, StringComparison.Ordinal);
        Assert.Contains("extra-core-property", schemaGate, StringComparison.Ordinal);
    }

    [Fact]
    public void TestSolution_IncludesLabsCoverageTestProject()
    {
        var solution = ReadRepoFile("bukit-test.slnx");

        Assert.Contains("tests/Bukit.Labs.Cli.Tests/Bukit.Labs.Cli.Tests.csproj", solution, StringComparison.Ordinal);
        Assert.DoesNotContain("src/Bukit-Core/", solution, StringComparison.Ordinal);
        Assert.DoesNotContain("src/Bukit-Labs/", solution, StringComparison.Ordinal);
        Assert.DoesNotContain("src/Bukit-Plugins/", solution, StringComparison.Ordinal);
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
            if (File.Exists(Path.Combine(dir, "bukit-core.slnx")))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir) ?? string.Empty;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
