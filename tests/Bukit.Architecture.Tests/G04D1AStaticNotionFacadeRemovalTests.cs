using System.Text.Json;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class G04D1AStaticNotionFacadeRemovalTests
{
    private const string LegacyColorPalette = "Bukit.Content.Notion.NotionColorPalette";
    private const string LegacyRichTextRenderer = "Bukit.Content.Notion.NotionRichTextRenderer";
    private static readonly string[] RemovedTypes = [LegacyColorPalette, LegacyRichTextRenderer];
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void BukitContent_DoesNotExposeApprovedLegacyStaticNotionFacades()
    {
        var assembly = typeof(Bukit.Content.Notion.NotionApiClient).Assembly;

        Assert.All(RemovedTypes, typeName =>
            Assert.Null(assembly.GetType(typeName, throwOnError: false, ignoreCase: false)));
    }

    [Fact]
    public void CanonicalNotionRendering_ReplacementsRemainPublic()
    {
        var assembly = typeof(Bukit.Notion.Rendering.NotionColorPalette).Assembly;

        Assert.Equal("Bukit.Notion", assembly.GetName().Name);
        Assert.NotNull(assembly.GetType(
            "Bukit.Notion.Rendering.NotionColorPalette",
            throwOnError: false,
            ignoreCase: false));
        Assert.NotNull(assembly.GetType(
            "Bukit.Notion.Rendering.NotionRichTextRenderer",
            throwOnError: false,
            ignoreCase: false));
    }

    [Fact]
    public void CurrentBaseline_PreservesTheApprovedG04D1ARemovals()
    {
        using var document = ReadJson("docs", "governance", "bukit-core-public-api-baseline.v1.json");
        var root = document.RootElement;
        var types = root.GetProperty("types").EnumerateArray().ToArray();

        Assert.Equal("bukit-core-public-api-baseline-v1", root.GetProperty("schema").GetString());
        Assert.Equal("net10.0", root.GetProperty("targetFramework").GetString());
        Assert.Equal("no-general-clr-sdk", root.GetProperty("sdkPolicy").GetString());
        Assert.Equal(14, root.GetProperty("assemblies").GetArrayLength());
        Assert.Equal(484, types.Length);
        Assert.Equal(57, types.Count(type =>
            type.GetProperty("compatibility").GetString() == "2.0-candidate"));
        Assert.All(RemovedTypes, removedType => Assert.DoesNotContain(types, type =>
            type.GetProperty("assembly").GetString() == "Bukit.Content" &&
            type.GetProperty("name").GetString() == removedType));

        Assert.False(File.Exists(Path.Combine(
            RepoRoot,
            "src",
            "Bukit-Core",
            "Bukit.Content",
            "Notion",
            "NotionColorPalette.cs")));
        Assert.False(File.Exists(Path.Combine(
            RepoRoot,
            "src",
            "Bukit-Core",
            "Bukit.Content",
            "Notion",
            "NotionRichTextRenderer.cs")));
        Assert.True(File.Exists(Path.Combine(
            RepoRoot,
            "src",
            "Bukit-Core",
            "Bukit.Notion",
            "Rendering",
            "NotionColorPalette.cs")));
        Assert.True(File.Exists(Path.Combine(
            RepoRoot,
            "src",
            "Bukit-Core",
            "Bukit.Notion",
            "Rendering",
            "NotionRichTextRenderer.cs")));
    }

    [Fact]
    public void ActiveGovernance_RecordsCurrentPostG04D1CM2RemainingCandidateState()
    {
        const string decision = "G-04D1B block-renderer-facade decision: only the 23 `Bukit.Content.Notion.BlockRenderers` facade types recorded in the G-04D1B ledger are approved for removal in 2.0; the other 110 candidates are not batch-approved.";
        const string currentBaseline = "The current public API baseline contains 484 types, including 57 `2.0-candidate` entries.";
        const string staleCurrentBaseline = "current baseline has the other 133 candidates";
        var declaration = File.ReadAllText(Path.Combine(
            RepoRoot,
            "docs",
            "governance",
            "bukit-core-2.0-consumer-declaration.md"));
        var guide = File.ReadAllText(Path.Combine(RepoRoot, "guide", "dev", "public-api-governance.md"));

        Assert.Contains(decision, declaration, StringComparison.Ordinal);
        Assert.Contains(decision, guide, StringComparison.Ordinal);
        Assert.Contains(currentBaseline, declaration, StringComparison.Ordinal);
        Assert.Contains(currentBaseline, guide, StringComparison.Ordinal);
        Assert.DoesNotContain(staleCurrentBaseline, declaration, StringComparison.Ordinal);
        Assert.DoesNotContain(staleCurrentBaseline, guide, StringComparison.Ordinal);
    }

    [Fact]
    public void ClosedManifest_PreservesBothHistoricalCandidates()
    {
        using var document = ReadJson("docs", "governance", "bukit-core-2.0-public-surface-candidates.v1.json");
        var root = document.RootElement;
        var candidates = root.GetProperty("candidates").EnumerateArray().ToArray();

        Assert.Equal(136, root.GetProperty("candidateCount").GetInt32());
        Assert.Equal(136, candidates.Length);
        Assert.Equal("closed", root.GetProperty("declarationState").GetString());

        foreach (var removedType in RemovedTypes)
        {
            var candidate = Assert.Single(candidates, entry =>
                entry.GetProperty("fullName").GetString() == removedType);

            Assert.Equal("consumer-declaration-pending", candidate.GetProperty("declarationStatus").GetString());
            Assert.Equal("unknown-until-voluntary-declaration", candidate.GetProperty("privateConsumerStatus").GetString());
            Assert.Equal(
                "no-public-match-found",
                candidate.GetProperty("externalEvidence").GetProperty("searchStatus").GetString());
        }
    }

    [Fact]
    public void ActiveGovernance_RecordsTheExactTwoTypeDecision()
    {
        const string decision = "G-04D1A two-static-facade decision: only `Bukit.Content.Notion.NotionColorPalette` and `Bukit.Content.Notion.NotionRichTextRenderer` are approved for removal in 2.0; the other 133 candidates are not batch-approved.";
        const string canonicalPalette = "`Bukit.Notion.Rendering.NotionColorPalette`";
        const string canonicalRenderer = "`Bukit.Notion.Rendering.NotionRichTextRenderer`";
        const string completedEvidence = "Completed cross-boundary validation and independent review evidence";
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
            "bukit-core-g04d1a-static-notion-facade-removal-2026-07-22.zh-CN.md");

        Assert.Contains(decision, declaration, StringComparison.Ordinal);
        Assert.Contains(decision, guide, StringComparison.Ordinal);
        Assert.Contains("The closed 136-entry candidate manifest remains the immutable historical cohort", declaration, StringComparison.Ordinal);
        Assert.Contains("the other 135", declaration, StringComparison.Ordinal);
        Assert.Contains("the other 135", guide, StringComparison.Ordinal);
        Assert.Contains("candidates were not batch-approved.", declaration, StringComparison.Ordinal);
        Assert.Contains("candidates were not batch-approved.", guide, StringComparison.Ordinal);
        Assert.Contains(completedEvidence, declaration, StringComparison.Ordinal);
        Assert.Contains(completedEvidence, guide, StringComparison.Ordinal);
        Assert.DoesNotContain("pending cross-boundary validation", declaration, StringComparison.Ordinal);
        Assert.DoesNotContain("pending cross-boundary validation", guide, StringComparison.Ordinal);
        Assert.DoesNotContain("remaining cross-boundary validation", declaration, StringComparison.Ordinal);
        Assert.DoesNotContain("remaining cross-boundary validation", guide, StringComparison.Ordinal);
        Assert.True(File.Exists(ledgerPath), $"Missing G-04D1A decision ledger: {ledgerPath}");

        var ledger = File.ReadAllText(ledgerPath);
        Assert.Contains("状态：已实施并通过跨边界验证与独立只读复审", ledger, StringComparison.Ordinal);
        Assert.Contains("537", ledger, StringComparison.Ordinal);
        Assert.Contains("133", ledger, StringComparison.Ordinal);
        Assert.Contains(LegacyColorPalette, ledger, StringComparison.Ordinal);
        Assert.Contains(LegacyRichTextRenderer, ledger, StringComparison.Ordinal);
        Assert.Contains(canonicalPalette, ledger, StringComparison.Ordinal);
        Assert.Contains(canonicalRenderer, ledger, StringComparison.Ordinal);
        Assert.Contains("28", ledger, StringComparison.Ordinal);
        Assert.Contains("NotionClientStats", ledger, StringComparison.Ordinal);
        Assert.Contains("schema", ledger, StringComparison.Ordinal);
        Assert.Contains("plugin protocol", ledger, StringComparison.Ordinal);
        Assert.Contains("transport", ledger, StringComparison.Ordinal);
        Assert.Contains("exceptions", ledger, StringComparison.Ordinal);
        Assert.Contains("URLs", ledger, StringComparison.Ordinal);
        Assert.Contains("paths", ledger, StringComparison.Ordinal);
        Assert.Contains("reports", ledger, StringComparison.Ordinal);
        Assert.Contains("version", ledger, StringComparison.Ordinal);
        Assert.Contains("## Task 1 已完成验证", ledger, StringComparison.Ordinal);
        Assert.Contains("Architecture 109 passed / 0 failed", ledger, StringComparison.Ordinal);
        Assert.Contains("Content 670 passed / 0 failed", ledger, StringComparison.Ordinal);
        Assert.Contains("Notion 86 passed / 0 failed", ledger, StringComparison.Ordinal);
        Assert.Contains("0 skipped", ledger, StringComparison.Ordinal);
        Assert.Contains("Core Release `--no-restore` build：exit 0，0 warnings / 0 errors", ledger, StringComparison.Ordinal);
        Assert.Contains("Labs Release `--no-restore` build：exit 0，0 warnings / 0 errors", ledger, StringComparison.Ordinal);
        Assert.Contains("NETSDK1004", ledger, StringComparison.Ordinal);
        Assert.Contains("WordCountSectionPlugin", ledger, StringComparison.Ordinal);
        Assert.Contains("精确 restore", ledger, StringComparison.Ordinal);
        Assert.Contains("MissingFieldException", ledger, StringComparison.Ordinal);
        Assert.Contains("21 errors", ledger, StringComparison.Ordinal);
        Assert.Contains("Plugins 原命令在非沙箱环境原样重跑：exit 0，0 warnings / 0 errors", ledger, StringComparison.Ordinal);
        Assert.Contains("NU1900", ledger, StringComparison.Ordinal);
        Assert.Contains("vuln_index.dat-new", ledger, StringComparison.Ordinal);
        Assert.Contains("native-aot.sh 2.0.0-alpha.1 osx-arm64", ledger, StringComparison.Ordinal);
        Assert.Contains("非沙箱环境：exit 0", ledger, StringComparison.Ordinal);
        Assert.Contains("12,022,035 bytes", ledger, StringComparison.Ordinal);
        Assert.Contains("未上传、未发布", ledger, StringComparison.Ordinal);
        Assert.Contains("smoke exit 0", ledger, StringComparison.Ordinal);
        Assert.Contains("Config check passed", ledger, StringComparison.Ordinal);
        Assert.Contains("fixture build completed", ledger, StringComparison.Ordinal);
        Assert.Contains("routes=2 errors=0 warnings=22", ledger, StringComparison.Ordinal);
        Assert.Contains("public API drift self-test OK", ledger, StringComparison.Ordinal);
        Assert.Contains("public-api-drift.sh check Release", ledger, StringComparison.Ordinal);
        Assert.Contains("0 warnings / 0 errors", ledger, StringComparison.Ordinal);
        Assert.Contains("focused post-change check", ledger, StringComparison.Ordinal);
        Assert.Contains("candidate manifest", ledger, StringComparison.Ordinal);
        Assert.Contains("diff 为 0", ledger, StringComparison.Ordinal);
        Assert.Contains("第一次独立只读实施复审：Approved / PASS", ledger, StringComparison.Ordinal);
        Assert.Contains("0 Critical、0 Important、0 Minor", ledger, StringComparison.Ordinal);
        Assert.Contains("base/current blob 同为 `7b07d6890562387010b52301e9f8716e9bf10ed1`", ledger, StringComparison.Ordinal);
        Assert.Contains("其余 28 个 renderer candidates", ledger, StringComparison.Ordinal);
        Assert.Contains("canonical", ledger, StringComparison.Ordinal);
        Assert.Contains("parent aggregate `post-change-targeted.sh`", ledger, StringComparison.Ordinal);
        Assert.Contains("fresh final aggregate diff review", ledger, StringComparison.Ordinal);

        var pending = ledger[ledger.IndexOf("## closure commit 后 parent 待完成", StringComparison.Ordinal)..];
        Assert.DoesNotContain("Architecture、Content、Notion 测试项目", pending, StringComparison.Ordinal);
        Assert.DoesNotContain("public API self-test", pending, StringComparison.Ordinal);
        Assert.DoesNotContain("更新\n  baseline 后真实 check", pending, StringComparison.Ordinal);
        Assert.DoesNotContain("focused post-change check", pending, StringComparison.Ordinal);
        Assert.DoesNotContain("candidate manifest", pending, StringComparison.Ordinal);
        Assert.DoesNotContain("Changes requested", pending, StringComparison.Ordinal);
        Assert.DoesNotContain("实施记录已建立 / 跨边界验证与独立复审待执行", ledger, StringComparison.Ordinal);
        Assert.DoesNotContain("routes=2 errors=0 warnings=0", ledger, StringComparison.Ordinal);
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
