using System.Text.Json;
using System.Xml.Linq;
using Bukit.Engine;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class G04CPublicSurfacePilotTests
{
    private const string RemovedTypeName = "Bukit.Engine.RouteInventoryInspectEntry";
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void EngineAssembly_DoesNotExposeRemovedRouteInventoryInspectEntry()
    {
        var engineAssembly = typeof(RouteInventoryValidator).Assembly;

        Assert.Null(engineAssembly.GetType(RemovedTypeName, throwOnError: false, ignoreCase: false));
    }

    [Fact]
    public void ProductVersion_IsTheApprovedTwoPointZeroAlpha()
    {
        var document = XDocument.Load(Path.Combine(RepoRoot, "Directory.Build.props"));
        var versions = document
            .Descendants()
            .Where(element => element.Name.LocalName == "Version")
            .Select(element => element.Value)
            .ToArray();

        Assert.Equal(["2.0.0-alpha.1"], versions);
    }

    [Fact]
    public void CurrentPublicApiBaseline_PreservesTheApprovedG04CRemoval()
    {
        using var document = ReadJson("docs", "governance", "bukit-core-public-api-baseline.v1.json");
        var root = document.RootElement;
        var types = root.GetProperty("types").EnumerateArray().ToArray();

        Assert.Equal("bukit-core-public-api-baseline-v1", root.GetProperty("schema").GetString());
        Assert.Equal("net10.0", root.GetProperty("targetFramework").GetString());
        Assert.Equal("no-general-clr-sdk", root.GetProperty("sdkPolicy").GetString());
        Assert.Equal(14, root.GetProperty("assemblies").GetArrayLength());
        Assert.Equal(449, types.Length);
        Assert.Equal(10, types.Count(type =>
            type.GetProperty("compatibility").GetString() == "2.0-candidate"));
        Assert.DoesNotContain(types, type =>
            type.GetProperty("assembly").GetString() == "Bukit.Engine" &&
            type.GetProperty("name").GetString() == RemovedTypeName);
    }

    [Fact]
    public void ClosedCandidateManifest_PreservesTheHistoricalPilotEvidence()
    {
        using var document = ReadJson("docs", "governance", "bukit-core-2.0-public-surface-candidates.v1.json");
        var root = document.RootElement;
        var candidates = root.GetProperty("candidates").EnumerateArray().ToArray();
        var target = Assert.Single(candidates, candidate =>
            candidate.GetProperty("fullName").GetString() == RemovedTypeName);

        Assert.Equal(136, root.GetProperty("candidateCount").GetInt32());
        Assert.Equal(136, candidates.Length);
        Assert.Equal("closed", root.GetProperty("declarationState").GetString());
        Assert.Equal("consumer-declaration-pending", target.GetProperty("declarationStatus").GetString());
        Assert.Equal("unknown-until-voluntary-declaration", target.GetProperty("privateConsumerStatus").GetString());
        Assert.Equal("no-public-match-found", target.GetProperty("externalEvidence").GetProperty("searchStatus").GetString());

        var queries = target.GetProperty("externalEvidence").GetProperty("queries").EnumerateArray().ToArray();
        Assert.Equal(2, queries.Length);
        Assert.All(queries, query =>
        {
            Assert.Equal(0, query.GetProperty("returned").GetInt32());
            Assert.False(query.GetProperty("truncated").GetBoolean());
        });
    }

    [Fact]
    public void ActiveGovernance_RecordsOnlyTheApprovedSingleTypeDecision()
    {
        const string decision = "Historical G-04C single-type decision: only `Bukit.Engine.RouteInventoryInspectEntry`";
        const string historicalCandidateCount = "the other 135";
        const string historicalRemainder = "candidates were not batch-approved.";
        const string historicalPostD1ARemainder = "the other 133 candidates are not batch-approved.";
        const string currentRemainder = "the other 103 candidates are not batch-approved.";
        const string currentBaseline = "The current public API baseline contains 449 types, including 10 `2.0-candidate` entries.";
        const string staleCurrentBaseline = "current baseline has the other 133 candidates";
        const string historicalDecision = "G-04C was the first";
        const string historicalDecisionKind = "authorized 2.0 removal decision";
        const string currentDecision = "G-04D1A was a later independent 2.0 removal decision";
        var declaration = File.ReadAllText(Path.Combine(
            RepoRoot,
            "docs",
            "governance",
            "bukit-core-2.0-consumer-declaration.md"));
        var guide = File.ReadAllText(Path.Combine(RepoRoot, "guide", "dev", "public-api-governance.md"));
        var ledgerPath = Path.Combine(
            RepoRoot,
            "docs",
            "analysis",
            "bukit-core-g04c-route-inventory-inspect-entry-removal-2026-07-22.zh-CN.md");

        Assert.Contains(decision, declaration, StringComparison.Ordinal);
        Assert.Contains(historicalCandidateCount, declaration, StringComparison.Ordinal);
        Assert.Contains(historicalRemainder, declaration, StringComparison.Ordinal);
        Assert.Contains(historicalPostD1ARemainder, declaration, StringComparison.Ordinal);
        Assert.Contains(currentRemainder, declaration, StringComparison.Ordinal);
        Assert.Contains(historicalDecision, declaration, StringComparison.Ordinal);
        Assert.Contains(historicalDecisionKind, declaration, StringComparison.Ordinal);
        Assert.Contains(currentDecision, declaration, StringComparison.Ordinal);
        Assert.Contains(currentBaseline, declaration, StringComparison.Ordinal);
        Assert.DoesNotContain(staleCurrentBaseline, declaration, StringComparison.Ordinal);
        Assert.Contains("The closed manifest preserves the 136-type review inventory.", declaration, StringComparison.Ordinal);
        Assert.Contains("At declaration-window closure, all 136 entries were review candidates rather than removal decisions.", declaration, StringComparison.Ordinal);
        Assert.DoesNotContain("All 136 entries are review candidates, not removal decisions.", declaration, StringComparison.Ordinal);
        Assert.Contains(decision, guide, StringComparison.Ordinal);
        Assert.Contains(historicalCandidateCount, guide, StringComparison.Ordinal);
        Assert.Contains(historicalRemainder, guide, StringComparison.Ordinal);
        Assert.Contains(historicalPostD1ARemainder, guide, StringComparison.Ordinal);
        Assert.Contains(currentRemainder, guide, StringComparison.Ordinal);
        Assert.Contains(historicalDecision, guide, StringComparison.Ordinal);
        Assert.Contains(historicalDecisionKind, guide, StringComparison.Ordinal);
        Assert.Contains(currentDecision, guide, StringComparison.Ordinal);
        Assert.Contains(currentBaseline, guide, StringComparison.Ordinal);
        Assert.DoesNotContain(staleCurrentBaseline, guide, StringComparison.Ordinal);
        Assert.DoesNotContain("sole authorized exception", declaration, StringComparison.Ordinal);
        Assert.DoesNotContain("sole authorized exception", guide, StringComparison.Ordinal);
        Assert.DoesNotContain("remain review-only", declaration, StringComparison.Ordinal);
        Assert.DoesNotContain("remain review-only", guide, StringComparison.Ordinal);
        Assert.True(File.Exists(ledgerPath), $"Missing G-04C decision ledger: {ledgerPath}");

        var ledger = File.ReadAllText(ledgerPath);
        Assert.Contains("其余 135 项候选没有获得批量变更授权", ledger, StringComparison.Ordinal);
        Assert.Contains("历史 cohort", ledger, StringComparison.Ordinal);
        Assert.Contains("没有替代 API", ledger, StringComparison.Ordinal);
        Assert.Contains("状态：已实施并通过跨边界验证与独立只读复审", ledger, StringComparison.Ordinal);
        Assert.Contains("- Core、Labs 与 `bukit-plugins.slnx` Release 编译通过；", ledger, StringComparison.Ordinal);
        Assert.Contains("- `osx-arm64` Native AOT 归档构建及 release-artifact smoke 通过；", ledger, StringComparison.Ordinal);
        Assert.Contains("- 第一次独立只读实施复审未发现未关闭的 Critical 或 Important finding。", ledger, StringComparison.Ordinal);
        Assert.Contains("aggregate targeted gate 和最终 aggregate diff 复审在本关闭提交后执行", ledger, StringComparison.Ordinal);
        Assert.DoesNotContain("状态：实施记录已建立 / 跨边界验证与独立复审待执行", ledger, StringComparison.Ordinal);
        Assert.DoesNotContain("smoke、aggregate targeted gate 和独立只读复审尚未执行", ledger, StringComparison.Ordinal);
    }

    private static JsonDocument ReadJson(params string[] relativeSegments)
    {
        var path = Path.Combine([RepoRoot, .. relativeSegments]);
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "bukit-core.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
