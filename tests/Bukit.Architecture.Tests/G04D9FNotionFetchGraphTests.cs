using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bukit.Engine;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class G04D9FNotionFetchGraphTests
{
    private const string FetcherTypeName =
        "Bukit.Engine.Plugins.BuiltIn.INotionPageFetcher";
    private const string PageTypeName =
        "Bukit.Engine.Plugins.BuiltIn.NotionFetchedPage";
    private const string CandidateManifestBlob =
        "7b07d6890562387010b52301e9f8716e9bf10ed1";
    private const string CurrentBaselineStatement =
        "The current public API baseline contains 425 types, including 0 `2.0-candidate` entries.";
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void NotionFetchTypes_ExistInternalAndKeepAtomicInterfaceGraph()
    {
        Assembly assembly = typeof(SiteEngine).Assembly;
        Type fetcher = GetType(assembly, FetcherTypeName);
        Type page = GetType(assembly, PageTypeName);

        Assert.True(fetcher.IsNotPublic);
        Assert.True(fetcher.IsInterface);
        Assert.True(page.IsNotPublic);
        Assert.True(page.IsSealed);
        Assert.DoesNotContain(fetcher, assembly.GetExportedTypes());
        Assert.DoesNotContain(page, assembly.GetExportedTypes());

        MethodInfo fetch = Assert.Single(fetcher.GetMethods());
        Assert.Equal("FetchAsync", fetch.Name);
        Assert.Equal(
            typeof(Task<>).MakeGenericType(page),
            fetch.ReturnType);
        Assert.Contains(
            typeof(CancellationToken),
            fetch.GetParameters()
                .Select(parameter => parameter.ParameterType));
    }

    [Fact]
    public void CurrentBaselineAndHistoricalManifest_RecordD9FTerminalState()
    {
        using JsonDocument current = ReadJson(
            "docs",
            "governance",
            "bukit-core-public-api-baseline.v1.json");
        JsonElement root = current.RootElement;
        JsonElement[] types = root.GetProperty("types")
            .EnumerateArray()
            .ToArray();
        Assert.Equal(425, types.Length);
        Assert.Equal(0, types.Count(entry =>
            entry.GetProperty("compatibility").GetString() ==
            "2.0-candidate"));
        Assert.DoesNotContain(types, entry =>
            entry.GetProperty("name").GetString() == FetcherTypeName);
        Assert.DoesNotContain(types, entry =>
            entry.GetProperty("name").GetString() == PageTypeName);

        string path = Path.Combine(
            RepoRoot,
            "docs",
            "governance",
            "bukit-core-2.0-public-surface-candidates.v1.json");
        byte[] bytes = File.ReadAllBytes(path);
        using JsonDocument manifest = JsonDocument.Parse(bytes);
        JsonElement[] historical = manifest.RootElement
            .GetProperty("candidates")
            .EnumerateArray()
            .Where(entry =>
                entry.GetProperty("fullName").GetString() is
                    FetcherTypeName or PageTypeName)
            .ToArray();
        Assert.Equal(2, historical.Length);

        byte[] prefix = Encoding.UTF8.GetBytes($"blob {bytes.Length}\0");
        var blobBytes = new byte[prefix.Length + bytes.Length];
        prefix.CopyTo(blobBytes, 0);
        bytes.CopyTo(blobBytes, prefix.Length);
        Assert.Equal(
            CandidateManifestBlob,
            Convert.ToHexStringLower(SHA1.HashData(blobBytes)));
    }

    [Fact]
    public void ActiveGovernance_RecordsCurrentBaselineAndD9FDecision()
    {
        foreach (string relativePath in new[]
                 {
                     Path.Combine("docs", "governance",
                         "bukit-core-2.0-consumer-declaration.md"),
                     Path.Combine("guide", "dev",
                         "public-api-governance.md")
                 })
        {
            string content = File.ReadAllText(
                Path.Combine(RepoRoot, relativePath));
            Assert.Contains(CurrentBaselineStatement, content);
            Assert.Contains("G-04D9F", content, StringComparison.Ordinal);
            Assert.Contains("INotionPageFetcher", content, StringComparison.Ordinal);
        }
    }

    private static Type GetType(Assembly assembly, string typeName) =>
        assembly.GetType(typeName, true, false)!;

    private static JsonDocument ReadJson(params string[] relativeSegments) =>
        JsonDocument.Parse(File.ReadAllText(
            Path.Combine([RepoRoot, .. relativeSegments])));

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
