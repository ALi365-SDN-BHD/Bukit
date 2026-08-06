using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bukit.Engine;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class G04D9HListTemplateCapabilityGraphTests
{
    private const string BuilderTypeName =
        "Bukit.Engine.SpecialListRouteBuilder";
    private const string CandidateManifestBlob =
        "7b07d6890562387010b52301e9f8716e9bf10ed1";
    private const string CurrentBaselineStatement =
        "The current public API baseline contains 425 types, including 0 `2.0-candidate` entries.";
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void SpecialListRouteBuilder_ExistsInternalAndNotExported()
    {
        Assembly assembly = typeof(TemplateCapabilitiesResolver).Assembly;
        Type builder = assembly.GetType(BuilderTypeName, true, false)!;

        Assert.True(builder.IsNotPublic);
        Assert.True(builder.IsAbstract);
        Assert.True(builder.IsSealed);
        Assert.DoesNotContain(builder, assembly.GetExportedTypes());
    }

    [Fact]
    public void TemplateCompanions_RemainPublicAndPropagatedByStableParents()
    {
        Assembly assembly = typeof(TemplateCapabilitiesResolver).Assembly;
        Type[] exported = assembly.GetExportedTypes();
        Type[] retained =
        [
            typeof(TemplateCapabilitiesResolver.ListPageContentResolution),
            typeof(TemplateCapabilitiesResolver.TemplateCapabilityFlags),
            typeof(TemplateCapabilitiesResolver.TemplateFieldDeclaration),
            typeof(TemplateVariableWarning)
        ];
        Assert.All(retained, type => Assert.Contains(type, exported));

        MethodInfo resolve = typeof(TemplateCapabilitiesResolver).GetMethod(
            nameof(TemplateCapabilitiesResolver.ResolveListPageContent))!;
        MethodInfo capabilities = typeof(TemplateCapabilitiesResolver).GetMethod(
            nameof(TemplateCapabilitiesResolver.GetCapabilities))!;
        Assert.Equal(
            typeof(TemplateCapabilitiesResolver.ListPageContentResolution),
            resolve.ReturnType);
        Assert.Equal(
            typeof(TemplateCapabilitiesResolver.TemplateCapabilityFlags),
            capabilities.ReturnType);

        Assert.Equal(
            typeof(List<TemplateCapabilitiesResolver.TemplateFieldDeclaration>),
            typeof(TemplateCapabilitiesResolver.TemplateCapabilityFlags)
                .GetProperty("Fields")!
                .PropertyType);

        Assert.All(
            typeof(ScribanTemplateLinter).GetMethods(
                BindingFlags.Public |
                BindingFlags.Static |
                BindingFlags.DeclaredOnly)
                .Where(method => method.Name.StartsWith(
                    "Lint",
                    StringComparison.Ordinal)),
            method => Assert.Equal(
                typeof(List<TemplateVariableWarning>),
                method.ReturnType));
    }

    [Fact]
    public void CurrentBaseline_HasNoUnresolvedCandidateAndRetainsFourCompanions()
    {
        using JsonDocument current = ReadJson(
            "docs", "governance",
            "bukit-core-public-api-baseline.v1.json");
        JsonElement[] types = current.RootElement.GetProperty("types")
            .EnumerateArray()
            .ToArray();
        Assert.Equal(427, types.Length);
        Assert.Equal(0, types.Count(entry =>
            entry.GetProperty("compatibility").GetString() ==
            "2.0-candidate"));
        Assert.DoesNotContain(types, entry =>
            entry.GetProperty("name").GetString() == BuilderTypeName);

        string[] retained =
        [
            "Bukit.Engine.TemplateCapabilitiesResolver+ListPageContentResolution",
            "Bukit.Engine.TemplateCapabilitiesResolver+TemplateCapabilityFlags",
            "Bukit.Engine.TemplateCapabilitiesResolver+TemplateFieldDeclaration",
            "Bukit.Engine.TemplateVariableWarning"
        ];
        foreach (string typeName in retained)
        {
            JsonElement entry = Assert.Single(types, candidate =>
                candidate.GetProperty("name").GetString() == typeName);
            Assert.Equal(
                "cross-assembly-implementation",
                entry.GetProperty("classification").GetString());
            Assert.Equal(
                "1.x-do-not-narrow",
                entry.GetProperty("compatibility").GetString());
        }
    }

    [Fact]
    public void HistoricalManifestAndActiveDocs_RecordD9HTerminalState()
    {
        string manifestPath = Path.Combine(
            RepoRoot, "docs", "governance",
            "bukit-core-2.0-public-surface-candidates.v1.json");
        byte[] bytes = File.ReadAllBytes(manifestPath);
        byte[] prefix = Encoding.UTF8.GetBytes($"blob {bytes.Length}\0");
        var blobBytes = new byte[prefix.Length + bytes.Length];
        prefix.CopyTo(blobBytes, 0);
        bytes.CopyTo(blobBytes, prefix.Length);
        Assert.Equal(
            CandidateManifestBlob,
            Convert.ToHexStringLower(SHA1.HashData(blobBytes)));

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
            Assert.Contains("G-04D9H", content, StringComparison.Ordinal);
            Assert.Contains("TemplateVariableWarning", content, StringComparison.Ordinal);
        }
    }

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
