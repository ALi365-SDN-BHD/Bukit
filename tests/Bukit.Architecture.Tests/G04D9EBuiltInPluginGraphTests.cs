using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bukit.Config;
using Bukit.Engine;
using Bukit.Engine.Abstractions.Plugins;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class G04D9EBuiltInPluginGraphTests
{
    private const string CandidateManifestBlob =
        "7b07d6890562387010b52301e9f8716e9bf10ed1";
    private const string CurrentBaselineStatement =
        "The current public API baseline contains 425 types, including 0 `2.0-candidate` entries.";
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string[] CandidateSimpleNames =
    [
        "AliasPlugin",
        "ArchivePlugin",
        "DataFilesPlugin",
        "FeedPlugin",
        "ImageProcessingPlugin",
        "LlmsTxtPlugin",
        "MenuPlugin",
        "PagesIndexPlugin",
        "PaginationPlugin",
        "RelatedContentPlugin",
        "SearchIndexPlugin",
        "SitemapPlugin",
        "TaxonomyPlugin"
    ];

    [Fact]
    public void ThirteenBuiltInImplementationTypes_ExistInternalAndNotExported()
    {
        Assembly assembly = typeof(SiteEngine).Assembly;
        Type[] exported = assembly.GetExportedTypes();

        foreach (string simpleName in CandidateSimpleNames)
        {
            Type type = GetType(
                assembly,
                $"Bukit.Engine.Plugins.BuiltIn.{simpleName}");
            Assert.True(type.IsNotPublic);
            Assert.True(type.IsSealed);
            Assert.Contains(typeof(IBukitPlugin), type.GetInterfaces());
            Assert.DoesNotContain(type, exported);
        }
    }

    [Fact]
    public void BuiltInSource_KeepsNineCandidateRegistrationsPlusAnalytics()
    {
        Assembly assembly = typeof(SiteEngine).Assembly;
        Type sourceType = GetType(
            assembly,
            "Bukit.Engine.Plugins.BuiltInPluginSource");
        object source = Activator.CreateInstance(
            sourceType,
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                new AppConfig
                {
                    Site = new SiteConfig
                    {
                        Name = "architecture-test",
                        Title = "Architecture Test"
                    },
                    Content = new ContentConfig()
                }
            ],
            culture: null)!;
        MethodInfo getPlugins = sourceType.GetMethod(
            "GetPlugins",
            BindingFlags.Public |
            BindingFlags.Instance |
            BindingFlags.DeclaredOnly)!;
        var plugins = Assert.IsAssignableFrom<IEnumerable<IBukitPlugin>>(
            getPlugins.Invoke(source, null));
        string[] registered = plugins
            .Select(plugin => plugin.GetType().Name)
            .ToArray();

        Assert.Equal(
            [
                "AnalyticsPlugin",
                "DataFilesPlugin",
                "PagesIndexPlugin",
                "TaxonomyPlugin",
                "PaginationPlugin",
                "ArchivePlugin",
                "RelatedContentPlugin",
                "AliasPlugin",
                "MenuPlugin",
                "ImageProcessingPlugin"
            ],
            registered);
        Assert.DoesNotContain("FeedPlugin", registered);
        Assert.DoesNotContain("LlmsTxtPlugin", registered);
        Assert.DoesNotContain("SearchIndexPlugin", registered);
        Assert.DoesNotContain("SitemapPlugin", registered);
    }

    [Fact]
    public void CurrentBaseline_RemovesExactlyThirteenBuiltInTypes()
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
        Assert.All(CandidateSimpleNames, simpleName =>
            Assert.DoesNotContain(types, entry =>
                entry.GetProperty("name").GetString() ==
                $"Bukit.Engine.Plugins.BuiltIn.{simpleName}"));
    }

    [Fact]
    public void ClosedManifest_PreservesThirteenHistoricalCandidatesAndExactBlob()
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
        string[] expected = CandidateSimpleNames
            .Select(name => $"Bukit.Engine.Plugins.BuiltIn.{name}")
            .ToArray();

        Assert.Equal(136, candidates.Length);
        Assert.Equal(
            13,
            candidates.Count(entry => expected.Contains(
                entry.GetProperty("fullName").GetString()!,
                StringComparer.Ordinal)));

        byte[] prefix = Encoding.UTF8.GetBytes($"blob {bytes.Length}\0");
        var blobBytes = new byte[prefix.Length + bytes.Length];
        prefix.CopyTo(blobBytes, 0);
        bytes.CopyTo(blobBytes, prefix.Length);
        Assert.Equal(
            CandidateManifestBlob,
            Convert.ToHexStringLower(SHA1.HashData(blobBytes)));
    }

    [Fact]
    public void ActiveGovernance_RecordsCurrentBaselineAndD9EDecision()
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
            Assert.Contains("G-04D9E", content, StringComparison.Ordinal);
            Assert.Matches(
                @"(?is)\b(?:9|nine)\b.{0,80}\bregistry-owned\b",
                content);
            Assert.Matches(
                @"(?is)\b(?:4|four)\b.{0,80}\baggregate-only\b",
                content);
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
