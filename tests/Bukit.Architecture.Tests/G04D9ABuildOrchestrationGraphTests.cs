using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class G04D9ABuildOrchestrationGraphTests
{
    private const string BuildPipelineTypeName =
        "Bukit.Engine.BuildPipeline";
    private const string BuildPipelineContextTypeName =
        "Bukit.Engine.BuildPipelineContext";
    private const string RoutePipelineTypeName =
        "Bukit.Engine.RoutePipeline";
    private const string RoutePipelineResultTypeName =
        "Bukit.Engine.RoutePipelineResult";
    private const string CandidateManifestBlob =
        "7b07d6890562387010b52301e9f8716e9bf10ed1";
    private const string CurrentBaselineStatement =
        "The current public API baseline contains 469 types, including 31 `2.0-candidate` entries.";
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void ApprovedBuildOrchestrationTypes_ExistInternalSealedAndNotExported()
    {
        Assembly assembly = typeof(SiteEngine).Assembly;
        Type[] exported = assembly.GetExportedTypes();

        foreach (string typeName in new[]
                 {
                     BuildPipelineTypeName,
                     BuildPipelineContextTypeName,
                     RoutePipelineTypeName,
                     RoutePipelineResultTypeName
                 })
        {
            Type type = GetType(assembly, typeName);

            Assert.True(type.IsNotPublic);
            Assert.True(type.IsSealed);
            Assert.DoesNotContain(type, exported);
        }
    }

    [Fact]
    public void InternalBuildPipelineGraph_KeepsConstructorExecutionAndContextShape()
    {
        Assembly assembly = typeof(SiteEngine).Assembly;
        Type pipeline = GetType(assembly, BuildPipelineTypeName);
        Type context = GetType(assembly, BuildPipelineContextTypeName);
        Type executor = typeof(Func<,,>).MakeGenericType(
            context,
            typeof(CancellationToken),
            typeof(Task<BuildResult>));

        ConstructorInfo pipelineConstructor = Assert.Single(
            pipeline.GetConstructors(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly));
        Assert.Equal(
            [executor],
            pipelineConstructor.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray());

        MethodInfo execute = Assert.Single(
            pipeline.GetMethods(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly),
            method => method.Name == "ExecuteAsync");
        Assert.Equal(typeof(Task<BuildResult>), execute.ReturnType);
        Assert.Equal(
            [context, typeof(CancellationToken)],
            execute.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray());
        Assert.True(execute.GetParameters()[1].HasDefaultValue);
        Assert.Null(execute.GetParameters()[1].DefaultValue);

        ConstructorInfo contextConstructor = Assert.Single(
            context.GetConstructors(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly));
        Assert.Equal(
            [typeof(AppConfig), typeof(string), typeof(ConfigOverrides)],
            contextConstructor.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray());

        PropertyInfo[] properties = context.GetProperties(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly)
            .Where(property =>
                property.Name is "Config" or "RootDir" or "Overrides")
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            ["Config", "Overrides", "RootDir"],
            properties.Select(property => property.Name).ToArray());
        Assert.Equal(
            [typeof(AppConfig), typeof(ConfigOverrides), typeof(string)],
            properties.Select(property => property.PropertyType).ToArray());
    }

    [Fact]
    public void InternalRoutePipelineGraph_KeepsExecuteAndResultShape()
    {
        Assembly assembly = typeof(SiteEngine).Assembly;
        Type pipeline = GetType(assembly, RoutePipelineTypeName);
        Type result = GetType(assembly, RoutePipelineResultTypeName);

        ConstructorInfo pipelineConstructor = Assert.Single(
            pipeline.GetConstructors(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly));
        Assert.Empty(pipelineConstructor.GetParameters());

        MethodInfo execute = Assert.Single(
            pipeline.GetMethods(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly),
            method => method.Name == "Execute");
        Assert.Equal(result, execute.ReturnType);
        ParameterInfo[] executeParameters = execute.GetParameters();
        Assert.Equal(
            [
                typeof(AppConfig),
                typeof(IReadOnlyList<ContentDocument>),
                typeof(ThemeTemplateResolver)
            ],
            executeParameters
                .Select(parameter => parameter.ParameterType)
                .ToArray());
        Assert.True(executeParameters[2].IsOptional);
        Assert.Null(executeParameters[2].DefaultValue);

        ConstructorInfo resultConstructor = Assert.Single(
            result.GetConstructors(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly));
        Assert.Equal(
            [
                typeof(IReadOnlyList<ContentDocument>),
                typeof(IReadOnlyList<RoutedContentDocument>),
                typeof(IReadOnlyList<RouteInfo>)
            ],
            resultConstructor.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray());

        PropertyInfo[] properties = result.GetProperties(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly)
            .Where(property =>
                property.Name is
                    "ContentDocuments" or
                    "RoutedDocuments" or
                    "ListRoutes")
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            ["ContentDocuments", "ListRoutes", "RoutedDocuments"],
            properties.Select(property => property.Name).ToArray());
        Assert.Equal(
            [
                typeof(IReadOnlyList<ContentDocument>),
                typeof(IReadOnlyList<RouteInfo>),
                typeof(IReadOnlyList<RoutedContentDocument>)
            ],
            properties.Select(property => property.PropertyType).ToArray());

        PropertyInfo listRouteGraph = Assert.Single(
            result.GetProperties(
                BindingFlags.NonPublic |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly),
            property => property.Name == "ListRouteGraph");
        Assert.False(listRouteGraph.GetMethod!.IsPublic);
    }

    [Fact]
    public void StableBuildParentsAndCompanions_RemainPublicWithExactPropagation()
    {
        Assembly assembly = typeof(SiteEngine).Assembly;
        Type[] exported = assembly.GetExportedTypes();

        Assert.Contains(typeof(BuildOptions), exported);
        Assert.Contains(typeof(BuildVariantSummary), exported);
        Assert.Contains(typeof(ContentPipelineResult), exported);

        MethodInfo providerBuild = Assert.Single(
            typeof(SiteEngine).GetMethods(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly),
            method =>
                method.Name == nameof(SiteEngine.BuildAsync) &&
                method.GetParameters().FirstOrDefault()?.ParameterType ==
                typeof(IContentProvider));
        Assert.Equal(typeof(Task), providerBuild.ReturnType);
        Assert.Equal(
            [
                typeof(IContentProvider),
                typeof(BuildOptions),
                typeof(CancellationToken)
            ],
            providerBuild.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray());

        PropertyInfo variants = typeof(BuildResult).GetProperty(
            nameof(BuildResult.Variants),
            BindingFlags.Public |
            BindingFlags.Instance |
            BindingFlags.DeclaredOnly)!;
        Assert.Equal(
            typeof(IReadOnlyList<BuildVariantSummary>),
            variants.PropertyType);

        MethodInfo contentExecute = Assert.Single(
            typeof(ContentPipeline).GetMethods(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly),
            method => method.Name == nameof(ContentPipeline.ExecuteAsync));
        Assert.Equal(
            typeof(Task<ContentPipelineResult>),
            contentExecute.ReturnType);
    }

    [Fact]
    public void EngineFriendBoundary_RemainsUnchanged()
    {
        string[] friends = typeof(SiteEngine).Assembly
            .GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(attribute => attribute.AssemblyName.Split(',')[0])
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["Bukit.Engine.Tests", "bukit"], friends);
    }

    [Fact]
    public void CurrentBaseline_RemovesFourTypesAndRetainsThreeCompanions()
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
        Assert.Equal(469, types.Length);
        Assert.Equal(31, types.Count(entry =>
            entry.GetProperty("compatibility").GetString() ==
            "2.0-candidate"));

        foreach (string typeName in new[]
                 {
                     BuildPipelineTypeName,
                     BuildPipelineContextTypeName,
                     RoutePipelineTypeName,
                     RoutePipelineResultTypeName
                 })
        {
            Assert.DoesNotContain(types, entry =>
                entry.GetProperty("name").GetString() == typeName);
        }

        foreach (string typeName in new[]
                 {
                     "Bukit.Engine.BuildOptions",
                     "Bukit.Engine.BuildVariantSummary",
                     "Bukit.Engine.ContentPipelineResult"
                 })
        {
            JsonElement retained = Assert.Single(types, entry =>
                entry.GetProperty("name").GetString() == typeName);
            Assert.Equal(
                "cross-assembly-implementation",
                retained.GetProperty("classification").GetString());
            Assert.Equal(
                "1.x-do-not-narrow",
                retained.GetProperty("compatibility").GetString());
            Assert.Equal(
                "2.0-review",
                retained.GetProperty("migrationHorizon").GetString());
        }
    }

    [Fact]
    public void ClosedManifest_PreservesSevenHistoricalCandidatesAndExactBlob()
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

        string[] expected =
        [
            "Bukit.Engine.BuildOptions",
            BuildPipelineTypeName,
            BuildPipelineContextTypeName,
            "Bukit.Engine.BuildVariantSummary",
            "Bukit.Engine.ContentPipelineResult",
            RoutePipelineTypeName,
            RoutePipelineResultTypeName
        ];
        JsonElement[] historical = candidates
            .Where(entry => expected.Contains(
                entry.GetProperty("fullName").GetString()!,
                StringComparer.Ordinal))
            .ToArray();
        Assert.Equal(7, historical.Length);
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
    public void ActiveGovernance_RecordsCurrentBaselineAndD9ADecision()
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
            Assert.Contains("G-04D9A", content, StringComparison.Ordinal);
            Assert.Contains(
                "BuildPipeline",
                content,
                StringComparison.Ordinal);
            Assert.Contains(
                "BuildOptions",
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
