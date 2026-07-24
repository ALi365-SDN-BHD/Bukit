using YamlDotNet.RepresentationModel;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class ReleaseWorkflowContractTests
{
    private readonly YamlMappingNode _root = LoadWorkflow();

    [Fact]
    public void CollectAssets_VerifiesTheSelectedRidSet()
    {
        var verify = Step(Job("collect-assets"), "Verify assets");
        var run = Scalar(verify, "run");

        Assert.Equal("${{ inputs.rids }}", Scalar(Mapping(verify, "env"), "RIDS"));
        Assert.Contains("case \"$RIDS\" in", run, StringComparison.Ordinal);
        Assert.Contains("linux-x64) expected_rids=(linux-x64)", run, StringComparison.Ordinal);
        Assert.Contains("osx-arm64) expected_rids=(osx-arm64)", run, StringComparison.Ordinal);
        Assert.Contains("win-x64) expected_rids=(win-x64)", run, StringComparison.Ordinal);
        Assert.Contains("all) expected_rids=(linux-x64 osx-arm64 win-x64)", run, StringComparison.Ordinal);
        Assert.Contains("*) echo \"unsupported RID selection: $RIDS\" >&2; exit 2 ;;",
            run, StringComparison.Ordinal);
        Assert.Contains("verify-release-assets.sh", run, StringComparison.Ordinal);
    }

    [Fact]
    public void CollectAssets_VerifiesBeforeUploadingTheExactDirectory()
    {
        var steps = Steps(Job("collect-assets")).ToArray();
        var verify = Assert.Single(steps, step => TryScalar(step, "name") == "Verify assets");
        var upload = Assert.Single(steps, step =>
            TryScalar(step, "uses")?.StartsWith("actions/upload-artifact@", StringComparison.Ordinal) == true);

        Assert.True(Array.IndexOf(steps, verify) < Array.IndexOf(steps, upload));
        Assert.False(IsExplicitlyTrue(TryScalar(verify, "continue-on-error")));
        Assert.Equal("release-assets/*", Scalar(Mapping(upload, "with"), "path"));
        Assert.Null(TryScalar(upload, "if"));
    }

    [Theory]
    [InlineData("true")]
    [InlineData("TRUE")]
    [InlineData("${{ true }}")]
    public void ExplicitTrueContinueOnErrorValues_AreRecognized(string value)
    {
        Assert.True(IsExplicitlyTrue(value));
    }

    [Theory]
    [InlineData("package-linux", "linux-x64")]
    [InlineData("package-macos", "osx-arm64")]
    [InlineData("package-windows", "win-x64")]
    public void PackageJob_OnlyRunsForItsRidOrAll(string jobName, string rid)
    {
        Assert.Equal(
            $"${{{{ inputs.rids == '{rid}' || inputs.rids == 'all' }}}}",
            Scalar(Job(jobName), "if"));
    }

    [Theory]
    [InlineData("package-linux", "linux-x64")]
    [InlineData("package-macos", "osx-arm64")]
    [InlineData("package-windows", "win-x64")]
    public void PackageJob_SmokesTheFinalArchiveBeforeUpload(string jobName, string rid)
    {
        var steps = Steps(Job(jobName)).ToArray();
        var package = Assert.Single(steps, step => TryScalar(step, "id") == "package");
        var smoke = Assert.Single(steps, step => TryScalar(step, "name") == "Smoke packaged archive");
        var upload = Assert.Single(steps, step =>
            TryScalar(step, "uses")?.StartsWith("actions/upload-artifact@", StringComparison.Ordinal) == true);

        Assert.True(Array.IndexOf(steps, package) < Array.IndexOf(steps, smoke));
        Assert.True(Array.IndexOf(steps, smoke) < Array.IndexOf(steps, upload));
        var smokeRun = Scalar(smoke, "run");
        var expected = "bash scripts/smoke/release-artifacts.sh \"${{ steps.package.outputs.archive }}\" " + rid;
        Assert.Equal(expected, smokeRun);
        Assert.DoesNotContain("publish_dir", smokeRun, StringComparison.Ordinal);
        Assert.Null(TryScalar(smoke, "if"));
        Assert.Null(TryScalar(smoke, "continue-on-error"));
    }

    [Fact]
    public void WindowsPackageUpload_UsesWorkspaceRelativeArchivePath()
    {
        var upload = Assert.Single(Steps(Job("package-windows")), step =>
            TryScalar(step, "uses")?.StartsWith("actions/upload-artifact@", StringComparison.Ordinal) == true);

        Assert.Equal(
            "artifacts/bukit-${{ inputs.version }}-win-x64.zip",
            Scalar(Mapping(upload, "with"), "path"));
    }

    [Fact]
    public void PublishRelease_ClassifiesVersionSuffixAsPrerelease()
    {
        var publish = Assert.Single(Steps(Job("publish-release")), step =>
            TryScalar(step, "uses")?.StartsWith("softprops/action-gh-release@", StringComparison.Ordinal) == true);

        Assert.Equal(
            "${{ contains(inputs.version, '-') }}",
            Scalar(Mapping(publish, "with"), "prerelease"));
    }

    [Fact]
    public void PublicRelease_RequiresProtectedEnvironmentAndMain()
    {
        var publishJob = Job("publish-release");

        Assert.Equal(
            "${{ inputs.publish == 'true' && github.ref == 'refs/heads/main' }}",
            Scalar(publishJob, "if"));
        Assert.Equal(
            "public-release",
            Scalar(Mapping(publishJob, "environment"), "name"));
        Assert.Equal(
            "write",
            Scalar(Mapping(publishJob, "permissions"), "contents"));
    }

    [Fact]
    public void ValidateInputs_RejectsPublicReleaseOutsideMain()
    {
        var validate = Step(Job("validate-inputs"), "Validate release request");
        var env = Mapping(validate, "env");
        var run = Scalar(validate, "run");

        Assert.Equal("${{ github.ref }}", Scalar(env, "REF"));
        Assert.Contains(
            "if [[ \"$PUBLISH\" == \"true\" && \"$REF\" != \"refs/heads/main\" ]]; then exit 1; fi",
            run,
            StringComparison.Ordinal);
    }

    private YamlMappingNode Job(string name)
    {
        return Mapping(Mapping(_root, "jobs"), name);
    }

    private static IEnumerable<YamlMappingNode> Steps(YamlMappingNode job)
    {
        return Assert.IsType<YamlSequenceNode>(Get(job, "steps")).Children
            .Select(node => Assert.IsType<YamlMappingNode>(node));
    }

    private static YamlMappingNode Step(YamlMappingNode job, string name)
    {
        return Assert.Single(Steps(job), candidate => TryScalar(candidate, "name") == name);
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

    private static string Scalar(YamlMappingNode parent, string key)
    {
        return Assert.IsType<YamlScalarNode>(Get(parent, key)).Value ?? string.Empty;
    }

    private static string? TryScalar(YamlMappingNode parent, string key)
    {
        return parent.Children.TryGetValue(new YamlScalarNode(key), out var value)
            ? Assert.IsType<YamlScalarNode>(value).Value ?? string.Empty
            : null;
    }

    private static bool IsExplicitlyTrue(string? value)
    {
        var normalized = value?.Trim();
        return string.Equals(normalized, "true", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "${{ true }}", StringComparison.OrdinalIgnoreCase);
    }

    private static YamlMappingNode LoadWorkflow()
    {
        var yaml = new YamlStream();
        yaml.Load(new StringReader(File.ReadAllText(
            Path.Combine(FindRepoRoot(), ".github", "workflows", "release.yaml"))));
        return Assert.IsType<YamlMappingNode>(Assert.Single(yaml.Documents).RootNode);
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
