using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class G04D3BNotionStatsTests
{
    private const string LegacyTypeName =
        "Bukit.Content.Notion.NotionClientStats";
    private const string CanonicalTypeName =
        "Bukit.Notion.Transport.NotionClientStats";
    private const string CandidateManifestBlob =
        "7b07d6890562387010b52301e9f8716e9bf10ed1";
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void LegacyStatsIdentity_IsAbsentAndCanonicalStatsRemainsPublic()
    {
        Assembly contentAssembly =
            typeof(Bukit.Content.Notion.NotionApiClient).Assembly;
        Assembly notionAssembly =
            typeof(Bukit.Notion.Transport.NotionClientStats).Assembly;

        Assert.Null(contentAssembly.GetType(
            LegacyTypeName,
            throwOnError: false,
            ignoreCase: false));
        Assert.DoesNotContain(
            contentAssembly.GetExportedTypes(),
            type => type.FullName == LegacyTypeName);

        Type canonical = notionAssembly.GetType(
            CanonicalTypeName,
            throwOnError: true,
            ignoreCase: false)!;
        Assert.True(canonical.IsPublic);
        Assert.Contains(canonical, notionAssembly.GetExportedTypes());
        Assert.Equal(
            [
                ("RequestCount", typeof(long)),
                ("ThrottleWaitCount", typeof(long)),
                ("ThrottleWaitTotalMs", typeof(long))
            ],
            canonical.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => (property.Name, property.PropertyType))
                .OrderBy(property => property.Name, StringComparer.Ordinal)
                .ToArray());
    }

    [Fact]
    public void LegacyFacade_InternalGetStatsReturnsCanonicalIdentityDirectly()
    {
        MethodInfo method = Assert.Single(
            typeof(Bukit.Content.Notion.NotionApiClient).GetMethods(
                BindingFlags.NonPublic |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly),
            candidate => candidate.Name == "GetStats");

        Assert.True(method.IsAssembly);
        Assert.Equal(
            typeof(Bukit.Notion.Transport.NotionClientStats),
            method.ReturnType);
        Assert.Empty(method.GetParameters());
    }

    [Fact]
    public void CurrentBaseline_RemovesOnlyTheLegacyStatsCandidate()
    {
        using JsonDocument current = ReadJson(
            "docs",
            "governance",
            "bukit-core-public-api-baseline.v1.json");
        JsonElement root = current.RootElement;
        JsonElement[] types = root.GetProperty("types").EnumerateArray().ToArray();

        Assert.Equal(14, root.GetProperty("assemblies").GetArrayLength());
        Assert.Equal(489, types.Length);
        Assert.Equal(63, types.Count(entry =>
            entry.GetProperty("compatibility").GetString() == "2.0-candidate"));
        Assert.DoesNotContain(types, entry =>
            entry.GetProperty("assembly").GetString() == "Bukit.Content" &&
            entry.GetProperty("name").GetString() == LegacyTypeName);

        JsonElement canonical = Assert.Single(types, entry =>
            entry.GetProperty("assembly").GetString() == "Bukit.Notion" &&
            entry.GetProperty("name").GetString() == CanonicalTypeName);
        Assert.Equal(
            "cross-assembly-implementation",
            canonical.GetProperty("classification").GetString());
        Assert.Equal(
            "1.x-do-not-narrow",
            canonical.GetProperty("compatibility").GetString());
    }

    [Fact]
    public void ExistingFriendAssemblyBoundariesRemainExact()
    {
        Assert.Equal(
            [
                "Bukit.Content.Tests",
                "Bukit.Engine",
                "Bukit.Engine.Tests"
            ],
            GetFriendAssemblies(
                typeof(Bukit.Content.Notion.NotionApiClient).Assembly));
        Assert.Equal(
            [
                "Bukit.Content",
                "Bukit.Content.Notion",
                "Bukit.Notion.Tests"
            ],
            GetFriendAssemblies(
                typeof(Bukit.Notion.Transport.NotionClientStats).Assembly));
        Assert.Equal(
            [
                "Bukit.Content",
                "Bukit.Content.Notion.Tests",
                "Bukit.Content.Tests"
            ],
            GetFriendAssemblies(
                typeof(Bukit.Content.Notion.NotionContentSource).Assembly));
    }

    [Fact]
    public void ClosedManifestPreservesLegacyCandidateAndExactBlob()
    {
        string path = Path.Combine(
            RepoRoot,
            "docs",
            "governance",
            "bukit-core-2.0-public-surface-candidates.v1.json");
        byte[] bytes = File.ReadAllBytes(path);
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        JsonElement[] candidates =
            root.GetProperty("candidates").EnumerateArray().ToArray();

        Assert.Equal("closed", root.GetProperty("declarationState").GetString());
        Assert.Equal(136, root.GetProperty("candidateCount").GetInt32());
        Assert.Equal(136, candidates.Length);

        JsonElement legacy = Assert.Single(candidates, entry =>
            entry.GetProperty("assembly").GetString() == "Bukit.Content" &&
            entry.GetProperty("fullName").GetString() == LegacyTypeName);
        Assert.Equal(
            "consumer-declaration-pending",
            legacy.GetProperty("declarationStatus").GetString());
        Assert.Equal(
            "unknown-until-voluntary-declaration",
            legacy.GetProperty("privateConsumerStatus").GetString());
        Assert.Equal(
            "no-public-match-found",
            legacy.GetProperty("externalEvidence")
                .GetProperty("searchStatus")
                .GetString());

        byte[] prefix = Encoding.UTF8.GetBytes($"blob {bytes.Length}\0");
        var blobBytes = new byte[prefix.Length + bytes.Length];
        prefix.CopyTo(blobBytes, 0);
        bytes.CopyTo(blobBytes, prefix.Length);

        Assert.Equal(
            CandidateManifestBlob,
            Convert.ToHexStringLower(SHA1.HashData(blobBytes)));
    }

    private static string[] GetFriendAssemblies(Assembly assembly)
        => assembly
            .GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(attribute => attribute.AssemblyName.Split(',')[0])
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

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
