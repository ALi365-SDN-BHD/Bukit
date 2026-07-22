using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class G04D1CM1MigrationContractTests
{
    private const string CandidateManifestBlob = "7b07d6890562387010b52301e9f8716e9bf10ed1";
    private const string GuideRelativePath =
        "docs/analysis/bukit-core-g04d1c-m1-canonical-migration-contract-2026-07-23.zh-CN.md";
    private const string ProvisionalStatus =
        "状态：provisional（Task 3 focused verification、parent aggregate、四项目 Release test 与独立复审待 parent controller 记录）";
    private const string M1Boundary =
        "M1 保留五个 legacy CLR 类型；M1 不授权 M2。";
    private const string M2Boundary =
        "M2 必须另行取得 deliberate public API approval，并把五个 legacy CLR identity 作为原子批次处理。";

    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly (string Legacy, string Canonical)[] MigrationTypes =
    [
        (
            "Bukit.Content.Notion.INotionBlockRenderer",
            "Bukit.Notion.Rendering.INotionBlockRenderer"),
        (
            "Bukit.Content.Notion.NotionBlockTransformer",
            "Bukit.Notion.Rendering.NotionBlockTransformer"),
        (
            "Bukit.Content.Notion.NotionBlockRendererRegistry",
            "Bukit.Notion.Rendering.NotionBlockRendererRegistry"),
        (
            "Bukit.Content.Notion.NotionRenderContext",
            "Bukit.Notion.Rendering.NotionRenderContext"),
        (
            "Bukit.Content.Notion.NotionBlocksRenderer",
            "Bukit.Notion.Rendering.NotionBlocksRenderer")
    ];

    [Fact]
    public void M1_KeepsLegacyAndCanonicalExtensionGraphTypesPublicUnderExactIdentities()
    {
        var legacyAssembly = typeof(Bukit.Content.Notion.NotionApiClient).Assembly;
        var canonicalAssembly = typeof(Bukit.Notion.Transport.NotionClient).Assembly;

        Assert.Equal("Bukit.Content", legacyAssembly.GetName().Name);
        Assert.Equal("Bukit.Notion", canonicalAssembly.GetName().Name);

        foreach (var (legacyName, canonicalName) in MigrationTypes)
        {
            AssertPublicTypeResolves(legacyAssembly, legacyName);
            AssertPublicTypeResolves(canonicalAssembly, canonicalName);
        }
    }

    [Fact]
    public void M1_KeepsGovernedBaselineAtFourteenAssembliesFiveHundredFourteenTypesAndOneHundredTenCandidates()
    {
        using var document = ReadJson(
            "docs",
            "governance",
            "bukit-core-public-api-baseline.v1.json");
        var root = document.RootElement;
        var types = root.GetProperty("types").EnumerateArray().ToArray();

        Assert.Equal("bukit-core-public-api-baseline-v1", root.GetProperty("schema").GetString());
        Assert.Equal(14, root.GetProperty("assemblies").GetArrayLength());
        Assert.Equal(514, types.Length);
        Assert.Equal(110, types.Count(type =>
            type.GetProperty("compatibility").GetString() == "2.0-candidate"));

        foreach (var (legacyName, _) in MigrationTypes)
        {
            var entry = Assert.Single(types, type =>
                type.GetProperty("assembly").GetString() == "Bukit.Content" &&
                type.GetProperty("name").GetString() == legacyName);

            Assert.Equal("implementation-public", entry.GetProperty("classification").GetString());
            Assert.Equal("2.0-candidate", entry.GetProperty("compatibility").GetString());
            Assert.Equal("2.0-review", entry.GetProperty("migrationHorizon").GetString());
        }
    }

    [Fact]
    public void M1_KeepsClosedCandidateManifestByteIdenticalAndPreservesLegacyEntries()
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

        foreach (var (legacyName, _) in MigrationTypes)
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
        var actualBlob = Convert.ToHexStringLower(SHA1.HashData(blobBytes));

        Assert.Equal(CandidateManifestBlob, actualBlob);
    }

    [Fact]
    public void CanonicalNotionProject_RemainsFreeOfProjectAndPackageDependencies()
    {
        var projectPath = Path.Combine(
            RepoRoot,
            "src",
            "Bukit-Core",
            "Bukit.Notion",
            "Bukit.Notion.csproj");
        var project = XDocument.Load(projectPath);

        Assert.Empty(project.Descendants("ProjectReference"));
        Assert.Empty(project.Descendants("PackageReference"));
    }

    [Fact]
    public void MigrationGuide_RecordsCompleteSourceContractAndM1M2Boundary()
    {
        var guidePath = Path.Combine(RepoRoot, GuideRelativePath);

        Assert.True(File.Exists(guidePath), $"Missing M1 migration guide: {guidePath}");

        var guide = File.ReadAllText(guidePath);

        Assert.Contains(ProvisionalStatus, guide, StringComparison.Ordinal);
        Assert.Contains(M1Boundary, guide, StringComparison.Ordinal);
        Assert.Contains(M2Boundary, guide, StringComparison.Ordinal);
        Assert.Contains("14 个程序集、514 个类型、110 个 `2.0-candidate`", guide, StringComparison.Ordinal);
        Assert.Contains("136-entry candidate manifest", guide, StringComparison.Ordinal);
        Assert.Contains(CandidateManifestBlob, guide, StringComparison.Ordinal);
        Assert.Contains("`Bukit.Notion.csproj` 保持 0 `ProjectReference` / 0 `PackageReference`", guide, StringComparison.Ordinal);

        foreach (var (legacyName, canonicalName) in MigrationTypes)
        {
            Assert.Contains($"`{legacyName}`", guide, StringComparison.Ordinal);
            Assert.Contains($"`{canonicalName}`", guide, StringComparison.Ordinal);
        }

        string[] requiredSourceContracts =
        [
            "public sealed class LegacyCustomRenderer : INotionBlockRenderer",
            "public sealed class CanonicalCustomRenderer : INotionBlockRenderer",
            "NotionBlockTransformer transformer =",
            "NotionBlockRendererRegistry.CreateDefault()",
            "NotionRenderContext context",
            "new NotionBlocksRenderer(client, registry)",
            "ApiVersion = NotionApiUrls.NotionVersion",
            "Timeout = TimeSpan.FromSeconds(30)",
            "RequestDelayMs = legacyOptions.RequestDelayMs",
            "MaxRetries = legacyOptions.MaxRetries",
            "MaxRps = legacyOptions.MaxRps",
            "NotionRequestSemantics.IdempotentRead",
            "NotionRequestSemantics.NonReplayableWrite",
            "ContentException",
            "NotionRenderingException",
            "NotionApiException",
            "OperationCanceledException",
            "RenderChildrenAsync",
            "renderer 不拥有 client",
            "injected `HttpClient` 仍由 caller 拥有",
            "internally-created `HttpClient` 由 `NotionClient` 拥有",
            "source break",
            "binary break",
            "type forwarding",
            "unknown-until-voluntary-declaration",
            "新证据回退规则"
        ];

        Assert.All(requiredSourceContracts, contract =>
            Assert.Contains(contract, guide, StringComparison.Ordinal));

        Assert.DoesNotContain("状态：已完成", guide, StringComparison.Ordinal);
        Assert.DoesNotContain("parent aggregate：PASS", guide, StringComparison.Ordinal);
        Assert.DoesNotContain("独立复审：PASS", guide, StringComparison.Ordinal);
    }

    private static void AssertPublicTypeResolves(
        System.Reflection.Assembly assembly,
        string fullName)
    {
        var type = assembly.GetType(fullName, throwOnError: false, ignoreCase: false);

        Assert.NotNull(type);
        Assert.True(type.IsPublic, $"Expected public CLR identity: {fullName}");
        Assert.Same(
            type,
            Type.GetType(
                $"{fullName}, {assembly.GetName().Name}",
                throwOnError: false,
                ignoreCase: false));
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
