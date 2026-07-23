using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bukit.Content;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class G04D3AContentBodyGraphTests
{
    private const string CandidateManifestBlob =
        "7b07d6890562387010b52301e9f8716e9bf10ed1";
    private static readonly string[] CandidateTypeNames =
    [
        "Bukit.Content.CompositeContentBodyStore",
        "Bukit.Content.DictionaryContentBodyStore",
        "Bukit.Content.Markdown.BasicMarkdownToHtml",
        "Bukit.Content.Markdown.MarkdownBodyStore"
    ];
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void ContentBodyGraphTypes_ExistButAreInternalAndNotExported()
    {
        Assembly assembly = typeof(IContentProvider).Assembly;
        var exportedTypeNames = assembly.GetExportedTypes()
            .Select(type => type.FullName)
            .ToHashSet(StringComparer.Ordinal);

        foreach (string typeName in CandidateTypeNames)
        {
            Type type = assembly.GetType(
                typeName,
                throwOnError: true,
                ignoreCase: false)!;

            Assert.True(type.IsNotPublic);
            Assert.DoesNotContain(typeName, exportedTypeNames);
        }
    }

    [Fact]
    public void LegacyNotionClientStats_IsAbsentAfterTask11CanonicalMigration()
    {
        Assembly assembly = typeof(IContentProvider).Assembly;
        const string typeName = "Bukit.Content.Notion.NotionClientStats";

        Assert.Null(assembly.GetType(
            typeName,
            throwOnError: false,
            ignoreCase: false));
        Assert.DoesNotContain(typeName, assembly.GetExportedTypes()
            .Select(exported => exported.FullName));
    }

    [Fact]
    public void ContentFriendAssemblies_PreserveTheExistingExactBoundary()
    {
        string[] friendAssemblies = typeof(IContentProvider).Assembly
            .GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(attribute => attribute.AssemblyName.Split(',')[0])
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "Bukit.Content.Tests",
                "Bukit.Engine",
                "Bukit.Engine.Tests"
            ],
            friendAssemblies);
    }

    [Fact]
    public void CurrentBaseline_RemovesOnlyTheFourContentBodyGraphCandidates()
    {
        using JsonDocument current = ReadJson(
            "docs",
            "governance",
            "bukit-core-public-api-baseline.v1.json");
        JsonElement root = current.RootElement;
        JsonElement[] currentTypes = root.GetProperty("types").EnumerateArray().ToArray();

        Assert.Equal(14, root.GetProperty("assemblies").GetArrayLength());
        Assert.Equal(486, currentTypes.Length);
        Assert.Equal(60, currentTypes.Count(entry =>
            entry.GetProperty("compatibility").GetString() == "2.0-candidate"));
        Assert.All(CandidateTypeNames, typeName =>
            Assert.DoesNotContain(currentTypes, entry =>
                entry.GetProperty("assembly").GetString() == "Bukit.Content" &&
                entry.GetProperty("name").GetString() == typeName));

    }

    [Fact]
    public void ClosedManifest_PreservesAllFourHistoricalCandidatesAndExactBlob()
    {
        string path = Path.Combine(
            RepoRoot,
            "docs",
            "governance",
            "bukit-core-2.0-public-surface-candidates.v1.json");
        byte[] bytes = File.ReadAllBytes(path);
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        JsonElement[] candidates = root.GetProperty("candidates").EnumerateArray().ToArray();

        Assert.Equal("closed", root.GetProperty("declarationState").GetString());
        Assert.Equal(136, root.GetProperty("candidateCount").GetInt32());
        Assert.Equal(136, candidates.Length);
        foreach (string typeName in CandidateTypeNames)
        {
            JsonElement candidate = Assert.Single(candidates, entry =>
                entry.GetProperty("assembly").GetString() == "Bukit.Content" &&
                entry.GetProperty("fullName").GetString() == typeName);
            Assert.Equal(
                "consumer-declaration-pending",
                candidate.GetProperty("declarationStatus").GetString());
        }

        byte[] prefix = Encoding.UTF8.GetBytes($"blob {bytes.Length}\0");
        var blobBytes = new byte[prefix.Length + bytes.Length];
        prefix.CopyTo(blobBytes, 0);
        bytes.CopyTo(blobBytes, prefix.Length);

        Assert.Equal(
            CandidateManifestBlob,
            Convert.ToHexStringLower(SHA1.HashData(blobBytes)));
    }

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
