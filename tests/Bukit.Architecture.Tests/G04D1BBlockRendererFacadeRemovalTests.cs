using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class G04D1BBlockRendererFacadeRemovalTests
{
    private const string Decision = "G-04D1B block-renderer-facade decision: only the 23 `Bukit.Content.Notion.BlockRenderers` facade types recorded in the G-04D1B ledger are approved for removal in 2.0; the other 110 candidates are not batch-approved.";
    private const string LedgerStatus = "状态：已实施并通过跨边界验证与独立只读复审";
    private const string ProvisionalLedgerStatus = "状态：实施记录已建立 / 跨边界验证与独立复审待执行";
    private const string CandidateManifestBlob = "7b07d6890562387010b52301e9f8716e9bf10ed1";
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string[] RendererNames =
    [
        "AudioBlockRenderer",
        "BookmarkBlockRenderer",
        "CalloutBlockRenderer",
        "ChildEntityBlockRenderer",
        "CodeBlockRenderer",
        "ColumnBlockRenderer",
        "ColumnListBlockRenderer",
        "DividerBlockRenderer",
        "EmbedBlockRenderer",
        "EquationBlockRenderer",
        "FileBlockRenderer",
        "ImageBlockRenderer",
        "LinkPreviewBlockRenderer",
        "LinkToPageBlockRenderer",
        "NoOpBlockRenderer",
        "PdfBlockRenderer",
        "RichTextContainerRenderer",
        "SyncedBlockRenderer",
        "TableBlockRenderer",
        "TableOfContentsBlockRenderer",
        "ToDoBlockRenderer",
        "ToggleBlockRenderer",
        "VideoBlockRenderer"
    ];
    private static readonly string[] D1CTypeNames =
    [
        "Bukit.Content.Notion.INotionBlockRenderer",
        "Bukit.Content.Notion.NotionBlockTransformer",
        "Bukit.Content.Notion.NotionBlockRendererRegistry",
        "Bukit.Content.Notion.NotionBlocksRenderer",
        "Bukit.Content.Notion.NotionRenderContext"
    ];

    [Fact]
    public void BukitContent_DoesNotExposeApprovedLegacyBlockRendererFacades()
    {
        var assembly = typeof(Bukit.Content.Notion.NotionPropertyParser).Assembly;

        Assert.All(RendererNames, rendererName => Assert.Null(assembly.GetType(
            $"Bukit.Content.Notion.BlockRenderers.{rendererName}",
            throwOnError: false,
            ignoreCase: false)));
    }

    [Fact]
    public void CanonicalNotionRendering_AllBlockRendererReplacementsRemainPublic()
    {
        var assembly = typeof(Bukit.Notion.Rendering.BlockRenderers.AudioBlockRenderer).Assembly;

        Assert.Equal("Bukit.Notion", assembly.GetName().Name);
        Assert.All(RendererNames, rendererName => Assert.NotNull(assembly.GetType(
            $"Bukit.Notion.Rendering.BlockRenderers.{rendererName}",
            throwOnError: false,
            ignoreCase: false)));
    }

    [Fact]
    public void CurrentBaseline_PreservesG04D1BAndG04D1CM2Removals()
    {
        using var document = ReadJson("docs", "governance", "bukit-core-public-api-baseline.v1.json");
        var root = document.RootElement;
        var types = root.GetProperty("types").EnumerateArray().ToArray();

        Assert.Equal("bukit-core-public-api-baseline-v1", root.GetProperty("schema").GetString());
        Assert.Equal("net10.0", root.GetProperty("targetFramework").GetString());
        Assert.Equal("no-general-clr-sdk", root.GetProperty("sdkPolicy").GetString());
        Assert.Equal(14, root.GetProperty("assemblies").GetArrayLength());
        Assert.Equal(425, types.Length);
        Assert.Equal(0, types.Count(type =>
            type.GetProperty("compatibility").GetString() == "2.0-candidate"));

        Assert.All(RendererNames, rendererName =>
        {
            Assert.DoesNotContain(types, type =>
                type.GetProperty("assembly").GetString() == "Bukit.Content" &&
                type.GetProperty("name").GetString() ==
                $"Bukit.Content.Notion.BlockRenderers.{rendererName}");
            Assert.Contains(types, type =>
                type.GetProperty("assembly").GetString() == "Bukit.Notion" &&
                type.GetProperty("name").GetString() ==
                $"Bukit.Notion.Rendering.BlockRenderers.{rendererName}");
        });
    }

    [Fact]
    public void ClosedManifest_PreservesImmutableG04D1BHistory()
    {
        var manifestPath = Path.Combine(
            RepoRoot,
            "docs",
            "governance",
            "bukit-core-2.0-public-surface-candidates.v1.json");
        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = document.RootElement;
        var candidates = root.GetProperty("candidates").EnumerateArray().ToArray();

        Assert.Equal(136, root.GetProperty("candidateCount").GetInt32());
        Assert.Equal(136, candidates.Length);
        Assert.Equal("closed", root.GetProperty("declarationState").GetString());

        foreach (var rendererName in RendererNames)
        {
            var candidate = Assert.Single(candidates, entry =>
                entry.GetProperty("fullName").GetString() ==
                $"Bukit.Content.Notion.BlockRenderers.{rendererName}");

            Assert.Equal("consumer-declaration-pending", candidate.GetProperty("declarationStatus").GetString());
            Assert.Equal("unknown-until-voluntary-declaration", candidate.GetProperty("privateConsumerStatus").GetString());
            Assert.Equal(
                "no-public-match-found",
                candidate.GetProperty("externalEvidence").GetProperty("searchStatus").GetString());
        }

        var bytes = File.ReadAllBytes(manifestPath);
        var prefix = Encoding.UTF8.GetBytes($"blob {bytes.Length}\0");
        var blobBytes = new byte[prefix.Length + bytes.Length];
        prefix.CopyTo(blobBytes, 0);
        bytes.CopyTo(blobBytes, prefix.Length);
        var blob = Convert.ToHexStringLower(SHA1.HashData(blobBytes));

        Assert.Equal(CandidateManifestBlob, blob);
    }

    [Fact]
    public void SourceBoundary_ReflectsApprovedHelperAndM2Removals()
    {
        var blockRendererDirectory = Path.Combine(
            RepoRoot,
            "src",
            "Bukit-Core",
            "Bukit.Content",
            "Notion",
            "BlockRenderers");
        var assembly = typeof(Bukit.Content.Notion.NotionPropertyParser).Assembly;

        Assert.False(File.Exists(Path.Combine(blockRendererDirectory, "BlockRendererFacades.cs")));
        Assert.False(File.Exists(Path.Combine(blockRendererDirectory, "NotionBlockHelpers.cs")));
        Assert.All(D1CTypeNames, typeName => Assert.Null(assembly.GetType(
            typeName,
            throwOnError: false,
            ignoreCase: false)));
    }

    [Fact]
    public void ActiveGovernance_RecordsCompletedG04D1BDecisionBoundary()
    {
        var declaration = File.ReadAllText(Path.Combine(
            RepoRoot,
            "docs",
            "governance",
            "bukit-core-2.0-consumer-declaration.md"));
        var guide = File.ReadAllText(Path.Combine(RepoRoot, "guide", "dev", "public-api-governance.md"));
        var ledger = File.ReadAllText(Path.Combine(
            RepoRoot,
            "docs",
            "analysis",
            "bukit-core-g04d1b-block-renderer-facade-removal-2026-07-23.zh-CN.md"));

        Assert.Contains(Decision, declaration, StringComparison.Ordinal);
        Assert.Contains(Decision, guide, StringComparison.Ordinal);
        Assert.Contains(LedgerStatus, ledger, StringComparison.Ordinal);
        Assert.DoesNotContain(ProvisionalLedgerStatus, ledger, StringComparison.Ordinal);
        Assert.DoesNotContain("跨边界验证与独立复审 remain pending", ledger, StringComparison.Ordinal);
        Assert.Contains("`Bukit.Notion.Rendering.BlockRenderers`", ledger, StringComparison.Ordinal);
        Assert.Contains("514 types", ledger, StringComparison.Ordinal);
        Assert.Contains("110 个 `2.0-candidate`", ledger, StringComparison.Ordinal);
        Assert.Contains("136-entry candidate manifest", ledger, StringComparison.Ordinal);
        Assert.Contains(CandidateManifestBlob, ledger, StringComparison.Ordinal);
        Assert.Contains("合计 756", ledger, StringComparison.Ordinal);
        Assert.Contains("source/binary breaking boundary", ledger, StringComparison.Ordinal);
        Assert.Contains("unknown-until-voluntary-declaration", ledger, StringComparison.Ordinal);
        Assert.Contains("Architecture 116 passed / 0 failed / 0 skipped", ledger, StringComparison.Ordinal);
        Assert.Contains("Content 486 passed / 0 failed / 0 skipped", ledger, StringComparison.Ordinal);
        Assert.Contains("Notion 270 passed / 0 failed / 0 skipped", ledger, StringComparison.Ordinal);
        Assert.Contains("bukit-core.slnx", ledger, StringComparison.Ordinal);
        Assert.Contains("bukit-labs.slnx", ledger, StringComparison.Ordinal);
        Assert.Contains("bukit-plugins.slnx", ledger, StringComparison.Ordinal);
        Assert.Contains("12021863 bytes", ledger, StringComparison.Ordinal);
        Assert.Contains("public API drift self-test OK", ledger, StringComparison.Ordinal);
        Assert.Contains("Approved / PASS; 0 Critical, 0 Important, 0 Minor", ledger, StringComparison.Ordinal);
        Assert.Contains("parent aggregate", ledger, StringComparison.Ordinal);
        Assert.Contains("post-change-targeted.sh", ledger, StringComparison.Ordinal);
        Assert.Contains("最终 aggregate diff 复审仍待 parent task 完成", ledger, StringComparison.Ordinal);
        Assert.Contains("Completed cross-boundary validation and independent review evidence is recorded there.", declaration, StringComparison.Ordinal);
        Assert.Contains("Completed cross-boundary validation and independent review evidence is recorded there.", guide, StringComparison.Ordinal);

        Assert.All(RendererNames, rendererName => Assert.Contains(
            $"`Bukit.Content.Notion.BlockRenderers.{rendererName}`",
            ledger,
            StringComparison.Ordinal));
        Assert.All(D1CTypeNames, typeName => Assert.Contains(typeName, ledger, StringComparison.Ordinal));

        Assert.Contains("不删除或修改五个 D1C 扩展图类型", ledger, StringComparison.Ordinal);
        Assert.Contains("不处理 `NotionClientStats`", ledger, StringComparison.Ordinal);
        Assert.Contains("不修改 canonical production renderer 行为或公开契约", ledger, StringComparison.Ordinal);
        Assert.Contains("不修改 schema、plugin protocol、transport、exceptions、URLs、paths 或 reports 契约", ledger, StringComparison.Ordinal);
        Assert.Contains("不修改项目文件、版本、CI、release、gate script 或 verification policy", ledger, StringComparison.Ordinal);
        Assert.Contains("不修改闭合的 136-entry candidate manifest", ledger, StringComparison.Ordinal);
        Assert.Contains("不授权剩余 110 个候选的批量删除，也不改变任何 1.x CLR visibility", ledger, StringComparison.Ordinal);

        Assert.Contains("Core 跨边界 Release 验证", ledger, StringComparison.Ordinal);
        Assert.Contains("Labs 跨边界 Release 验证", ledger, StringComparison.Ordinal);
        Assert.Contains("plugins 跨边界 Release 验证", ledger, StringComparison.Ordinal);
        Assert.Contains("Native AOT 与 release-artifact smoke", ledger, StringComparison.Ordinal);
        Assert.Contains("独立只读实施复审", ledger, StringComparison.Ordinal);
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
