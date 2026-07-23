using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class G04D2ERuntimeContextTests
{
    private const string CandidateManifestBlob =
        "7b07d6890562387010b52301e9f8716e9bf10ed1";
    private const string RuntimeContextTypeName =
        "Bukit.PluginHost.PluginRuntimeOnlyContext";
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void RuntimeContext_ExistsButIsInternalAndNotExported()
    {
        var assembly = typeof(Bukit.PluginHost.PluginConfigLoader).Assembly;
        var type = assembly.GetType(
            RuntimeContextTypeName,
            throwOnError: true,
            ignoreCase: false)!;

        Assert.False(type.IsPublic);
        Assert.DoesNotContain(
            assembly.GetExportedTypes(),
            exported => exported.FullName == RuntimeContextTypeName);
    }

    [Fact]
    public void RetainedConfigLoader_HasOnlyParameterlessPublicConstruction()
    {
        var retainedType = typeof(Bukit.PluginHost.PluginConfigLoader);
        var runtimeContextType = retainedType.Assembly.GetType(
            RuntimeContextTypeName,
            throwOnError: true,
            ignoreCase: false)!;
        var publicConstructors = retainedType.GetConstructors(
            BindingFlags.Public | BindingFlags.Instance);
        var runtimeContextConstructor = retainedType.GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            types: [runtimeContextType],
            modifiers: null);

        var parameterless = Assert.Single(publicConstructors);
        Assert.Empty(parameterless.GetParameters());
        Assert.NotNull(runtimeContextConstructor);
        Assert.True(runtimeContextConstructor.IsAssembly);
        Assert.DoesNotContain(
            publicConstructors,
            constructor => constructor.GetParameters().Any(parameter =>
                parameter.ParameterType == runtimeContextType));
    }

    [Fact]
    public void PluginHost_GrantsOnlyTheApprovedTestFriendAccess()
    {
        var assembly = typeof(Bukit.PluginHost.PluginConfigLoader).Assembly;
        var friends = assembly
            .GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(attribute => attribute.AssemblyName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            ["Bukit.Cli.Tests", "Bukit.PluginHost.Tests"],
            friends);
        Assert.DoesNotContain(friends, friend =>
            !friend.EndsWith(".Tests", StringComparison.Ordinal));
    }

    [Fact]
    public void CurrentBaseline_ContainsFourteenAssemblies443TypesAnd0Candidates()
    {
        using var document = ReadJson(
            "docs",
            "governance",
            "bukit-core-public-api-baseline.v1.json");
        var root = document.RootElement;
        var types = root.GetProperty("types").EnumerateArray().ToArray();

        Assert.Equal(14, root.GetProperty("assemblies").GetArrayLength());
        Assert.Equal(443, types.Length);
        Assert.Equal(0, types.Count(entry =>
            entry.GetProperty("compatibility").GetString() == "2.0-candidate"));
        Assert.DoesNotContain(types, entry =>
            entry.GetProperty("assembly").GetString() == "Bukit.PluginHost" &&
            entry.GetProperty("name").GetString() == RuntimeContextTypeName);

        var retained = Assert.Single(types, entry =>
            entry.GetProperty("assembly").GetString() == "Bukit.PluginHost" &&
            entry.GetProperty("name").GetString() ==
            "Bukit.PluginHost.PluginConfigLoader");
        var members = retained.GetProperty("publicMembers")
            .EnumerateArray()
            .Select(member => member.GetString())
            .ToArray();

        Assert.Contains("public .ctor()", members);
        Assert.DoesNotContain(members, member =>
            member?.Contains(RuntimeContextTypeName, StringComparison.Ordinal) == true);
    }

    [Fact]
    public void ClosedManifest_PreservesHistoricalCandidateAndExactBlob()
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

        var candidate = Assert.Single(candidates, entry =>
            entry.GetProperty("assembly").GetString() == "Bukit.PluginHost" &&
            entry.GetProperty("fullName").GetString() == RuntimeContextTypeName);
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

        var prefix = Encoding.UTF8.GetBytes($"blob {bytes.Length}\0");
        var blobBytes = new byte[prefix.Length + bytes.Length];
        prefix.CopyTo(blobBytes, 0);
        bytes.CopyTo(blobBytes, prefix.Length);

        Assert.Equal(
            CandidateManifestBlob,
            Convert.ToHexStringLower(SHA1.HashData(blobBytes)));
    }

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
