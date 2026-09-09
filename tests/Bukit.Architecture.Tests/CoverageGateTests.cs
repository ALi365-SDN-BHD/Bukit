using System.Xml.Linq;
using YamlDotNet.RepresentationModel;
using Xunit;
using Xunit.Sdk;

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
        Assert.Contains("tests/Bukit.Content.Notion.Tests/Bukit.Content.Notion.Tests.csproj", listProjects, StringComparison.Ordinal);
        Assert.Contains("tests/Bukit.Notion.Tests/Bukit.Notion.Tests.csproj", listProjects, StringComparison.Ordinal);
        Assert.Contains("tests/Bukit.Theme.Tests/Bukit.Theme.Tests.csproj", listProjects, StringComparison.Ordinal);
        Assert.DoesNotContain("SilkroadBiz23", listProjects, StringComparison.Ordinal);
        Assert.DoesNotContain("silkroad_biz23", listProjects, StringComparison.Ordinal);
        Assert.DoesNotContain("tests/Bukit.Importing.Tests", listProjects, StringComparison.Ordinal);
        Assert.DoesNotContain("tests/Bukit.Labs.Cli.Tests", listProjects, StringComparison.Ordinal);
        Assert.Contains("--collect:XPlat Code Coverage", runOne, StringComparison.Ordinal);
        Assert.Equal(13, listProjects.Split('\n').Count(line => line.StartsWith("emit tests/", StringComparison.Ordinal)));
        Assert.Equal(1, runOne.Split("dotnet test", StringSplitOptions.None).Length - 1);
        Assert.Contains("trx;LogFileName=tests.trx", runOne, StringComparison.Ordinal);
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
        Assert.Contains("overall-nan", schemaGate, StringComparison.Ordinal);
        Assert.Contains("missing-schema", schemaGate, StringComparison.Ordinal);
        Assert.Contains("wrong-schema", schemaGate, StringComparison.Ordinal);
        Assert.Contains("legacy-cli-field", schemaGate, StringComparison.Ordinal);
        Assert.Contains("legacy-labs-field", schemaGate, StringComparison.Ordinal);
        Assert.Contains("\"scope\"", validatePolicy, StringComparison.Ordinal);
        Assert.Contains("\"minimums\"", validatePolicy, StringComparison.Ordinal);
    }

    [Fact]
    public void CoverageRunsettings_DoesNotExcludeThemeAssembly()
    {
        var runsettings = ReadRepoFile("coverage.runsettings");

        AssertCoverageRunsettingsContract(runsettings);
    }

    [Theory]
    [InlineData("**/*.Generated.cs")]
    [InlineData("**/Bukit.Theme/**/*.cs")]
    public void CoverageRunsettings_RejectsAdditionalBroadFileExclusions(string broadExclusion)
    {
        var runsettings = ReadRepoFile("coverage.runsettings");
        var mutated = runsettings.Replace(
            "        </Configuration>",
            $"          <ExcludeByFile>{broadExclusion}</ExcludeByFile>\n        </Configuration>",
            StringComparison.Ordinal);

        Assert.NotEqual(runsettings, mutated);
        Assert.ThrowsAny<XunitException>(() => AssertCoverageRunsettingsContract(mutated));
    }

    private static void AssertCoverageRunsettingsContract(string runsettings)
    {
        var document = XDocument.Parse(runsettings);
        var excludeByFile = Assert.Single(document.Descendants("ExcludeByFile"));

        Assert.Equal(
            "**/obj/**/*.g.cs,**/ThemeManifestYamlStaticContext.Generated.cs",
            excludeByFile.Value);
        Assert.DoesNotContain("[Bukit.Theme]*", runsettings, StringComparison.Ordinal);
        Assert.DoesNotContain("<Exclude>", runsettings, StringComparison.Ordinal);
    }

    [Fact]
    public void LocalArtifactScan_AllowsCoverageGateOutputOnly()
    {
        var script = ReadRepoFile("scripts", "checks", "docs", "no-local-artifacts.sh");

        Assert.Contains(@"\./TestResults/coverage/.*/coverage\.cobertura\.xml$#d", script, StringComparison.Ordinal);
        Assert.DoesNotContain(@"sed '\#^\./TestResults/coverage/#d'", script, StringComparison.Ordinal);
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

        AssertCoverageWorkflowContract(ciWorkflow, release: false);
        AssertCoverageWorkflowContract(releaseWorkflow, release: true);
        Assert.DoesNotContain("bash scripts/checks/coverage.sh Release", ciWorkflow, StringComparison.Ordinal);
        Assert.DoesNotContain("bash scripts/checks/coverage.sh Release", releaseWorkflow, StringComparison.Ordinal);
        Assert.DoesNotContain(".github/workflows/ci.yml", ciWorkflow, StringComparison.Ordinal);
        Assert.DoesNotContain("CORE_COVERAGE_THRESHOLD:", ciWorkflow, StringComparison.Ordinal);
    }

    [Fact]
    public void CoverageWorkflowContract_RejectsMiswiredProjectDependency()
    {
        var workflow = ReadRepoFile(".github", "workflows", "ci.yaml");
        var invalid = workflow.Replace(
            "    needs: coverage-plan\n",
            "    needs: fast-contracts\n",
            StringComparison.Ordinal);

        Assert.NotEqual(workflow, invalid);
        Assert.ThrowsAny<XunitException>(() => AssertCoverageWorkflowContract(invalid, release: false));
    }

    [Fact]
    public void CoverageWorkflowContract_RejectsMissingPolicyArtifact()
    {
        var workflow = ReadRepoFile(".github", "workflows", "release.yaml");
        var invalid = workflow.Replace(
            "            docs/coverage-baselines.json\n",
            "            docs/not-the-coverage-policy.json\n",
            StringComparison.Ordinal);

        Assert.NotEqual(workflow, invalid);
        Assert.ThrowsAny<XunitException>(() => AssertCoverageWorkflowContract(invalid, release: true));
    }

    [Fact]
    public void CoverageWorkflowContract_RejectsDetachedCoveragePlan()
    {
        var workflow = ReadRepoFile(".github", "workflows", "ci.yaml");
        var invalid = workflow.Replace(
            "    needs: fast-contracts\n",
            "    needs: core-tests\n",
            StringComparison.Ordinal);

        Assert.NotEqual(workflow, invalid);
        Assert.ThrowsAny<XunitException>(() => AssertCoverageWorkflowContract(invalid, release: false));
    }

    [Fact]
    public void CoverageWorkflowContract_RejectsAdditionalSummaryArtifactPath()
    {
        var workflow = ReadRepoFile(".github", "workflows", "ci.yaml");
        var invalid = workflow.Replace(
            "            docs/coverage-baselines.json\n",
            "            docs/coverage-baselines.json\n            docs/unrelated-secret.txt\n",
            StringComparison.Ordinal);

        Assert.NotEqual(workflow, invalid);
        Assert.ThrowsAny<XunitException>(() => AssertCoverageWorkflowContract(invalid, release: false));
    }

    [Fact]
    public void CoverageWorkflowContract_RejectsDetachedProjectMatrix()
    {
        var workflow = ReadRepoFile(".github", "workflows", "ci.yaml");
        var invalid = workflow.Replace(
            "      matrix: ${{ fromJSON(needs.coverage-plan.outputs.matrix) }}\n",
            "      matrix: ${{ fromJSON('{}') }}\n",
            StringComparison.Ordinal);

        Assert.NotEqual(workflow, invalid);
        Assert.ThrowsAny<XunitException>(() => AssertCoverageWorkflowContract(invalid, release: false));
    }

    [Fact]
    public void CoverageWorkflowContract_RejectsWrongDownloadedArtifact()
    {
        var workflow = ReadRepoFile(".github", "workflows", "ci.yaml");
        var invalid = workflow.Replace(
            "          pattern: core-coverage-project-*\n          path: TestResults/coverage/projects\n          merge-multiple: true\n",
            "          pattern: unrelated-artifact-*\n          path: TestResults/coverage/projects\n          merge-multiple: true\n",
            StringComparison.Ordinal);

        Assert.NotEqual(workflow, invalid);
        Assert.ThrowsAny<XunitException>(() => AssertCoverageWorkflowContract(invalid, release: false));
    }

    [Fact]
    public void CoverageDocs_SeparateCurrentMatrixContractFromHistoricalPlans()
    {
        var testing = ReadRepoFile("guide", "dev", "testing.md");
        var release = ReadRepoFile("guide", "dev", "release.md");
        var prerelease = ReadRepoFile("docs", "release", "release-prerelease-template.md");
        var plan104 = ReadRepoFile("docs", "release", "bukit-core-1.0.4-development-plan.md");
        var plan105106 = ReadRepoFile("docs", "release", "bukit-core-1.0.5-1.0.6-combined-development-plan.md");
        var checklist102 = ReadRepoFile("docs", "release", "bukit-1.0.2-release-checklist.md");

        Assert.Contains("coverage-plan", testing, StringComparison.Ordinal);
        Assert.Contains("per-project matrix", testing, StringComparison.Ordinal);
        Assert.Contains("coverage-summary", release, StringComparison.Ordinal);
        Assert.Contains("Architecture contracts", prerelease, StringComparison.Ordinal);
        Assert.Contains("Historical planning record", plan104, StringComparison.Ordinal);
        Assert.Contains("Historical planning record", plan105106, StringComparison.Ordinal);
        Assert.Contains("Historical release record", checklist102, StringComparison.Ordinal);
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

    private static void AssertCoverageWorkflowContract(string workflow, bool release)
    {
        var yaml = new YamlStream();
        yaml.Load(new StringReader(workflow));
        var root = Assert.IsType<YamlMappingNode>(Assert.Single(yaml.Documents).RootNode);

        AssertRunContains(Job(root, "fast-contracts"),
            "dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj");

        var plan = Job(root, "coverage-plan");
        Assert.Equal(["fast-contracts"], Needs(plan));
        var outputs = Mapping(plan, "outputs");
        Assert.Equal("${{ steps.projects.outputs.matrix }}", TryScalar(outputs, "matrix"));
        Assert.Equal("${{ steps.projects.outputs.count }}", TryScalar(outputs, "count"));
        AssertRunContains(plan, "coverage/output-path-self-test.sh");
        AssertRunContains(plan, "coverage/project-list-self-test.sh");
        AssertRunContains(plan, "coverage/matrix-self-test.sh");
        AssertRunContains(plan, "coverage/summarize-self-test.py");
        AssertRunContains(plan, "coverage-baseline-schema.sh");
        AssertRunContains(plan, "coverage/matrix.py");
        var projectsStep = Assert.Single(Steps(plan), step => TryScalar(step, "id") == "projects");
        Assert.Contains("coverage/matrix.py", TryScalar(projectsStep, "run"), StringComparison.Ordinal);

        var projects = Job(root, "coverage-projects");
        Assert.Equal(["coverage-plan"], Needs(projects));
        Assert.Equal(
            "${{ fromJSON(needs.coverage-plan.outputs.matrix) }}",
            TryScalar(Mapping(projects, "strategy"), "matrix"));
        AssertRunContains(projects, "coverage/run-one.sh");
        AssertArtifact(
            projects,
            "core-coverage-project-${{ matrix.name }}",
            "TestResults/coverage/projects");

        var summary = Job(root, "coverage-summary");
        Assert.Equal(["coverage-plan", "coverage-projects"], Needs(summary));
        AssertRunContains(summary, "coverage/find-results.sh");
        AssertRunContains(summary, "coverage/summarize.py");
        AssertDownloadArtifact(summary, "core-coverage-project-*", "TestResults/coverage/projects");
        var summaryStep = Assert.Single(Steps(summary), step =>
            TryScalar(step, "run")?.Contains("coverage/find-results.sh", StringComparison.Ordinal) == true);
        Assert.Equal(
            "${{ needs.coverage-plan.outputs.count }}",
            TryScalar(Mapping(summaryStep, "env"), "COVERAGE_PROJECT_COUNT"));
        AssertArtifact(
            summary,
            "core-coverage",
            "TestResults/coverage",
            "docs/coverage-baselines.json");

        if (!release)
        {
            AssertRunContains(plan, "coverage-run-one-self-test.sh");
            AssertCiExecutionContract(root, workflow);
            return;
        }

        foreach (var job in new[] { "package-linux", "package-macos", "package-windows", "collect-assets" })
        {
            Assert.Contains("coverage-summary", Needs(Job(root, job)));
        }
    }

    [Theory]
    [InlineData("needs: [coverage-plan, coverage-projects, coverage-summary, platform-tests]", "needs: [coverage-plan, coverage-projects, coverage-summary]")]
    [InlineData("always() && (github.event_name", "(github.event_name")]
    [InlineData("!= \"success\"", "== \"failure\"")]
    [InlineData("coverage-run-one-self-test.sh", "missing-runner-owner.sh")]
    [InlineData("ubuntu-24.04", "ubuntu-latest")]
    [InlineData("macos-15", "macos-14")]
    [InlineData("windows-2025", "windows-2022")]
    [InlineData("Bukit.Shared.Tests.PlatformSafeSourceFileOpenerTests", "OmittedOpenerTests")]
    [InlineData("inputs.gate == 'core' || inputs.gate == 'coverage'", "inputs.gate == 'core'")]
    public void CiExecutionContract_RejectsMissingRequiredEvidence(string before, string after)
    {
        var workflow = ReadRepoFile(".github", "workflows", "ci.yaml");
        var invalid = workflow.Replace(before, after, StringComparison.Ordinal);
        Assert.NotEqual(workflow, invalid);
        Assert.ThrowsAny<XunitException>(() => AssertCoverageWorkflowContract(invalid, release: false));
    }

    private static void AssertCiExecutionContract(YamlMappingNode root, string workflow)
    {
        const string coreTrigger = "github.event_name != 'workflow_dispatch' || inputs.gate == 'core'";
        var core = Job(root, "core-tests");
        Assert.Equal("Core tests", TryScalar(core, "name"));
        Assert.Equal(["coverage-plan", "coverage-projects", "coverage-summary", "platform-tests"], Needs(core));
        Assert.Equal("${{ always() && (" + coreTrigger + ") }}", TryScalar(core, "if"));
        AssertRunContains(core, "required = [\"coverage-plan\", \"coverage-projects\", \"coverage-summary\", \"platform-tests\"]");
        AssertRunContains(core, "needs.get(name, {}).get(\"result\") != \"success\"");
        Assert.DoesNotContain("ci-full.sh", workflow, StringComparison.Ordinal);
        Assert.Equal("${{ " + coreTrigger + " || inputs.gate == 'coverage' }}", TryScalar(Job(root, "coverage-plan"), "if"));
        Assert.Null(TryScalar(Job(root, "fast-contracts"), "if"));
        Assert.Equal("Fast contracts", TryScalar(Job(root, "fast-contracts"), "name"));
        Assert.Equal("Core coverage", TryScalar(Job(root, "coverage-summary"), "name"));
        var upload = Assert.Single(Steps(Job(root, "coverage-projects")), step => IsArtifact(step, "core-coverage-project-${{ matrix.name }}"));
        Assert.Equal("always()", TryScalar(upload, "if"));
        Assert.Null(TryScalar(upload, "continue-on-error"));

        var platform = Job(root, "platform-tests");
        Assert.Equal("${{ " + coreTrigger + " }}", TryScalar(platform, "if"));
        Assert.Equal("${{ matrix.runner }}", TryScalar(platform, "runs-on"));
        var matrix = Assert.IsType<YamlSequenceNode>(Get(Mapping(Mapping(platform, "strategy"), "matrix"), "include"));
        Assert.Equal(
            ["ubuntu-24.04/Linux/x64", "macos-15/Darwin/arm64", "windows-2025/Windows/x64"],
            matrix.Children.Cast<YamlMappingNode>().Select(item => $"{TryScalar(item, "runner")}/{TryScalar(item, "os")}/{TryScalar(item, "arch")}"));
        foreach (var name in new[]
        {
            "Bukit.PluginHost.Tests.SystemProcessRunnerTests", "Bukit.PluginHost.Tests.PluginPathValidatorTests",
            "Bukit.Engine.Tests.ExternalToolProcessRunnerTests", "Bukit.Engine.Tests.SafeOutputFileSystemTests",
            "Bukit.Engine.Tests.DirectoryCopyFollowSymlinksTests", "Bukit.Shared.Tests.PlatformPathHelperTests",
            "Bukit.Shared.Tests.PathUtilsTests", "Bukit.Shared.Tests.PlatformSafeSourceFileOpenerTests",
            "Bukit.Cli.Tests.PreviewCommandTests", "Bukit.Cli.Tests.PreviewCommandExtendedTests", "Bukit.Cli.Tests.DevCommandTests"
        })
            AssertRunContains(platform, name);
        AssertRunContains(platform, "FullyQualifiedName~");
        AssertRunContains(platform, "BUKIT_NOT_APPLICABLE:");
        AssertRunContains(platform, "actual_os != expected_os or host_arch != expected_arch");
        AssertRunContains(platform, "any(count == 0 for count in actual_by_class.values())");
        AssertRunContains(platform, "get(\"outcome\") not in (\"Completed\", \"Passed\")");
        Assert.DoesNotContain("--collect", string.Join("\n", Steps(platform).Select(step => TryScalar(step, "run"))), StringComparison.Ordinal);
        Assert.Equal("always()", TryScalar(Assert.Single(Steps(platform), step => IsArtifact(step, "platform-${{ matrix.os }}-${{ matrix.arch }}")), "if"));
    }

    private static YamlMappingNode Job(YamlMappingNode root, string name)
    {
        return Mapping(Mapping(root, "jobs"), name);
    }

    private static string[] Needs(YamlMappingNode job)
    {
        return Get(job, "needs") switch
        {
            YamlScalarNode scalar => [Scalar(scalar)],
            YamlSequenceNode sequence => sequence.Children
                .Select(node => Scalar(Assert.IsType<YamlScalarNode>(node)))
                .ToArray(),
            var node => throw new XunitException($"needs must be scalar or sequence, found {node.GetType().Name}"),
        };
    }

    private static void AssertRunContains(YamlMappingNode job, string fragment)
    {
        Assert.Contains(Steps(job), step =>
            TryScalar(step, "run")?.Contains(fragment, StringComparison.Ordinal) == true);
    }

    private static void AssertArtifact(YamlMappingNode job, string name, params string[] expectedPaths)
    {
        var artifact = Assert.Single(Steps(job), step => IsArtifact(step, name));
        var options = Mapping(artifact, "with");
        Assert.Equal(name, TryScalar(options, "name"));
        var paths = Scalar(Assert.IsType<YamlScalarNode>(Get(options, "path")))
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.Equal(
            expectedPaths.Order(StringComparer.Ordinal),
            paths.Order(StringComparer.Ordinal));
    }

    private static bool IsArtifact(YamlMappingNode step, string name)
    {
        if (TryScalar(step, "uses")?.StartsWith("actions/upload-artifact@", StringComparison.Ordinal) != true)
        {
            return false;
        }

        return TryScalar(Mapping(step, "with"), "name") == name;
    }

    private static void AssertDownloadArtifact(YamlMappingNode job, string pattern, string path)
    {
        var download = Assert.Single(Steps(job), step => IsDownloadArtifact(step, path));
        var options = Mapping(download, "with");
        Assert.Equal(pattern, TryScalar(options, "pattern"));
        Assert.Equal("true", TryScalar(options, "merge-multiple"));
    }

    private static bool IsDownloadArtifact(YamlMappingNode step, string path)
    {
        if (TryScalar(step, "uses")?.StartsWith("actions/download-artifact@", StringComparison.Ordinal) != true)
        {
            return false;
        }

        return TryScalar(Mapping(step, "with"), "path") == path;
    }

    private static IEnumerable<YamlMappingNode> Steps(YamlMappingNode job)
    {
        return Assert.IsType<YamlSequenceNode>(Get(job, "steps")).Children
            .Select(node => Assert.IsType<YamlMappingNode>(node));
    }

    private static YamlMappingNode Mapping(YamlMappingNode parent, string key)
    {
        return Assert.IsType<YamlMappingNode>(Get(parent, key));
    }

    private static YamlNode Get(YamlMappingNode parent, string key)
    {
        Assert.True(parent.Children.TryGetValue(new YamlScalarNode(key), out var value),
            $"missing YAML key: {key}");
        return value;
    }

    private static string? TryScalar(YamlMappingNode parent, string key)
    {
        return parent.Children.TryGetValue(new YamlScalarNode(key), out var value)
            ? Scalar(Assert.IsType<YamlScalarNode>(value))
            : null;
    }

    private static string Scalar(YamlScalarNode node)
    {
        return node.Value ?? string.Empty;
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
