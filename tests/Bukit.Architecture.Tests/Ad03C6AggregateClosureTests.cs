using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class Ad03C6AggregateClosureTests
{
    private const string CandidateManifestBlob =
        "7b07d6890562387010b52301e9f8716e9bf10ed1";
    private const string ParserTypeName =
        "Bukit.Content.Notion.NotionPropertyParser";
    private const string NoticePath =
        "docs/governance/bukit-core-2.0-notion-compatibility-migration.md";
    private const string ClosurePath =
        "docs/analysis/bukit-core-ad03c-final-aggregate-closure-2026-07-24.zh-CN.md";
    private const string C0Path =
        "docs/analysis/bukit-core-ad03c-eligibility-migration-contract-audit-2026-07-24.zh-CN.md";
    private static readonly string RepoRoot = FindRepoRoot();

    private static readonly (string Assembly, string Name)[] RemovedTypes =
    [
        ("Bukit.Shared", "Bukit.Shared.Notion.BulletedListItemBlock"),
        ("Bukit.Shared", "Bukit.Shared.Notion.CalloutBlock"),
        ("Bukit.Shared", "Bukit.Shared.Notion.CodeBlock"),
        ("Bukit.Shared", "Bukit.Shared.Notion.Heading1Block"),
        ("Bukit.Shared", "Bukit.Shared.Notion.Heading2Block"),
        ("Bukit.Shared", "Bukit.Shared.Notion.Heading3Block"),
        ("Bukit.Shared", "Bukit.Shared.Notion.HtmlToNotionBlockConverter"),
        ("Bukit.Shared", "Bukit.Shared.Notion.ImageBlock"),
        ("Bukit.Shared", "Bukit.Shared.Notion.NotionApiUrls"),
        ("Bukit.Shared", "Bukit.Shared.Notion.NotionBlock"),
        ("Bukit.Shared", "Bukit.Shared.Notion.NumberedListItemBlock"),
        ("Bukit.Shared", "Bukit.Shared.Notion.ParagraphBlock"),
        ("Bukit.Shared", "Bukit.Shared.Notion.QuoteBlock"),
        ("Bukit.Shared", "Bukit.Shared.Notion.RichTextSegment"),
        ("Bukit.Shared", "Bukit.Shared.Notion.ToggleBlock"),
        ("Bukit.Content", "Bukit.Content.Notion.NotionApiClient"),
        ("Bukit.Content", "Bukit.Content.Notion.NotionContentProvider"),
        ("Bukit.Content", "Bukit.Content.Notion.NotionProviderOptions")
    ];

    [Fact]
    public void GovernedBaseline_RecordsExactFinalSurfaceAndDistributions()
    {
        using JsonDocument document = ReadJson(
            "docs",
            "governance",
            "bukit-core-public-api-baseline.v1.json");
        JsonElement root = document.RootElement;
        JsonElement[] types = root.GetProperty("types").EnumerateArray().ToArray();

        Assert.Equal(14, root.GetProperty("assemblies").GetArrayLength());
        Assert.Equal(425, types.Length);
        Assert.Equal(
            0,
            types.Count(entry =>
                entry.GetProperty("compatibility").GetString() ==
                "2.0-candidate"));

        AssertDistribution(
            types,
            "classification",
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["cross-assembly-implementation"] = 256,
                ["implementation-public"] = 41,
                ["serialized-contract"] = 96,
                ["plugin-wire-contract"] = 23,
                ["persisted-internal-format"] = 6,
                ["aot-serialization-surface"] = 3
            });
        AssertDistribution(
            types,
            "compatibility",
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["1.x-do-not-narrow"] = 260,
                ["1.x-shape-stable"] = 119,
                ["not-a-clr-contract"] = 40,
                ["1.x-migration-safe"] = 6
            });
        AssertDistribution(
            types,
            "migrationHorizon",
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["2.0-review"] = 303,
                ["retain-1.x"] = 122
            });
    }

    [Fact]
    public void GovernedBaseline_RemovesExactLegacySetAndRetainsParserMetadataAndShape()
    {
        using JsonDocument document = ReadJson(
            "docs",
            "governance",
            "bukit-core-public-api-baseline.v1.json");
        JsonElement[] types = document.RootElement
            .GetProperty("types")
            .EnumerateArray()
            .ToArray();

        foreach ((string assembly, string name) in RemovedTypes)
        {
            Assert.DoesNotContain(types, entry =>
                entry.GetProperty("assembly").GetString() == assembly &&
                entry.GetProperty("name").GetString() == name);
        }

        JsonElement parser = Assert.Single(types, entry =>
            entry.GetProperty("assembly").GetString() == "Bukit.Content" &&
            entry.GetProperty("name").GetString() == ParserTypeName);
        Assert.Equal(
            "implementation-public",
            parser.GetProperty("classification").GetString());
        Assert.Equal(
            "1.x-do-not-narrow",
            parser.GetProperty("compatibility").GetString());
        Assert.Equal(
            "2.0-review",
            parser.GetProperty("migrationHorizon").GetString());
        Assert.Equal(
            "public static class Bukit.Content.Notion.NotionPropertyParser",
            parser.GetProperty("signature").GetString());
        Assert.Equal(
            [
                "public static System.Collections.Generic.IReadOnlyDictionary<System.String!, Bukit.Engine.Abstractions.Content.ContentField!>! ExtractAllFields(System.Text.Json.JsonElement properties)",
                "public static System.Collections.Generic.IReadOnlyDictionary<System.String!, Bukit.Engine.Abstractions.Content.ContentField!>! ExtractFields(System.Text.Json.JsonElement properties)"
            ],
            parser.GetProperty("publicMembers")
                .EnumerateArray()
                .Select(member => member.GetString()!)
                .ToArray());
        Assert.Empty(parser.GetProperty("protectedMembers").EnumerateArray());
    }

    [Fact]
    public void MigrationNotice_RecordsCompleteBreakingChangeContract()
    {
        string notice = ReadText(NoticePath);

        Assert.Contains("2.0-only", notice, StringComparison.Ordinal);
        Assert.Contains("1.x remains unchanged", notice, StringComparison.Ordinal);
        foreach ((string assembly, string name) in RemovedTypes)
        {
            Assert.Contains($"`{name}`", notice, StringComparison.Ordinal);
            Assert.Contains($"`{assembly}.dll`", notice, StringComparison.Ordinal);
        }

        Assert.Contains("`Bukit.Notion.NotionApiUrls`", notice, StringComparison.Ordinal);
        Assert.Contains(
            "`Bukit.Notion.Conversion.HtmlToNotionBlockConverter`",
            notice,
            StringComparison.Ordinal);
        Assert.Contains("`Bukit.Notion.Blocks`", notice, StringComparison.Ordinal);
        Assert.Contains(
            "`Bukit.Content.Notion.NotionContentSource`",
            notice,
            StringComparison.Ordinal);
        Assert.Contains("not a drop-in", notice, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not a productized public SDK", notice, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("update assembly references", notice, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("recompile", notice, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("reflection", notice, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("serializer", notice, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("assembly-qualified", notice, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no mechanical migration", notice, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("private", notice, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unindexed", notice, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("binary-only", notice, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("undisclosed", notice, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(ParserTypeName, notice, StringComparison.Ordinal);
        Assert.Contains("`Bukit.Content.dll`", notice, StringComparison.Ordinal);
        Assert.Contains("TLS", notice, StringComparison.Ordinal);
        Assert.Contains("retry", notice, StringComparison.Ordinal);
        Assert.Contains("cache", notice, StringComparison.Ordinal);
        Assert.Contains("schema", notice, StringComparison.Ordinal);
        Assert.Contains("protocol", notice, StringComparison.Ordinal);
        Assert.Contains("assets", notice, StringComparison.Ordinal);
        Assert.Contains("SEO", notice, StringComparison.Ordinal);
        Assert.Contains("path", notice, StringComparison.Ordinal);
    }

    [Fact]
    public void ClosureLedger_RecordsC0ThroughC5DeltasAndClosedStatus()
    {
        string ledger = ReadText(ClosurePath);
        var expectedCommits = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AD-03C0"] = "38dbc0fb",
            ["AD-03C1"] = "fafed2bd",
            ["AD-03C2"] = "ed226179",
            ["AD-03C3"] = "9ef16a6a",
            ["AD-03C4"] = "6d053a13",
            ["AD-03C5"] = "1caa0482"
        };

        foreach ((string task, string commit) in expectedCommits)
        {
            Assert.Contains(task, ledger, StringComparison.Ordinal);
            Assert.Contains(commit, ledger, StringComparison.Ordinal);
        }

        Assert.Contains("正式关闭", ledger, StringComparison.Ordinal);
        Assert.Contains("19 -> 1", ledger, StringComparison.Ordinal);
        Assert.Contains("public types | -18", ledger, StringComparison.Ordinal);
        Assert.Contains("test-only helpers | -2", ledger, StringComparison.Ordinal);
        Assert.Contains("compatibility project references | -1", ledger, StringComparison.Ordinal);
        Assert.Contains("14 / 425 / 0", ledger, StringComparison.Ordinal);
        Assert.Contains(ParserTypeName, ledger, StringComparison.Ordinal);
        Assert.Contains("retain-by-design", ledger, StringComparison.Ordinal);
        Assert.Contains(CandidateManifestBlob, ledger, StringComparison.Ordinal);
    }

    [Fact]
    public void ActiveGovernanceUsesCurrentCountAndC0RecordsActualOutcome()
    {
        string guide = ReadText("guide/dev/public-api-governance.md");
        string declaration =
            ReadText("docs/governance/bukit-core-2.0-consumer-declaration.md");
        string c0 = ReadText(C0Path);

        Assert.Contains("425 public types", guide, StringComparison.Ordinal);
        Assert.DoesNotContain("443 public types", guide, StringComparison.Ordinal);
        Assert.Contains("425 types", declaration, StringComparison.Ordinal);
        Assert.DoesNotContain("443 types", declaration, StringComparison.Ordinal);

        Assert.Contains("C1-C6 complete", c0, StringComparison.Ordinal);
        Assert.Contains("14 / 425 / 0", c0, StringComparison.Ordinal);
        Assert.Contains("18 removed", c0, StringComparison.Ordinal);
        Assert.Contains("one retained", c0, StringComparison.Ordinal);
        Assert.Contains("G-04 已以 443 public types / 0 candidates 关闭", c0, StringComparison.Ordinal);
        Assert.Contains(
            "bukit-core-2.0-notion-compatibility-migration.md",
            c0,
            StringComparison.Ordinal);
        Assert.Contains(
            "bukit-core-ad03c-final-aggregate-closure-2026-07-24.zh-CN.md",
            c0,
            StringComparison.Ordinal);
        Assert.Contains(
            $"blob/e16142331111060a09385fb29fdf72c28da260c4/src/Bukit-Core/Bukit.Shared/Notion/NotionBlockTypes.cs",
            c0,
            StringComparison.Ordinal);
    }

    [Fact]
    public void HistoricalCandidateManifest_RemainsClosedAndByteExact()
    {
        string path = Path.Combine(
            RepoRoot,
            "docs",
            "governance",
            "bukit-core-2.0-public-surface-candidates.v1.json");
        byte[] bytes = File.ReadAllBytes(path);
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;

        Assert.Equal("closed", root.GetProperty("declarationState").GetString());
        Assert.Equal(136, root.GetProperty("candidateCount").GetInt32());
        Assert.Equal(136, root.GetProperty("candidates").GetArrayLength());

        byte[] prefix = Encoding.UTF8.GetBytes($"blob {bytes.Length}\0");
        var blobBytes = new byte[prefix.Length + bytes.Length];
        prefix.CopyTo(blobBytes, 0);
        bytes.CopyTo(blobBytes, prefix.Length);

        Assert.Equal(
            CandidateManifestBlob,
            Convert.ToHexStringLower(SHA1.HashData(blobBytes)));
    }

    private static void AssertDistribution(
        IEnumerable<JsonElement> entries,
        string propertyName,
        IReadOnlyDictionary<string, int> expected)
    {
        Dictionary<string, int> actual = entries
            .GroupBy(
                entry => entry.GetProperty(propertyName).GetString()!,
                StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Count(),
                StringComparer.Ordinal);

        Assert.Equal(expected, actual);
    }

    private static JsonDocument ReadJson(params string[] relativeSegments)
        => JsonDocument.Parse(File.ReadAllText(
            Path.Combine([RepoRoot, .. relativeSegments])));

    private static string ReadText(string relativePath)
        => File.ReadAllText(Path.Combine(
            RepoRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));

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

        throw new DirectoryNotFoundException(
            "Could not locate the Bukit repository root.");
    }
}
