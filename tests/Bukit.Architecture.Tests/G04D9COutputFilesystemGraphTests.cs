using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bukit.Engine;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class G04D9COutputFilesystemGraphTests
{
    private const string CandidateManifestBlob =
        "7b07d6890562387010b52301e9f8716e9bf10ed1";
    private const string CurrentBaselineStatement =
        "The current public API baseline contains 443 types, including 0 `2.0-candidate` entries.";
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string[] CandidateTypeNames =
    [
        "Bukit.Engine.DirectoryCopy",
        "Bukit.Engine.DirectoryCopyOptions",
        "Bukit.Engine.FileWriter",
        "Bukit.Engine.Incremental.HashUtil",
        "Bukit.Engine.Output.IOutputFileSystem",
        "Bukit.Engine.Output.IOutputPathPolicy",
        "Bukit.Engine.Output.OutputPathSecurityException",
        "Bukit.Engine.Output.SafeOutputFileSystem",
        "Bukit.Engine.Output.SafePathResolver"
    ];

    [Fact]
    public void OutputFilesystemTypes_ExistInternalAndNotExported()
    {
        Assembly assembly = typeof(SiteEngine).Assembly;
        Type[] exported = assembly.GetExportedTypes();

        foreach (string typeName in CandidateTypeNames)
        {
            Type type = GetType(assembly, typeName);

            Assert.True(type.IsNotPublic);
            Assert.DoesNotContain(type, exported);
        }
    }

    [Fact]
    public void OutputFilesystemTypes_KeepTheirApprovedKindsAndRelationships()
    {
        Assembly assembly = typeof(SiteEngine).Assembly;

        foreach (string typeName in new[]
                 {
                     "Bukit.Engine.DirectoryCopy",
                     "Bukit.Engine.FileWriter",
                     "Bukit.Engine.Incremental.HashUtil"
                 })
        {
            Type type = GetType(assembly, typeName);
            Assert.True(type.IsAbstract);
            Assert.True(type.IsSealed);
        }

        Type options = GetType(
            assembly,
            "Bukit.Engine.DirectoryCopyOptions");
        Assert.True(options.IsSealed);
        Assert.False(options.IsAbstract);

        Type outputFileSystem = GetType(
            assembly,
            "Bukit.Engine.Output.IOutputFileSystem");
        Type outputPathPolicy = GetType(
            assembly,
            "Bukit.Engine.Output.IOutputPathPolicy");
        Assert.True(outputFileSystem.IsInterface);
        Assert.True(outputPathPolicy.IsInterface);

        Type safeFileSystem = GetType(
            assembly,
            "Bukit.Engine.Output.SafeOutputFileSystem");
        Type safePathResolver = GetType(
            assembly,
            "Bukit.Engine.Output.SafePathResolver");
        Assert.Contains(outputFileSystem, safeFileSystem.GetInterfaces());
        Assert.Contains(outputPathPolicy, safePathResolver.GetInterfaces());

        Type securityException = GetType(
            assembly,
            "Bukit.Engine.Output.OutputPathSecurityException");
        Assert.Equal(typeof(InvalidOperationException), securityException.BaseType);

        Type destinationComparer = GetType(
            assembly,
            "Bukit.Engine.OutputDestinationIdentityComparer");
        Assert.True(destinationComparer.IsNotPublic);
        Assert.DoesNotContain(destinationComparer, assembly.GetExportedTypes());
    }

    [Fact]
    public void CoreOutputGraph_KeepsExpectedMemberNames()
    {
        Assembly assembly = typeof(SiteEngine).Assembly;

        Assert.Equal(
            ["Copy", "Sync", "SyncFiles", "SyncFilesRecursive"],
            GetType(assembly, "Bukit.Engine.DirectoryCopy")
                .GetMethods(
                    BindingFlags.Public |
                    BindingFlags.Static |
                    BindingFlags.DeclaredOnly)
                .Select(method => method.Name)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray());
        Assert.Equal(
            ["GetSafeFullPath", "WriteUtf8"],
            GetType(assembly, "Bukit.Engine.FileWriter")
                .GetMethods(
                    BindingFlags.Public |
                    BindingFlags.Static |
                    BindingFlags.DeclaredOnly)
                .Select(method => method.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray());
        Assert.Equal(
            [
                "Sha256Hex",
                "Sha256HexForDirectory",
                "ToHexLower"
            ],
            GetType(assembly, "Bukit.Engine.Incremental.HashUtil")
                .GetMethods(
                    BindingFlags.Public |
                    BindingFlags.Static |
                    BindingFlags.DeclaredOnly)
                .Select(method => method.Name)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray());
    }

    [Fact]
    public void CurrentBaseline_RemovesExactlyNineD9CTypes()
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
        Assert.Equal(443, types.Length);
        Assert.Equal(0, types.Count(entry =>
            entry.GetProperty("compatibility").GetString() ==
            "2.0-candidate"));
        Assert.All(CandidateTypeNames, typeName =>
            Assert.DoesNotContain(types, entry =>
                entry.GetProperty("name").GetString() == typeName));
    }

    [Fact]
    public void ClosedManifest_PreservesNineHistoricalCandidatesAndExactBlob()
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

        Assert.Equal("closed", root.GetProperty("declarationState").GetString());
        Assert.Equal(136, root.GetProperty("candidateCount").GetInt32());
        Assert.Equal(136, candidates.Length);
        JsonElement[] historical = candidates
            .Where(entry => CandidateTypeNames.Contains(
                entry.GetProperty("fullName").GetString()!,
                StringComparer.Ordinal))
            .ToArray();
        Assert.Equal(9, historical.Length);
        Assert.All(historical, entry =>
        {
            Assert.Equal(
                "consumer-declaration-pending",
                entry.GetProperty("declarationStatus").GetString());
            Assert.Equal(
                "unknown-until-voluntary-declaration",
                entry.GetProperty("privateConsumerStatus").GetString());
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
    public void ActiveGovernance_RecordsCurrentBaselineAndD9CDecision()
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
            Assert.Contains("G-04D9C", content, StringComparison.Ordinal);
            Assert.Contains("SafePathResolver", content, StringComparison.Ordinal);
            Assert.Contains(
                "OutputDestinationIdentityComparer",
                content,
                StringComparison.Ordinal);
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
