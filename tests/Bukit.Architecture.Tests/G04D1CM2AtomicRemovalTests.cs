using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class G04D1CM2AtomicRemovalTests
{
    private const string CandidateManifestBlob = "7b07d6890562387010b52301e9f8716e9bf10ed1";
    private const string Decision =
        "G-04D1C-M2 five-type atomic decision: only the five approved `Bukit.Content.Notion` renderer-extension CLR identities are removed in 2.0; the other 105 candidates are not batch-approved.";
    private const string CurrentBaseline =
        "The current public API baseline contains 501 types, including 89 `2.0-candidate` entries.";
    private const string GovernanceExclusion =
        """
        This decision does not
        authorize removal of `NotionApiClient`, `NotionProviderOptions`, or
        `NotionClientStats`.
        """;
    private const string LedgerDecision =
        """
        批准只覆盖以下五个 `Bukit.Content.Notion` legacy renderer-extension CLR
        identity 的 2.0 原子删除：
        """;
    private const string LedgerPrivateConsumer =
        "- private consumer 继续为 `unknown-until-voluntary-declaration`；";
    private const string LedgerExclusion =
        "M2 不授权删除、internalize、重命名、obsolete 或修改以下类型的 public signature：";
    private static readonly string RepoRoot = FindRepoRoot();

    private static readonly string[] RemovedLegacyTypes =
    [
        "Bukit.Content.Notion.INotionBlockRenderer",
        "Bukit.Content.Notion.NotionBlockTransformer",
        "Bukit.Content.Notion.NotionBlockRendererRegistry",
        "Bukit.Content.Notion.NotionRenderContext",
        "Bukit.Content.Notion.NotionBlocksRenderer"
    ];

    private static readonly string[] CanonicalReplacementTypes =
    [
        "Bukit.Notion.Rendering.INotionBlockRenderer",
        "Bukit.Notion.Rendering.NotionBlockTransformer",
        "Bukit.Notion.Rendering.NotionBlockRendererRegistry",
        "Bukit.Notion.Rendering.NotionRenderContext",
        "Bukit.Notion.Rendering.NotionBlocksRenderer"
    ];

    [Fact]
    public void BukitContent_DoesNotExposeApprovedLegacyExtensionGraph()
    {
        var contentAssembly = typeof(Bukit.Content.Notion.NotionApiClient).Assembly;

        Assert.All(RemovedLegacyTypes, name =>
            Assert.Null(contentAssembly.GetType(name, throwOnError: false, ignoreCase: false)));
    }

    [Fact]
    public void LegacyCompatibilitySourceFiles_AreRemovedAsOneBatch()
    {
        string[] files =
        [
            "INotionBlockRenderer.cs",
            "NotionBlockRendererRegistry.cs",
            "NotionRenderContext.cs",
            "NotionBlocksRenderer.cs"
        ];
        var directory = Path.Combine(
            RepoRoot,
            "src",
            "Bukit-Core",
            "Bukit.Content",
            "Notion");

        Assert.All(files, file => Assert.False(
            File.Exists(Path.Combine(directory, file)),
            $"Approved M2 compatibility source still exists: {file}"));
    }

    [Fact]
    public void CanonicalReplacements_RemainPublicInBukitNotion()
    {
        var notionAssembly = typeof(Bukit.Notion.Transport.NotionClient).Assembly;

        Assert.Equal("Bukit.Notion", notionAssembly.GetName().Name);
        Assert.All(CanonicalReplacementTypes, name =>
        {
            var type = notionAssembly.GetType(name, throwOnError: false, ignoreCase: false);

            Assert.NotNull(type);
            Assert.True(type.IsPublic, $"Canonical replacement is not public: {name}");
        });
    }

    [Fact]
    public void ExplicitlyExcludedLegacyTypes_RemainPublicWithExactIdentities()
    {
        var contentAssembly = typeof(Bukit.Content.Notion.NotionApiClient).Assembly;
        string[] retainedTypes =
        [
            "Bukit.Content.Notion.NotionApiClient",
            "Bukit.Content.Notion.NotionProviderOptions",
            "Bukit.Content.Notion.NotionClientStats"
        ];

        Assert.All(retainedTypes, name =>
        {
            var type = contentAssembly.GetType(name, throwOnError: false, ignoreCase: false);

            Assert.NotNull(type);
            Assert.True(type.IsPublic, $"Explicitly excluded type is not public: {name}");
        });
    }

    [Fact]
    public void CurrentBaseline_ContainsFourteenAssembliesFiveHundredFiveTypesAndOneHundredOneCandidates()
    {
        using var document = ReadJson(
            "docs",
            "governance",
            "bukit-core-public-api-baseline.v1.json");
        var root = document.RootElement;
        var types = root.GetProperty("types").EnumerateArray().ToArray();

        Assert.Equal("bukit-core-public-api-baseline-v1", root.GetProperty("schema").GetString());
        Assert.Equal(14, root.GetProperty("assemblies").GetArrayLength());
        Assert.Equal(501, types.Length);
        Assert.Equal(89, types.Count(type =>
            type.GetProperty("compatibility").GetString() == "2.0-candidate"));
        Assert.All(RemovedLegacyTypes, removed => Assert.DoesNotContain(types, type =>
            type.GetProperty("assembly").GetString() == "Bukit.Content" &&
            type.GetProperty("name").GetString() == removed));
    }

    [Fact]
    public void ClosedManifest_PreservesHistoricalFiveTypeEvidenceAndExactBlob()
    {
        var path = Path.Combine(
            RepoRoot,
            "docs",
            "governance",
            "bukit-core-2.0-public-surface-candidates.v1.json");
        var bytes = File.ReadAllBytes(path);
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        var candidates = root.GetProperty("candidates").EnumerateArray().ToArray();

        Assert.Equal("closed", root.GetProperty("declarationState").GetString());
        Assert.Equal(136, root.GetProperty("candidateCount").GetInt32());
        Assert.Equal(136, candidates.Length);

        foreach (var legacyName in RemovedLegacyTypes)
        {
            var candidate = Assert.Single(candidates, entry =>
                entry.GetProperty("assembly").GetString() == "Bukit.Content" &&
                entry.GetProperty("fullName").GetString() == legacyName);

            Assert.Equal(
                "consumer-declaration-pending",
                candidate.GetProperty("declarationStatus").GetString());
            Assert.Equal(
                "unknown-until-voluntary-declaration",
                candidate.GetProperty("privateConsumerStatus").GetString());
            Assert.Equal(
                "no-public-match-found",
                candidate.GetProperty("externalEvidence").GetProperty("searchStatus").GetString());
        }

        var prefix = Encoding.UTF8.GetBytes($"blob {bytes.Length}\0");
        var blobBytes = new byte[prefix.Length + bytes.Length];
        prefix.CopyTo(blobBytes, 0);
        bytes.CopyTo(blobBytes, prefix.Length);

        Assert.Equal(
            CandidateManifestBlob,
            Convert.ToHexStringLower(SHA1.HashData(blobBytes)));
    }

    [Fact]
    public void ActiveGovernance_RecordsExactM2DecisionAndExclusions()
    {
        var declaration = File.ReadAllText(Path.Combine(
            RepoRoot,
            "docs",
            "governance",
            "bukit-core-2.0-consumer-declaration.md"));
        var guide = File.ReadAllText(Path.Combine(
            RepoRoot,
            "guide",
            "dev",
            "public-api-governance.md"));
        var ledger = File.ReadAllText(Path.Combine(
            RepoRoot,
            "docs",
            "analysis",
            "bukit-core-g04d1c-m2-five-type-atomic-removal-2026-07-23.zh-CN.md"));
        var m1Guide = File.ReadAllText(Path.Combine(
            RepoRoot,
            "docs",
            "analysis",
            "bukit-core-g04d1c-m1-canonical-migration-contract-2026-07-23.zh-CN.md"));

        Assert.Contains(Decision, declaration, StringComparison.Ordinal);
        Assert.Contains(Decision, guide, StringComparison.Ordinal);
        Assert.Contains(CurrentBaseline, declaration, StringComparison.Ordinal);
        Assert.Contains(CurrentBaseline, guide, StringComparison.Ordinal);
        Assert.Contains("unknown-until-voluntary-declaration", declaration, StringComparison.Ordinal);
        Assert.Contains("unknown-until-voluntary-declaration", guide, StringComparison.Ordinal);
        Assert.Contains(GovernanceExclusion, declaration, StringComparison.Ordinal);
        Assert.Contains(GovernanceExclusion, guide, StringComparison.Ordinal);
        Assert.Contains(LedgerDecision, ledger, StringComparison.Ordinal);
        Assert.Contains(LedgerPrivateConsumer, ledger, StringComparison.Ordinal);
        Assert.Contains(LedgerExclusion, ledger, StringComparison.Ordinal);
        Assert.Contains("NotionApiClient", ledger, StringComparison.Ordinal);
        Assert.Contains("NotionProviderOptions", ledger, StringComparison.Ordinal);
        Assert.Contains("NotionClientStats", ledger, StringComparison.Ordinal);
        Assert.Contains("14 / 509 / 105", ledger, StringComparison.Ordinal);
        Assert.Contains(CandidateManifestBlob, ledger, StringComparison.Ordinal);
        Assert.Contains(
            "M1 保留五个 legacy CLR 类型；M1 不授权 M2。",
            m1Guide,
            StringComparison.Ordinal);

        Assert.All(RemovedLegacyTypes, legacyName =>
        {
            Assert.Contains($"`{legacyName}`", declaration, StringComparison.Ordinal);
            Assert.Contains($"`{legacyName}`", guide, StringComparison.Ordinal);
            Assert.Contains($"`{legacyName}`", ledger, StringComparison.Ordinal);
        });
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

        throw new DirectoryNotFoundException("Could not locate the Bukit repository root.");
    }
}
