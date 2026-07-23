using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class G04D2DPermissionGraphTests
{
    private const string CandidateManifestBlob =
        "7b07d6890562387010b52301e9f8716e9bf10ed1";
    private const string FileSystemEvaluatorTypeName =
        "Bukit.PluginHost.PluginFileSystemPermissionEvaluator";
    private const string PathNormalizerTypeName =
        "Bukit.PluginHost.PluginPermissionPathNormalizer";
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void PermissionCandidates_ExistButAreInternalAndNotExported()
    {
        var assembly = typeof(Bukit.PluginHost.PluginPermissionEvaluator).Assembly;
        var exportedTypes = assembly.GetExportedTypes();

        foreach (string typeName in CandidateTypeNames)
        {
            var type = assembly.GetType(typeName, throwOnError: false, ignoreCase: false);

            Assert.NotNull(type);
            Assert.False(type.IsPublic);
            Assert.DoesNotContain(exportedTypes, exported => exported.FullName == typeName);
            Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
            Assert.Empty(type.GetMethods(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.Static |
                BindingFlags.DeclaredOnly));
        }
    }

    [Fact]
    public void RetainedPermissionEvaluator_ExposesOnlyParameterlessPublicConstruction()
    {
        var retainedType = typeof(Bukit.PluginHost.PluginPermissionEvaluator);
        var candidateType = retainedType.Assembly.GetType(
            FileSystemEvaluatorTypeName,
            throwOnError: true,
            ignoreCase: false)!;
        var publicConstructors = retainedType.GetConstructors(
            BindingFlags.Public | BindingFlags.Instance);
        var injectionConstructor = retainedType.GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            types: [candidateType],
            modifiers: null);

        var parameterless = Assert.Single(publicConstructors);
        Assert.Empty(parameterless.GetParameters());
        Assert.NotNull(injectionConstructor);
        Assert.True(injectionConstructor.IsAssembly);
        Assert.DoesNotContain(publicConstructors, constructor =>
            constructor.GetParameters().Any(parameter =>
                parameter.ParameterType == candidateType));
    }

    [Fact]
    public void CurrentBaseline_ContainsFourteenAssemblies462TypesAnd23Candidates()
    {
        using var document = ReadJson(
            "docs",
            "governance",
            "bukit-core-public-api-baseline.v1.json");
        var root = document.RootElement;
        var types = root.GetProperty("types").EnumerateArray().ToArray();

        Assert.Equal(14, root.GetProperty("assemblies").GetArrayLength());
        Assert.Equal(462, types.Length);
        Assert.Equal(23, types.Count(entry =>
            entry.GetProperty("compatibility").GetString() == "2.0-candidate"));
        Assert.All(CandidateTypeNames, candidateTypeName =>
            Assert.DoesNotContain(types, entry =>
                entry.GetProperty("assembly").GetString() == "Bukit.PluginHost" &&
                entry.GetProperty("name").GetString() == candidateTypeName));

        var retained = Assert.Single(types, entry =>
            entry.GetProperty("assembly").GetString() == "Bukit.PluginHost" &&
            entry.GetProperty("name").GetString() ==
            "Bukit.PluginHost.PluginPermissionEvaluator");
        var members = retained.GetProperty("publicMembers")
            .EnumerateArray()
            .Select(member => member.GetString())
            .ToArray();

        Assert.Contains("public .ctor()", members);
        Assert.DoesNotContain(members, member =>
            member?.Contains(FileSystemEvaluatorTypeName, StringComparison.Ordinal) == true);
    }

    [Fact]
    public void ClosedManifest_PreservesBothHistoricalCandidatesAndExactBlob()
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

        foreach (string typeName in CandidateTypeNames)
        {
            var candidate = Assert.Single(candidates, entry =>
                entry.GetProperty("assembly").GetString() == "Bukit.PluginHost" &&
                entry.GetProperty("fullName").GetString() == typeName);

            Assert.Equal(
                "consumer-declaration-pending",
                candidate.GetProperty("declarationStatus").GetString());
            Assert.Equal(
                "unknown-until-voluntary-declaration",
                candidate.GetProperty("privateConsumerStatus").GetString());
            Assert.Equal(
                "no-public-match-found",
                candidate.GetProperty("externalEvidence")
                    .GetProperty("searchStatus")
                    .GetString());
        }

        var prefix = Encoding.UTF8.GetBytes($"blob {bytes.Length}\0");
        var blobBytes = new byte[prefix.Length + bytes.Length];
        prefix.CopyTo(blobBytes, 0);
        bytes.CopyTo(blobBytes, prefix.Length);

        Assert.Equal(
            CandidateManifestBlob,
            Convert.ToHexStringLower(SHA1.HashData(blobBytes)));
    }

    private static string[] CandidateTypeNames =>
    [
        FileSystemEvaluatorTypeName,
        PathNormalizerTypeName
    ];

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

        throw new DirectoryNotFoundException(
            "Could not locate the Bukit repository root.");
    }
}
