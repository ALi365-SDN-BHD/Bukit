using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class G04D1CM1MigrationContractTests
{
    private const string CandidateManifestBlob = "7b07d6890562387010b52301e9f8716e9bf10ed1";
    private const string GuideRelativePath =
        "docs/analysis/bukit-core-g04d1c-m1-canonical-migration-contract-2026-07-23.zh-CN.md";
    private const string ProvisionalStatus =
        "状态：provisional（Task 3 focused verification、parent aggregate、四项目 Release test 与独立复审待 parent controller 记录）";
    private const string M1Boundary =
        "M1 保留五个 legacy CLR 类型；M1 不授权 M2。";
    private const string M2Boundary =
        "M2 必须另行取得 deliberate public API approval，并把五个 legacy CLR identity 作为原子批次处理。";
    private const string LegacyCallbackTranslationContract =
        "Legacy `TranslateAsync` 会包装 custom callback 直接抛出的 `NotionRenderingException` 和 `NotionApiException`；只有其他 consumer-defined exception 原样传播。Canonical renderer 不执行该翻译，这三类 callback exception 都直接传播。";
    private const string CallbackRenderingExceptionRow =
        "| custom callback 抛出 `NotionRenderingException` | `ContentException`，inner 为原 `NotionRenderingException` | 原 `NotionRenderingException` | legacy unwrap inner；canonical 直接 catch |";
    private const string CallbackApiExceptionRow =
        "| custom callback 抛出 `NotionApiException` | `ContentException`，inner 为原 `NotionApiException` | 原 `NotionApiException` | legacy unwrap inner；canonical 直接 catch |";
    private const string CallbackConsumerExceptionRow =
        "| custom callback 抛出其他 consumer-defined exception | 原异常，不包装 | 原异常，不包装 | 两侧按 consumer 自有类型处理 |";

    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly (string Legacy, string Canonical)[] MigrationTypes =
    [
        (
            "Bukit.Content.Notion.INotionBlockRenderer",
            "Bukit.Notion.Rendering.INotionBlockRenderer"),
        (
            "Bukit.Content.Notion.NotionBlockTransformer",
            "Bukit.Notion.Rendering.NotionBlockTransformer"),
        (
            "Bukit.Content.Notion.NotionBlockRendererRegistry",
            "Bukit.Notion.Rendering.NotionBlockRendererRegistry"),
        (
            "Bukit.Content.Notion.NotionRenderContext",
            "Bukit.Notion.Rendering.NotionRenderContext"),
        (
            "Bukit.Content.Notion.NotionBlocksRenderer",
            "Bukit.Notion.Rendering.NotionBlocksRenderer")
    ];

    [Fact]
    public void M1_KeepsLegacyAndCanonicalExtensionGraphTypesPublicUnderExactIdentities()
    {
        var legacyAssembly = typeof(Bukit.Content.Notion.NotionApiClient).Assembly;
        var canonicalAssembly = typeof(Bukit.Notion.Transport.NotionClient).Assembly;

        Assert.Equal("Bukit.Content", legacyAssembly.GetName().Name);
        Assert.Equal("Bukit.Notion", canonicalAssembly.GetName().Name);

        foreach (var (legacyName, canonicalName) in MigrationTypes)
        {
            AssertPublicTypeResolves(legacyAssembly, legacyName);
            AssertPublicTypeResolves(canonicalAssembly, canonicalName);
        }
    }

    [Fact]
    public void CompileTimeConsumers_UseExactLegacyAndCanonicalExtensionGraphShapes()
    {
        var legacyConsumer = new LegacyCustomRendererConsumer();
        var canonicalConsumer = new CanonicalCustomRendererConsumer();

        Assert.IsAssignableFrom<Bukit.Content.Notion.INotionBlockRenderer>(legacyConsumer);
        Assert.IsAssignableFrom<Bukit.Notion.Rendering.INotionBlockRenderer>(canonicalConsumer);
        Assert.IsType<Bukit.Content.Notion.NotionBlockTransformer>(
            LegacyCustomRendererConsumer.Transformer);
        Assert.IsType<Bukit.Notion.Rendering.NotionBlockTransformer>(
            CanonicalCustomRendererConsumer.Transformer);

        Assert.Equal(
            typeof(Bukit.Content.Notion.NotionApiClient),
            typeof(LegacyCustomRendererConsumer)
                .GetProperty(nameof(LegacyCustomRendererConsumer.ReceivedClient))!
                .PropertyType);
        Assert.Equal(
            typeof(Bukit.Notion.Transport.NotionClient),
            typeof(CanonicalCustomRendererConsumer)
                .GetProperty(nameof(CanonicalCustomRendererConsumer.ReceivedClient))!
                .PropertyType);

        AssertFactorySignature(
            typeof(LegacyCustomRendererConsumer),
            typeof(Bukit.Content.Notion.NotionApiClient),
            typeof(Bukit.Content.Notion.NotionBlocksRenderer));
        AssertFactorySignature(
            typeof(CanonicalCustomRendererConsumer),
            typeof(Bukit.Notion.Transport.NotionClient),
            typeof(Bukit.Notion.Rendering.NotionBlocksRenderer));
    }

    [Fact]
    public void LegacyAndCanonicalExtensionGraphs_KeepExactPublicSignaturesAndAssemblyIdentities()
    {
        AssertExtensionGraphSignatures(
            "Bukit.Content",
            typeof(Bukit.Content.Notion.INotionBlockRenderer),
            typeof(Bukit.Content.Notion.NotionBlockTransformer),
            typeof(Bukit.Content.Notion.NotionBlockRendererRegistry),
            typeof(Bukit.Content.Notion.NotionRenderContext),
            typeof(Bukit.Content.Notion.NotionBlocksRenderer),
            typeof(Bukit.Content.Notion.NotionApiClient));
        AssertExtensionGraphSignatures(
            "Bukit.Notion",
            typeof(Bukit.Notion.Rendering.INotionBlockRenderer),
            typeof(Bukit.Notion.Rendering.NotionBlockTransformer),
            typeof(Bukit.Notion.Rendering.NotionBlockRendererRegistry),
            typeof(Bukit.Notion.Rendering.NotionRenderContext),
            typeof(Bukit.Notion.Rendering.NotionBlocksRenderer),
            typeof(Bukit.Notion.Transport.NotionClient));
    }

    [Fact]
    public void M1_KeepsGovernedBaselineAtFourteenAssembliesFiveHundredFourteenTypesAndOneHundredTenCandidates()
    {
        using var document = ReadJson(
            "docs",
            "governance",
            "bukit-core-public-api-baseline.v1.json");
        var root = document.RootElement;
        var types = root.GetProperty("types").EnumerateArray().ToArray();

        Assert.Equal("bukit-core-public-api-baseline-v1", root.GetProperty("schema").GetString());
        Assert.Equal(14, root.GetProperty("assemblies").GetArrayLength());
        Assert.Equal(514, types.Length);
        Assert.Equal(110, types.Count(type =>
            type.GetProperty("compatibility").GetString() == "2.0-candidate"));

        foreach (var (legacyName, _) in MigrationTypes)
        {
            var entry = Assert.Single(types, type =>
                type.GetProperty("assembly").GetString() == "Bukit.Content" &&
                type.GetProperty("name").GetString() == legacyName);

            Assert.Equal("implementation-public", entry.GetProperty("classification").GetString());
            Assert.Equal("2.0-candidate", entry.GetProperty("compatibility").GetString());
            Assert.Equal("2.0-review", entry.GetProperty("migrationHorizon").GetString());
        }
    }

    [Fact]
    public void M1_KeepsClosedCandidateManifestByteIdenticalAndPreservesLegacyEntries()
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

        foreach (var (legacyName, _) in MigrationTypes)
        {
            var candidate = Assert.Single(candidates, entry =>
                entry.GetProperty("assembly").GetString() == "Bukit.Content" &&
                entry.GetProperty("fullName").GetString() == legacyName);

            Assert.Equal(
                "consumer-declaration-pending",
                candidate.GetProperty("declarationStatus").GetString());
            Assert.Equal(
                "unknown-until-voluntary-declaration",
                candidate.GetProperty("privateConsumerStatus").GetString());
            Assert.Equal(
                "no-public-match-found",
                candidate.GetProperty("externalEvidence").GetProperty("searchStatus").GetString());
        }

        var prefix = Encoding.UTF8.GetBytes($"blob {bytes.Length}\0");
        var blobBytes = new byte[prefix.Length + bytes.Length];
        prefix.CopyTo(blobBytes, 0);
        bytes.CopyTo(blobBytes, prefix.Length);
        var actualBlob = Convert.ToHexStringLower(SHA1.HashData(blobBytes));

        Assert.Equal(CandidateManifestBlob, actualBlob);
    }

    [Fact]
    public void CanonicalNotionProject_RemainsFreeOfProjectAndPackageDependencies()
    {
        var projectPath = Path.Combine(
            RepoRoot,
            "src",
            "Bukit-Core",
            "Bukit.Notion",
            "Bukit.Notion.csproj");
        var project = XDocument.Load(projectPath);

        Assert.Empty(project.Descendants("ProjectReference"));
        Assert.Empty(project.Descendants("PackageReference"));
    }

    [Fact]
    public void MigrationGuide_RecordsCompleteSourceContractAndM1M2Boundary()
    {
        var guidePath = Path.Combine(RepoRoot, GuideRelativePath);

        Assert.True(File.Exists(guidePath), $"Missing M1 migration guide: {guidePath}");

        var guide = File.ReadAllText(guidePath);

        Assert.Contains(ProvisionalStatus, guide, StringComparison.Ordinal);
        Assert.Contains(M1Boundary, guide, StringComparison.Ordinal);
        Assert.Contains(M2Boundary, guide, StringComparison.Ordinal);
        Assert.Contains("14 个程序集、514 个类型、110 个 `2.0-candidate`", guide, StringComparison.Ordinal);
        Assert.Contains("136-entry candidate manifest", guide, StringComparison.Ordinal);
        Assert.Contains(CandidateManifestBlob, guide, StringComparison.Ordinal);
        Assert.Contains("`Bukit.Notion.csproj` 保持 0 `ProjectReference` / 0 `PackageReference`", guide, StringComparison.Ordinal);

        foreach (var (legacyName, canonicalName) in MigrationTypes)
        {
            Assert.Contains($"`{legacyName}`", guide, StringComparison.Ordinal);
            Assert.Contains($"`{canonicalName}`", guide, StringComparison.Ordinal);
        }

        string[] requiredSourceContracts =
        [
            "public sealed class LegacyCustomRenderer : INotionBlockRenderer",
            "public sealed class CanonicalCustomRenderer : INotionBlockRenderer",
            "NotionBlockTransformer transformer =",
            "NotionBlockRendererRegistry.CreateDefault()",
            "NotionRenderContext context",
            "new NotionBlocksRenderer(client, registry)",
            "ApiVersion = NotionApiUrls.NotionVersion",
            "Timeout = TimeSpan.FromSeconds(30)",
            "RequestDelayMs = legacyOptions.RequestDelayMs",
            "MaxRetries = legacyOptions.MaxRetries",
            "MaxRps = legacyOptions.MaxRps",
            "NotionRequestSemantics.IdempotentRead",
            "NotionRequestSemantics.NonReplayableWrite",
            "ContentException",
            "NotionRenderingException",
            "NotionApiException",
            "OperationCanceledException",
            "RenderChildrenAsync",
            "renderer 不拥有 client",
            "injected `HttpClient` 仍由 caller 拥有",
            "internally-created `HttpClient` 由 `NotionClient` 拥有",
            "source break",
            "binary break",
            "type forwarding",
            "unknown-until-voluntary-declaration",
            "新证据回退规则"
        ];

        Assert.All(requiredSourceContracts, contract =>
            Assert.Contains(contract, guide, StringComparison.Ordinal));

        Assert.Contains(LegacyCallbackTranslationContract, guide, StringComparison.Ordinal);
        Assert.Contains(CallbackRenderingExceptionRow, guide, StringComparison.Ordinal);
        Assert.Contains(CallbackApiExceptionRow, guide, StringComparison.Ordinal);
        Assert.Contains(CallbackConsumerExceptionRow, guide, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(guide, "catch (ConsumerCallbackException)"));
        Assert.DoesNotContain(
            "| custom renderer/transformer exception | 原异常，不包装 | 原异常，不包装 |",
            guide,
            StringComparison.Ordinal);

        Assert.DoesNotContain("状态：已完成", guide, StringComparison.Ordinal);
        Assert.DoesNotContain("parent aggregate：PASS", guide, StringComparison.Ordinal);
        Assert.DoesNotContain("独立复审：PASS", guide, StringComparison.Ordinal);
    }

    private static void AssertPublicTypeResolves(
        System.Reflection.Assembly assembly,
        string fullName)
    {
        var type = assembly.GetType(fullName, throwOnError: false, ignoreCase: false);

        Assert.NotNull(type);
        Assert.True(type.IsPublic, $"Expected public CLR identity: {fullName}");
        Assert.Same(
            type,
            Type.GetType(
                $"{fullName}, {assembly.GetName().Name}",
                throwOnError: false,
                ignoreCase: false));
    }

    private static void AssertExtensionGraphSignatures(
        string expectedAssemblyName,
        Type rendererInterface,
        Type transformerDelegate,
        Type registry,
        Type context,
        Type blocksRenderer,
        Type client)
    {
        Type[] graphTypes =
        [
            rendererInterface,
            transformerDelegate,
            registry,
            context,
            blocksRenderer
        ];
        Assert.All(graphTypes, type =>
        {
            Assert.True(type.IsPublic, $"Expected public type: {type.FullName}");
            Assert.Equal(expectedAssemblyName, type.Assembly.GetName().Name);
        });
        Assert.Equal(expectedAssemblyName, client.Assembly.GetName().Name);

        Type[] callbackParameters =
        [
            typeof(JsonElement),
            context,
            typeof(CancellationToken)
        ];
        AssertPublicMethod(
            rendererInterface,
            "RenderAsync",
            typeof(Task<string>),
            callbackParameters);
        AssertPublicMethod(
            transformerDelegate,
            "Invoke",
            typeof(Task<string>),
            callbackParameters);

        var clientProperty = context.GetProperty("Client");
        Assert.NotNull(clientProperty);
        Assert.Equal(client, clientProperty.PropertyType);
        Assert.True(clientProperty.GetMethod?.IsPublic);
        AssertPublicMethod(
            context,
            "RenderChildrenAsync",
            typeof(Task<string>),
            typeof(string),
            typeof(CancellationToken));

        AssertPublicMethod(
            registry,
            "Register",
            registry,
            typeof(string),
            rendererInterface);
        AssertPublicMethod(
            registry,
            "SetCustomTransformer",
            registry,
            typeof(string),
            transformerDelegate);
        AssertPublicMethod(
            registry,
            "RemoveCustomTransformer",
            registry,
            typeof(string));

        var constructor = blocksRenderer.GetConstructor([client, registry]);
        Assert.NotNull(constructor);
        Assert.True(constructor.IsPublic);
        var registryProperty = blocksRenderer.GetProperty("Registry");
        Assert.NotNull(registryProperty);
        Assert.Equal(registry, registryProperty.PropertyType);
        Assert.True(registryProperty.GetMethod?.IsPublic);
        AssertPublicMethod(
            blocksRenderer,
            "RenderPageAsync",
            typeof(Task<string>),
            typeof(string),
            typeof(CancellationToken));
    }

    private static void AssertPublicMethod(
        Type declaringType,
        string methodName,
        Type returnType,
        params Type[] parameterTypes)
    {
        var method = declaringType.GetMethod(methodName, parameterTypes);

        Assert.NotNull(method);
        Assert.True(method.IsPublic);
        Assert.Equal(returnType, method.ReturnType);
        Assert.Equal(
            parameterTypes,
            method.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
    }

    private static void AssertFactorySignature(
        Type consumer,
        Type client,
        Type renderer)
    {
        var factory = consumer.GetMethod("CreateRenderer");

        Assert.NotNull(factory);
        Assert.True(factory.IsPublic);
        Assert.True(factory.IsStatic);
        Assert.Equal(renderer, factory.ReturnType);
        Assert.Equal(
            [client],
            factory.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
    }

    private static int CountOccurrences(string value, string fragment)
        => value.Split(fragment, StringSplitOptions.None).Length - 1;

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

        throw new InvalidOperationException("Could not locate repository root.");
    }

    private sealed class LegacyCustomRendererConsumer : Bukit.Content.Notion.INotionBlockRenderer
    {
        public Bukit.Content.Notion.NotionApiClient? ReceivedClient { get; private set; }

        public static Bukit.Content.Notion.NotionBlockTransformer Transformer { get; } =
            async (_, context, cancellationToken) =>
                await context.RenderChildrenAsync("legacy-child", cancellationToken);

        public async Task<string?> RenderAsync(
            JsonElement block,
            Bukit.Content.Notion.NotionRenderContext context,
            CancellationToken cancellationToken)
        {
            ReceivedClient = context.Client;
            return await context.RenderChildrenAsync("legacy-child", cancellationToken);
        }

        public static Bukit.Content.Notion.NotionBlocksRenderer CreateRenderer(
            Bukit.Content.Notion.NotionApiClient client)
        {
            var registry = Bukit.Content.Notion.NotionBlockRendererRegistry.CreateDefault()
                .Register("custom", new LegacyCustomRendererConsumer())
                .SetCustomTransformer("custom", Transformer);
            return new Bukit.Content.Notion.NotionBlocksRenderer(client, registry);
        }
    }

    private sealed class CanonicalCustomRendererConsumer : Bukit.Notion.Rendering.INotionBlockRenderer
    {
        public Bukit.Notion.Transport.NotionClient? ReceivedClient { get; private set; }

        public static Bukit.Notion.Rendering.NotionBlockTransformer Transformer { get; } =
            async (_, context, cancellationToken) =>
                await context.RenderChildrenAsync("canonical-child", cancellationToken);

        public async Task<string?> RenderAsync(
            JsonElement block,
            Bukit.Notion.Rendering.NotionRenderContext context,
            CancellationToken cancellationToken)
        {
            ReceivedClient = context.Client;
            return await context.RenderChildrenAsync("canonical-child", cancellationToken);
        }

        public static Bukit.Notion.Rendering.NotionBlocksRenderer CreateRenderer(
            Bukit.Notion.Transport.NotionClient client)
        {
            var registry = Bukit.Notion.Rendering.NotionBlockRendererRegistry.CreateDefault()
                .Register("custom", new CanonicalCustomRendererConsumer())
                .SetCustomTransformer("custom", Transformer);
            return new Bukit.Notion.Rendering.NotionBlocksRenderer(client, registry);
        }
    }
}
