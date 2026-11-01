using System.Diagnostics;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace Bukit.Architecture.Tests;

public sealed class WindowsGitTransportWorkflowTests
{
    [Fact]
    public void WindowsJob_CoversDeploymentAndRetainsCheckedEvidence()
    {
        var job = Job();
        Assert.Equal("windows-2025", Scalar(job, "runs-on"));
        Assert.Equal("fast-contracts", Scalar(job, "needs"));
        Assert.False(job.Children.ContainsKey(new YamlScalarNode("continue-on-error")));
        var steps = Steps(job);
        var execution = ExecutionStep(job);
        Assert.Equal("python", Scalar(execution, "shell"));
        Assert.False(execution.Children.ContainsKey(new YamlScalarNode("continue-on-error")));
        var script = Scalar(execution, "run");
        foreach (var value in new[]
                 {
                     "DeployGitTransportPrimitiveTests", "DeployIdentityTransportTests", "DeployWindowsRegressionTests",
                     "DeployAsync_RealLocalGitClonesAndPushesWithAskpassAlive",
                     "DeployAsync_RealLocalGitPushFailure_CleansReadOnlyObjectsAndAskpass",
                     "DeployAsync_RealLocalGitPushCancellation_CleansReadOnlyObjectsAndAskpass",
                     "DeployAsync_UserPagesRepoWithDotRemoteUrl_DeploysToRootGithubIoUrl",
                     "DeployAsync_CancelDuringPush_PropagatesOperationCanceledException_AndCleansUp",
                     "EnsureMetadataWriteTargetIsNotLink_RejectsRegularAndDanglingSymlinks",
                     "FullyQualifiedName~", "validate(ET.parse(trx).getroot(), completed.returncode)",
                     "GIT_ALLOW_PROTOCOL=\"file\"", "exist_ok=False", "if residue:", "windows-git-evidence.json"
                 })
        {
            Assert.Contains(value, script, StringComparison.Ordinal);
        }
        var checkout = Assert.Single(steps, step => TryScalar(step, "uses") == "actions/checkout@v7");
        var lineEndings = Assert.Single(steps, step => TryScalar(step, "name") == "Preserve fixture input line endings");
        Assert.Contains("core.autocrlf false", Scalar(lineEndings, "run"), StringComparison.Ordinal);
        Assert.True(Array.IndexOf(steps, lineEndings) < Array.IndexOf(steps, checkout));
        var upload = Assert.Single(steps, step => TryScalar(step, "uses") == "actions/upload-artifact@v7");
        Assert.Equal("always()", Scalar(upload, "if"));
        var with = Assert.IsType<YamlMappingNode>(upload.Children[new YamlScalarNode("with")]);
        Assert.Equal("TestResults/windows-git", Scalar(with, "path"));
        Assert.Equal("error", Scalar(with, "if-no-files-found"));
    }

    [Fact]
    public async Task ActualEmbeddedVerifier_RejectsMissingSkippedDuplicateOrFalseGreenResults()
    {
        var source = Scalar(ExecutionStep(Job()), "run");
        var code = "__name__ = 'owner_contract'\n" + source + "\n" + """
            def sample():
                root = ET.Element("TestRun")
                results = ET.SubElement(root, "Results")
                for cls, count in expected.items():
                    methods = provider_methods if cls.endswith("GitHubPagesDeployProviderTests") else ["Test" + str(i) for i in range(count)]
                    for name in methods:
                        ET.SubElement(results, "UnitTestResult", testName=cls + "." + name, outcome="Passed")
                summary = ET.SubElement(root, "ResultSummary", outcome="Completed")
                total = str(sum(expected.values()))
                ET.SubElement(summary, "Counters", total=total, executed=total, passed=total, failed="0", notExecuted="0")
                return root
            assert sum(expected.values()) == 61
            assert validate(sample(), 0) == expected
            mutations = [
                lambda root: root.find("ResultSummary").set("outcome", "Failed"),
                lambda root: root.find("Results").remove(root.find("Results")[0]),
                lambda root: root.find("Results")[0].set("outcome", "NotExecuted"),
                lambda root: root.find("Results")[0].set("testName", root.find("Results")[1].get("testName")),
                lambda root: root.find("Results")[0].set("testName", "Unexpected.Class.Test"),
                lambda root: root.find("Results")[-1].set("testName", "Bukit.Cli.Tests.GitHubPagesDeployProviderTests.WrongMethod"),
                lambda root: ET.SubElement(ET.SubElement(root.find("Results")[0], "Output"), "StdOut").__setattr__("text", "BUKIT_NOT_APPLICABLE: wrong platform"),
            ]
            for key in ["total", "executed", "passed", "failed", "notExecuted"]:
                mutations.append(lambda root, key=key: root.find("ResultSummary/Counters").set(key, "999"))
            rejected = 0
            for mutation in mutations:
                root = sample()
                mutation(root)
                try:
                    validate(root, 0)
                except ValueError:
                    rejected += 1
                else:
                    raise AssertionError("Verifier accepted a false GREEN result")
            try:
                validate(sample(), 1)
            except ValueError:
                rejected += 1
            else:
                raise AssertionError("Verifier accepted a failed process")
            assert rejected == 13
            print("Windows Git owner verifier: passing fixture and 13 rejected mutants")
            """;
        var start = new ProcessStartInfo(OperatingSystem.IsWindows() ? "python" : "python3")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("-B");
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add(code);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            await Task.WhenAll(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(process.ExitCode == 0, await stderr);
            Assert.Contains("13 rejected mutants", await stdout, StringComparison.Ordinal);
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
    }

    private static YamlMappingNode ExecutionStep(YamlMappingNode job)
        => Assert.Single(Steps(job), step => TryScalar(step, "name") == "Run real Git transport and deployment regressions");

    private static YamlMappingNode[] Steps(YamlMappingNode job)
        => Assert.IsType<YamlSequenceNode>(job.Children[new YamlScalarNode("steps")]).Children
            .Select(node => Assert.IsType<YamlMappingNode>(node)).ToArray();

    private static string Scalar(YamlMappingNode node, string key)
        => Assert.IsType<YamlScalarNode>(node.Children[new YamlScalarNode(key)]).Value ?? string.Empty;

    private static string? TryScalar(YamlMappingNode node, string key)
        => node.Children.TryGetValue(new YamlScalarNode(key), out var value) ? Assert.IsType<YamlScalarNode>(value).Value : null;

    private static YamlMappingNode Job()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "bukit-core.slnx"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var yaml = new YamlStream();
        yaml.Load(new StringReader(File.ReadAllText(Path.Combine(directory.FullName, ".github", "workflows", "ci.yaml"))));
        var root = Assert.IsType<YamlMappingNode>(Assert.Single(yaml.Documents).RootNode);
        var jobs = Assert.IsType<YamlMappingNode>(root.Children[new YamlScalarNode("jobs")]);
        return Assert.IsType<YamlMappingNode>(jobs.Children[new YamlScalarNode("windows-git-transport")]);
    }
}
