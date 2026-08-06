using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bukit.Engine;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class G04D9GPluginSourceCapabilityGraphTests
{
    private const string CandidateManifestBlob =
        "7b07d6890562387010b52301e9f8716e9bf10ed1";
    private const string CurrentBaselineStatement =
        "The current public API baseline contains 425 types, including 0 `2.0-candidate` entries.";
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string[] TypeNames =
    [
        "Bukit.Engine.Plugins.BuiltInPluginSource",
        "Bukit.Engine.Plugins.IPluginSource",
        "Bukit.Engine.Plugins.PluginCapability"
    ];

    [Fact]
    public void SourceAndCapabilityGraph_ExistsInternalAndNotExported()
    {
        Assembly assembly = typeof(PluginRegistry).Assembly;
        Type[] exported = assembly.GetExportedTypes();
        Type source = GetType(assembly, TypeNames[0]);
        Type sourceInterface = GetType(assembly, TypeNames[1]);
        Type capability = GetType(assembly, TypeNames[2]);

        Assert.True(source.IsNotPublic);
        Assert.True(sourceInterface.IsNotPublic);
        Assert.True(sourceInterface.IsInterface);
        Assert.Contains(sourceInterface, source.GetInterfaces());
        Assert.True(capability.IsNotPublic);
        Assert.True(capability.IsAbstract);
        Assert.True(capability.IsSealed);
        Assert.All(TypeNames, name =>
            Assert.DoesNotContain(GetType(assembly, name), exported));

        Assert.Equal(
            "emit-outputs",
            capability.GetField("EmitOutputs")!.GetRawConstantValue());
        Assert.Equal(
            "derive-pages",
            capability.GetField("DerivePages")!.GetRawConstantValue());
    }

    [Fact]
    public void StableRegistryFacade_KeepsPluginTupleContract()
    {
        MethodInfo method = Assert.Single(
            typeof(PluginRegistry).GetMethods(
                BindingFlags.Public |
                BindingFlags.Static |
                BindingFlags.DeclaredOnly));
        Assert.Equal("GetAllPlugins", method.Name);
        Assert.Equal(
            [typeof(BuildContext)],
            method.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray());
        Assert.Contains(
            "IBukitPlugin",
            method.ReturnType.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void CurrentBaselineManifestAndDocs_RecordD9GTerminalState()
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
        Assert.All(TypeNames, name =>
            Assert.DoesNotContain(types, entry =>
                entry.GetProperty("name").GetString() == name));

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
            Assert.Contains("G-04D9G", content, StringComparison.Ordinal);
            Assert.Contains("CG-019", content, StringComparison.Ordinal);
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
