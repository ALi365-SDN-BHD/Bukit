using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bukit.Config;
using Bukit.Engine;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Stages;
using Bukit.Rendering;
using Bukit.Shared;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class G04D9BContentStageContractGraphTests
{
    private const string CollectionValidatorTypeName =
        "Bukit.Engine.ContentCollectionContractValidator";
    private const string SchemaValidatorTypeName =
        "Bukit.Engine.ContentSchemaValidator";
    private const string CandidateManifestBlob =
        "7b07d6890562387010b52301e9f8716e9bf10ed1";
    private const string CurrentBaselineStatement =
        "The current public API baseline contains 425 types, including 0 `2.0-candidate` entries.";
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void ApprovedValidators_ExistInternalStaticAndNotExported()
    {
        Assembly assembly = typeof(ContentPipeline).Assembly;
        Type[] exported = assembly.GetExportedTypes();

        foreach (string typeName in new[]
                 {
                     CollectionValidatorTypeName,
                     SchemaValidatorTypeName
                 })
        {
            Type type = GetType(assembly, typeName);

            Assert.True(type.IsNotPublic);
            Assert.True(type.IsAbstract);
            Assert.True(type.IsSealed);
            Assert.DoesNotContain(type, exported);
        }
    }

    [Fact]
    public void InternalCollectionValidator_KeepsBothValidateOverloads()
    {
        Type validator = GetType(
            typeof(ContentPipeline).Assembly,
            CollectionValidatorTypeName);
        MethodInfo[] methods = validator.GetMethods(
                BindingFlags.Public |
                BindingFlags.Static |
                BindingFlags.DeclaredOnly)
            .Where(method => method.Name == "Validate")
            .OrderBy(
                method => method.GetParameters()[0].ParameterType.FullName,
                StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(2, methods.Length);
        Assert.All(methods, method => Assert.Equal(typeof(void), method.ReturnType));
        Assert.Contains(methods, method =>
            Assert.Single(method.GetParameters()).ParameterType ==
            typeof(RawContentDocument));
        Assert.Contains(methods, method =>
            Assert.Single(method.GetParameters()).ParameterType ==
            typeof(IReadOnlyList<RawContentDocument>));
    }

    [Fact]
    public void InternalSchemaValidator_KeepsInternalValidationEntryPoints()
    {
        Type validator = GetType(
            typeof(ContentPipeline).Assembly,
            SchemaValidatorTypeName);

        Assert.Empty(validator.GetMethods(
            BindingFlags.Public |
            BindingFlags.Static |
            BindingFlags.DeclaredOnly));

        string[] internalMethods = validator.GetMethods(
                BindingFlags.NonPublic |
                BindingFlags.Static |
                BindingFlags.DeclaredOnly)
            .Where(method =>
                method.IsAssembly &&
                method.Name is
                    "ValidateFields" or
                    "Validate" or
                    "ResolveSchemaFailMode")
            .Select(method => method.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            ["ResolveSchemaFailMode", "Validate", "ValidateFields"],
            internalMethods);
    }

    [Fact]
    public void RetainedStageAndProviderGraph_RemainsPublicAndPropagated()
    {
        Assembly assembly = typeof(ContentPipeline).Assembly;
        Type[] exported = assembly.GetExportedTypes();

        Type[] retained =
        [
            typeof(ContentValidationIssue),
            typeof(IContentProviderFactory),
            typeof(ITemplateRenderer),
            typeof(ContentStageInput),
            typeof(ContentStageOutput),
            typeof(IContentStage),
            typeof(TemplateRendererBase)
        ];
        Assert.All(retained, type => Assert.Contains(type, exported));

        ConstructorInfo[] constructors = typeof(ContentPipeline)
            .GetConstructors(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly)
            .OrderBy(
                constructor =>
                    constructor.GetParameters()[0].ParameterType.FullName,
                StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(2, constructors.Length);
        Assert.Contains(constructors, constructor =>
            constructor.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .SequenceEqual(
                [
                    typeof(IContentProviderFactory),
                    typeof(ILogger)
                ]));
        Assert.Contains(constructors, constructor =>
            constructor.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .SequenceEqual(
                [
                    typeof(IReadOnlyList<IContentStage>),
                    typeof(ILogger)
                ]));

        MethodInfo execute = Assert.Single(
            typeof(ContentPipeline).GetMethods(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly),
            method => method.Name == nameof(ContentPipeline.ExecuteAsync));
        Assert.Equal(typeof(Task<ContentPipelineResult>), execute.ReturnType);

        Assert.Contains(
            typeof(IContentProviderFactory),
            typeof(DefaultContentProviderFactory).GetInterfaces());
    }

    [Fact]
    public void RetainedIssueAndStageShapes_RemainExact()
    {
        ConstructorInfo issueConstructor = Assert.Single(
            typeof(ContentValidationIssue).GetConstructors(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly));
        Assert.Equal(
            [typeof(string), typeof(string), typeof(string), typeof(string)],
            issueConstructor.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray());

        MethodInfo validateDocuments = Assert.Single(
            typeof(ContentModelSchemaProjection).GetMethods(
                BindingFlags.Public |
                BindingFlags.Static |
                BindingFlags.DeclaredOnly),
            method =>
                method.Name ==
                nameof(ContentModelSchemaProjection.ValidateDocuments));
        Assert.Equal(
            typeof(IReadOnlyList<ContentValidationIssue>),
            validateDocuments.ReturnType);

        MethodInfo stageExecute = Assert.Single(
            typeof(IContentStage).GetMethods(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly),
            method => method.Name == nameof(IContentStage.ExecuteAsync));
        Assert.Equal(typeof(Task<ContentStageOutput>), stageExecute.ReturnType);
        Assert.Equal(
            [typeof(ContentStageInput), typeof(CancellationToken)],
            stageExecute.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray());

        AssertRecordConstructor(
            typeof(ContentStageInput),
            [
                typeof(IReadOnlyList<ContentDocument>),
                typeof(IContentBodyStore),
                typeof(AppConfig),
                typeof(ConfigOverrides),
                typeof(string),
                typeof(string),
                typeof(ILogger)
            ]);
        AssertRecordConstructor(
            typeof(ContentStageOutput),
            [
                typeof(IReadOnlyList<ContentDocument>),
                typeof(IContentBodyStore),
                typeof(string),
                typeof(long),
                typeof(IReadOnlyList<ContentValidationIssue>)
            ]);
    }

    [Fact]
    public void RendererExtensionSeam_RemainsPublicAbstractWithProtectedHooks()
    {
        Type renderer = typeof(TemplateRendererBase);

        Assert.True(renderer.IsPublic);
        Assert.True(renderer.IsAbstract);
        Assert.Contains(typeof(ITemplateRenderer), renderer.GetInterfaces());

        ConstructorInfo constructor = Assert.Single(
            renderer.GetConstructors(
                BindingFlags.NonPublic |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly));
        Assert.True(constructor.IsFamily);
        Assert.Equal(
            [
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(IReadOnlyDictionary<string, string>)
            ],
            constructor.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray());

        string[] protectedMethods = renderer.GetMethods(
                BindingFlags.NonPublic |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly)
            .Where(method => method.IsFamily && !method.IsSpecialName)
            .Select(method => method.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            [
                "ExtractLayoutDirective",
                "ParseTemplateText",
                "RenderTemplateCore",
                "RenderWithLayout",
                "ResolveTemplatePath",
                "SetContent"
            ],
            protectedMethods);

        Assert.Equal(
            ["RenderList", "RenderPage"],
            typeof(ITemplateRenderer).GetMethods()
                .Select(method => method.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray());
    }

    [Fact]
    public void CurrentBaseline_RemovesTwoValidatorsAndRetainsSevenContracts()
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
        Assert.Equal(425, types.Length);
        Assert.Equal(0, types.Count(entry =>
            entry.GetProperty("compatibility").GetString() ==
            "2.0-candidate"));

        Assert.DoesNotContain(types, entry =>
            entry.GetProperty("name").GetString() ==
            CollectionValidatorTypeName);
        Assert.DoesNotContain(types, entry =>
            entry.GetProperty("name").GetString() ==
            SchemaValidatorTypeName);

        string[] retainedNames =
        [
            "Bukit.Engine.ContentValidationIssue",
            "Bukit.Engine.IContentProviderFactory",
            "Bukit.Engine.ITemplateRenderer",
            "Bukit.Engine.Stages.ContentStageInput",
            "Bukit.Engine.Stages.ContentStageOutput",
            "Bukit.Engine.Stages.IContentStage",
            "Bukit.Engine.TemplateRendererBase"
        ];
        foreach (string typeName in retainedNames)
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

        string[] expected =
        [
            CollectionValidatorTypeName,
            SchemaValidatorTypeName,
            "Bukit.Engine.ContentValidationIssue",
            "Bukit.Engine.IContentProviderFactory",
            "Bukit.Engine.ITemplateRenderer",
            "Bukit.Engine.Stages.ContentStageInput",
            "Bukit.Engine.Stages.ContentStageOutput",
            "Bukit.Engine.Stages.IContentStage",
            "Bukit.Engine.TemplateRendererBase"
        ];
        JsonElement[] historical = candidates
            .Where(entry => expected.Contains(
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
    public void ActiveGovernance_RecordsCurrentBaselineAndD9BDecision()
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
            Assert.Contains("G-04D9B", content, StringComparison.Ordinal);
            Assert.Contains(
                "ContentSchemaValidator",
                content,
                StringComparison.Ordinal);
            Assert.Contains(
                "TemplateRendererBase",
                content,
                StringComparison.Ordinal);
        }
    }

    private static void AssertRecordConstructor(
        Type type,
        Type[] expectedParameters)
    {
        ConstructorInfo constructor = Assert.Single(
            type.GetConstructors(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly));
        Assert.Equal(
            expectedParameters,
            constructor.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray());
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
