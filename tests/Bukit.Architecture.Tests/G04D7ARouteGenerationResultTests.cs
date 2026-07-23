using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Routing;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class G04D7ARouteGenerationResultTests
{
    private const string RemovedTypeName =
        "Bukit.Routing.RouteGenerator+RouteGenerationResult";
    private const string CandidateManifestBlob =
        "7b07d6890562387010b52301e9f8716e9bf10ed1";
    private const string CurrentBaselineStatement =
        "The current public API baseline contains 449 types, including 10 `2.0-candidate` entries.";
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void RoutingAssembly_DoesNotContainOrExportRemovedResult()
    {
        Assembly assembly = typeof(RouteGenerator).Assembly;

        Assert.Null(assembly.GetType(
            RemovedTypeName,
            throwOnError: false,
            ignoreCase: false));
        Assert.DoesNotContain(
            assembly.GetExportedTypes(),
            type => type.FullName == RemovedTypeName);
    }

    [Fact]
    public void GenerateWithSource_ReturnsExactNamedTupleAndKeepsParameters()
    {
        MethodInfo method = Assert.Single(
            typeof(RouteGenerator)
                .GetMethods(
                    BindingFlags.Public |
                    BindingFlags.Static |
                    BindingFlags.DeclaredOnly),
            candidate =>
                candidate.Name ==
                nameof(RouteGenerator.GenerateWithSource));

        Assert.Equal(
            typeof(ValueTuple<
                RouteInfo,
                RouteGenerator.RouteSource>),
            method.ReturnType);

        TupleElementNamesAttribute tupleNames =
            Assert.IsType<TupleElementNamesAttribute>(
                method.ReturnParameter.GetCustomAttribute(
                    typeof(TupleElementNamesAttribute)));
        Assert.Collection(
            tupleNames.TransformNames,
            name => Assert.Equal("Route", name),
            name => Assert.Equal("Source", name));

        ParameterInfo[] parameters = method.GetParameters();
        Assert.Equal(
            [
                typeof(ContentDocument),
                typeof(string),
                typeof(IReadOnlyDictionary<string, string>),
                typeof(IReadOnlyDictionary<
                    string,
                    RouteGenerator.CollectionRouteRule>)
            ],
            parameters
                .Select(parameter => parameter.ParameterType)
                .ToArray());
        Assert.False(parameters[0].HasDefaultValue);
        Assert.Equal("none", parameters[1].DefaultValue);
        Assert.Null(parameters[2].DefaultValue);
        Assert.Null(parameters[3].DefaultValue);
    }

    [Fact]
    public void RouteSource_RemainsPublicWithExactNamesAndOrdinals()
    {
        Type sourceType = typeof(RouteGenerator.RouteSource);

        Assert.True(sourceType.IsNestedPublic);
        Assert.True(sourceType.IsEnum);
        Assert.Equal(
            [
                "FullOverride",
                "PartialOverride",
                "Collection",
                "Permalink"
            ],
            Enum.GetNames(sourceType));
        Assert.Equal(
            [0, 1, 2, 3],
            Enum.GetValues<RouteGenerator.RouteSource>()
                .Select(value => (int)value)
                .ToArray());
    }

    [Fact]
    public void RoutingAssembly_DoesNotAddFriendAssemblies()
    {
        string[] friends = typeof(RouteGenerator).Assembly
            .GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(attribute => attribute.AssemblyName)
            .ToArray();

        Assert.Empty(friends);
    }

    [Fact]
    public void CurrentBaseline_RecordsTupleSignatureAnd449Types10Candidates()
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
        Assert.Equal(449, types.Length);
        Assert.Equal(10, types.Count(entry =>
            entry.GetProperty("compatibility").GetString() ==
            "2.0-candidate"));
        Assert.DoesNotContain(types, entry =>
            entry.GetProperty("assembly").GetString() ==
            "Bukit.Routing" &&
            entry.GetProperty("name").GetString() ==
            RemovedTypeName);

        JsonElement generator = Assert.Single(types, entry =>
            entry.GetProperty("assembly").GetString() ==
            "Bukit.Routing" &&
            entry.GetProperty("name").GetString() ==
            "Bukit.Routing.RouteGenerator");
        string member = Assert.Single(
            generator.GetProperty("publicMembers")
                .EnumerateArray()
                .Select(entry => entry.GetString()!),
            signature =>
                signature.Contains(
                    " GenerateWithSource(",
                    StringComparison.Ordinal));

        Assert.StartsWith(
            "public static System.ValueTuple" +
            "<Bukit.Engine.Abstractions.Routing.RouteInfo!, " +
            "Bukit.Routing.RouteGenerator.RouteSource> " +
            "GenerateWithSource(",
            member,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RouteGenerationResult",
            member,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ActiveGovernance_RecordsCurrentBaselineStatement()
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
            Assert.Contains(
                CurrentBaselineStatement,
                content,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "The current public API baseline contains 486 types, " +
                "including 60 `2.0-candidate` entries.",
                content,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ClosedManifest_PreservesHistoricalCandidateAndExactBlob()
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

        Assert.Equal(
            "closed",
            root.GetProperty("declarationState").GetString());
        Assert.Equal(136, root.GetProperty("candidateCount").GetInt32());
        Assert.Equal(136, candidates.Length);

        JsonElement candidate = Assert.Single(candidates, entry =>
            entry.GetProperty("assembly").GetString() ==
            "Bukit.Routing" &&
            entry.GetProperty("fullName").GetString() ==
            RemovedTypeName);
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
            if (File.Exists(
                    Path.Combine(directory.FullName, "bukit-core.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the Bukit repository root.");
    }
}
