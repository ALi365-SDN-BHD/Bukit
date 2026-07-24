using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bukit.Engine;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class G04D9DFeedSeoSitemapGraphTests
{
    private const string CandidateManifestBlob =
        "7b07d6890562387010b52301e9f8716e9bf10ed1";
    private const string CurrentBaselineStatement =
        "The current public API baseline contains 425 types, including 0 `2.0-candidate` entries.";
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string[] InternalizedTypeNames =
    [
        "Bukit.Engine.AtomFeedGenerator",
        "Bukit.Engine.JsonFeedGenerator",
        "Bukit.Engine.SitemapGenerator",
        "Bukit.Engine.SitemapGenerator+Alternate",
        "Bukit.Engine.SitemapGenerator+UrlEntry",
        "Bukit.Engine.SeoAlternatesService",
        "Bukit.Engine.SeoInjectionPolicy"
    ];

    [Fact]
    public void ApprovedFeedSeoSitemapTypes_ExistButAreNotExported()
    {
        Assembly assembly = typeof(RssGenerator).Assembly;
        Type[] exported = assembly.GetExportedTypes();

        foreach (string typeName in InternalizedTypeNames)
        {
            Type type = GetType(assembly, typeName);
            Assert.DoesNotContain(type, exported);

            if (!type.IsNested)
            {
                Assert.True(type.IsNotPublic);
            }
            else
            {
                Assert.True(type.DeclaringType!.IsNotPublic);
            }
        }
    }

    [Fact]
    public void SitemapGraph_KeepsNestedRecordShapeInsideInternalParent()
    {
        Assembly assembly = typeof(RssGenerator).Assembly;
        Type sitemap = GetType(assembly, "Bukit.Engine.SitemapGenerator");
        Type alternate = GetType(
            assembly,
            "Bukit.Engine.SitemapGenerator+Alternate");
        Type entry = GetType(
            assembly,
            "Bukit.Engine.SitemapGenerator+UrlEntry");

        Assert.True(sitemap.IsAbstract);
        Assert.True(sitemap.IsSealed);
        Assert.Equal(sitemap, alternate.DeclaringType);
        Assert.Equal(sitemap, entry.DeclaringType);
        Assert.True(alternate.IsSealed);
        Assert.True(entry.IsSealed);
    }

    [Fact]
    public void RssGeneratorAndPost_RemainPublicWithFeedCompanionShape()
    {
        Assembly assembly = typeof(RssGenerator).Assembly;
        Type[] exported = assembly.GetExportedTypes();

        Assert.True(typeof(RssGenerator).IsPublic);
        Assert.True(typeof(RssGenerator).IsAbstract);
        Assert.True(typeof(RssGenerator).IsSealed);
        Assert.True(typeof(RssGenerator.Post).IsNestedPublic);
        Assert.Contains(typeof(RssGenerator), exported);
        Assert.Contains(typeof(RssGenerator.Post), exported);

        Assert.Equal(
            ["BuildAbsoluteUrl", "Generate", "GenerateMerged"],
            typeof(RssGenerator).GetMethods(
                    BindingFlags.Public |
                    BindingFlags.Static |
                    BindingFlags.DeclaredOnly)
                .Select(method => method.Name)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray());
    }

    [Fact]
    public void CurrentBaseline_RecordsSevenInternalAndOneRetained()
    {
        using JsonDocument current = ReadJson(
            "docs",
            "governance",
            "bukit-core-public-api-baseline.v1.json");
        JsonElement root = current.RootElement;
        JsonElement[] types = root.GetProperty("types")
            .EnumerateArray()
            .ToArray();

        Assert.Equal(14, root.GetProperty("assemblies").GetArrayLength());
        Assert.Equal(425, types.Length);
        Assert.Equal(0, types.Count(entry =>
            entry.GetProperty("compatibility").GetString() ==
            "2.0-candidate"));
        Assert.All(InternalizedTypeNames, typeName =>
            Assert.DoesNotContain(types, entry =>
                entry.GetProperty("name").GetString() == typeName));

        JsonElement rss = Assert.Single(types, entry =>
            entry.GetProperty("name").GetString() ==
            "Bukit.Engine.RssGenerator");
        Assert.Equal(
            "cross-assembly-implementation",
            rss.GetProperty("classification").GetString());
        Assert.Equal(
            "1.x-do-not-narrow",
            rss.GetProperty("compatibility").GetString());
        Assert.Contains(types, entry =>
            entry.GetProperty("name").GetString() ==
            "Bukit.Engine.RssGenerator+Post");
    }

    [Fact]
    public void ClosedManifest_PreservesEightHistoricalCandidatesAndExactBlob()
    {
        string path = Path.Combine(
            RepoRoot,
            "docs",
            "governance",
            "bukit-core-2.0-public-surface-candidates.v1.json");
        byte[] bytes = File.ReadAllBytes(path);
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        JsonElement[] candidates = root.GetProperty("candidates")
            .EnumerateArray()
            .ToArray();
        string[] expected =
        [
            .. InternalizedTypeNames,
            "Bukit.Engine.RssGenerator"
        ];

        Assert.Equal("closed", root.GetProperty("declarationState").GetString());
        Assert.Equal(136, root.GetProperty("candidateCount").GetInt32());
        Assert.Equal(136, candidates.Length);
        JsonElement[] historical = candidates
            .Where(entry => expected.Contains(
                entry.GetProperty("fullName").GetString()!,
                StringComparer.Ordinal))
            .ToArray();
        Assert.Equal(8, historical.Length);
        Assert.All(historical, entry =>
        {
            Assert.Equal(
                "consumer-declaration-pending",
                entry.GetProperty("declarationStatus").GetString());
            Assert.Equal(
                "no-public-match-found",
                entry.GetProperty("externalEvidence")
                    .GetProperty("searchStatus")
                    .GetString());
        });

        byte[] prefix = Encoding.UTF8.GetBytes($"blob {bytes.Length}\0");
        var blobBytes = new byte[prefix.Length + bytes.Length];
        prefix.CopyTo(blobBytes, 0);
        bytes.CopyTo(blobBytes, prefix.Length);
        Assert.Equal(
            CandidateManifestBlob,
            Convert.ToHexStringLower(SHA1.HashData(blobBytes)));
    }

    [Fact]
    public void ActiveGovernance_RecordsCurrentBaselineAndD9DDecision()
    {
        foreach (string relativePath in new[]
                 {
                     Path.Combine(
                         "docs",
                         "governance",
                         "bukit-core-2.0-consumer-declaration.md"),
                     Path.Combine(
                         "guide",
                         "dev",
                         "public-api-governance.md")
                 })
        {
            string content = File.ReadAllText(
                Path.Combine(RepoRoot, relativePath));
            Assert.Contains(CurrentBaselineStatement, content);
            Assert.Contains("G-04D9D", content, StringComparison.Ordinal);
            Assert.Contains("external_unverified", content, StringComparison.Ordinal);
            Assert.Contains("RssGenerator", content, StringComparison.Ordinal);
        }
    }

    private static Type GetType(Assembly assembly, string typeName) =>
        assembly.GetType(
            typeName,
            throwOnError: true,
            ignoreCase: false)!;

    private static JsonDocument ReadJson(params string[] relativeSegments)
    {
        string path = Path.Combine([RepoRoot, .. relativeSegments]);
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

        throw new DirectoryNotFoundException(
            "Could not locate the Bukit repository root.");
    }
}
