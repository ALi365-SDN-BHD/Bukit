using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class CoverageGateTests
{
    [Fact]
    public void CoverageEntrypoint_IsThinAndDelegatesToSmallSteps()
    {
        var script = ReadRepoFile("scripts", "checks", "coverage.sh");

        Assert.Contains("coverage policy", script, StringComparison.Ordinal);
        Assert.Contains("coverage project:", script, StringComparison.Ordinal);
        Assert.Contains("coverage summary", script, StringComparison.Ordinal);
        Assert.Contains("scripts/checks/coverage/list-core-projects.sh", script, StringComparison.Ordinal);
        Assert.Contains("scripts/checks/coverage/run-one.sh", script, StringComparison.Ordinal);
        Assert.Contains("scripts/checks/coverage/find-results.sh", script, StringComparison.Ordinal);
        Assert.Contains("scripts/checks/coverage/summarize.py", script, StringComparison.Ordinal);
        Assert.DoesNotContain("--collect:\"XPlat Code Coverage\"", script, StringComparison.Ordinal);
        Assert.DoesNotContain("coverage.cobertura.xml", script, StringComparison.Ordinal);
    }

    [Fact]
    public void CoverageSmallScripts_ArePresentAndSinglePurpose()
    {
        var listProjects = ReadRepoFile("scripts", "checks", "coverage", "list-core-projects.sh");
        var runOne = ReadRepoFile("scripts", "checks", "coverage", "run-one.sh");
        var findResults = ReadRepoFile("scripts", "checks", "coverage", "find-results.sh");
        var summarize = ReadRepoFile("scripts", "checks", "coverage", "summarize.py");
        var validatePolicy = ReadRepoFile("scripts", "checks", "coverage", "validate-policy.py");

        Assert.Contains("tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj", listProjects, StringComparison.Ordinal);
        Assert.Contains("tests/Bukit.Theme.Tests/Bukit.Theme.Tests.csproj", listProjects, StringComparison.Ordinal);
        Assert.DoesNotContain("tests/Bukit.Importing.Tests", listProjects, StringComparison.Ordinal);
        Assert.DoesNotContain("tests/Bukit.Labs.Cli.Tests", listProjects, StringComparison.Ordinal);
        Assert.Contains("--collect:XPlat Code Coverage", runOne, StringComparison.Ordinal);
        Assert.Contains("coverage.cobertura.xml", findResults, StringComparison.Ordinal);
        Assert.Contains("src/Bukit-Core", summarize, StringComparison.Ordinal);
        Assert.Contains("projectFloor", validatePolicy, StringComparison.Ordinal);
    }

    [Fact]
    public void CoveragePolicy_IsCoreOnlyV2Contract()
    {
        var policy = ReadRepoFile("docs", "coverage-baselines.json");
        var repoRoot = FindRepoRoot();

        Assert.Contains("\"version\": \"2.0.0\"", policy, StringComparison.Ordinal);
        Assert.Contains("\"scope\": \"core\"", policy, StringComparison.Ordinal);
        Assert.Contains("\"metric\": \"line\"", policy, StringComparison.Ordinal);
        Assert.Contains("\"overall\": 84.0", policy, StringComparison.Ordinal);
        Assert.Contains("\"projectFloor\": 70.0", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("\"cli\"", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("\"importing\"", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("\"labs\"", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("\"blocking\"", policy, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "schemas", "coverage-baselines.v2.json")));
        Assert.False(File.Exists(Path.Combine(repoRoot, "docs", "schemas", "coverage-baselines.v1.json")));
    }

    [Fact]
    public void CoverageBaselineParserContract_IsCoveredBySmallPolicyGate()
    {
        var schemaGate = ReadRepoFile("scripts", "checks", "coverage-baseline-schema.sh");
        var validatePolicy = ReadRepoFile("scripts", "checks", "coverage", "validate-policy.py");

        Assert.Contains("missing-scope", schemaGate, StringComparison.Ordinal);
        Assert.Contains("plugin-scope", schemaGate, StringComparison.Ordinal);
        Assert.Contains("overall-above-100", schemaGate, StringComparison.Ordinal);
        Assert.Contains("legacy-cli-field", schemaGate, StringComparison.Ordinal);
        Assert.Contains("legacy-labs-field", schemaGate, StringComparison.Ordinal);
        Assert.Contains("\"scope\"", validatePolicy, StringComparison.Ordinal);
        Assert.Contains("\"minimums\"", validatePolicy, StringComparison.Ordinal);
    }

    [Fact]
    public void CoverageRunsettings_DoesNotExcludeThemeAssembly()
    {
        var runsettings = ReadRepoFile("coverage.runsettings");

        Assert.Contains("<ExcludeByFile>**/obj/**/*.g.cs</ExcludeByFile>", runsettings, StringComparison.Ordinal);
        Assert.DoesNotContain("[Bukit.Theme]*", runsettings, StringComparison.Ordinal);
        Assert.DoesNotContain("<Exclude>", runsettings, StringComparison.Ordinal);
    }

    [Fact]
    public void TestSolution_KeepsRuntimeTestProjectsOnly()
    {
        var solution = ReadRepoFile("bukit-test.slnx");

        Assert.Contains("tests/Bukit.Labs.Cli.Tests/Bukit.Labs.Cli.Tests.csproj", solution, StringComparison.Ordinal);
        Assert.DoesNotContain("src/Bukit-Core/", solution, StringComparison.Ordinal);
        Assert.DoesNotContain("src/Bukit-Labs/", solution, StringComparison.Ordinal);
        Assert.DoesNotContain("src/Bukit-Plugins/", solution, StringComparison.Ordinal);
    }

    [Fact]
    public void CiWorkflows_RunCoreCoverageGateFromYamlWorkflows()
    {
        var ciWorkflow = ReadRepoFile(".github", "workflows", "ci.yaml");
        var releaseWorkflow = ReadRepoFile(".github", "workflows", "release.yaml");

        Assert.Contains("Core coverage", ciWorkflow, StringComparison.Ordinal);
        Assert.Contains("bash scripts/checks/coverage-baseline-schema.sh", ciWorkflow, StringComparison.Ordinal);
        Assert.Contains("bash scripts/checks/coverage.sh Release", ciWorkflow, StringComparison.Ordinal);
        Assert.Contains("Core coverage", releaseWorkflow, StringComparison.Ordinal);
        Assert.Contains("bash scripts/checks/coverage-baseline-schema.sh", releaseWorkflow, StringComparison.Ordinal);
        Assert.Contains("bash scripts/checks/coverage.sh Release", releaseWorkflow, StringComparison.Ordinal);
        Assert.DoesNotContain(".github/workflows/ci.yml", ciWorkflow, StringComparison.Ordinal);
        Assert.DoesNotContain("CORE_COVERAGE_THRESHOLD:", ciWorkflow, StringComparison.Ordinal);
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
