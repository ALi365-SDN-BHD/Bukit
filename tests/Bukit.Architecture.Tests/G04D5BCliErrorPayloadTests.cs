using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Bukit.Cli.Shared.Cli.Parsing;
using Bukit.Cli.Shared.Cli.Rendering;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class G04D5BCliErrorPayloadTests
{
    private const string RendererTypeName =
        "Bukit.Cli.Shared.Cli.Rendering.CliErrorRenderer";
    private const string DiagnosticTypeName =
        "Bukit.Cli.Shared.Cli.Rendering.CliErrorRenderer+CliErrorDiagnostic";
    private const string PayloadTypeName =
        "Bukit.Cli.Shared.Cli.Rendering.CliErrorRenderer+CliErrorPayload";
    private const string JsonContextTypeName =
        "Bukit.Cli.Shared.Cli.Rendering.CliErrorJsonContext";
    private const string CandidateManifestBlob =
        "7b07d6890562387010b52301e9f8716e9bf10ed1";
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void Payload_ExistsAsExactInternalNestedTypeAndIsNotExported()
    {
        Type renderer = typeof(CliErrorRenderer);
        Assembly assembly = renderer.Assembly;
        Type payload = assembly.GetType(
            PayloadTypeName,
            throwOnError: true,
            ignoreCase: false)!;

        Assert.Equal(PayloadTypeName, payload.FullName);
        Assert.Equal(renderer, payload.DeclaringType);
        Assert.True(payload.IsNestedAssembly);
        Assert.False(payload.IsNestedPublic);
        Assert.DoesNotContain(payload, assembly.GetExportedTypes());
    }

    [Fact]
    public void RendererAndDiagnosticRemainPublicAndExported()
    {
        Type renderer = typeof(CliErrorRenderer);
        Type diagnostic = typeof(CliErrorRenderer.CliErrorDiagnostic);
        Type[] exportedTypes = renderer.Assembly.GetExportedTypes();

        Assert.Equal(RendererTypeName, renderer.FullName);
        Assert.True(renderer.IsPublic);
        Assert.True(renderer.IsAbstract);
        Assert.True(renderer.IsSealed);
        Assert.Contains(renderer, exportedTypes);

        Assert.Equal(DiagnosticTypeName, diagnostic.FullName);
        Assert.Equal(renderer, diagnostic.DeclaringType);
        Assert.True(diagnostic.IsNestedPublic);
        Assert.Contains(diagnostic, exportedTypes);
    }

    [Fact]
    public void Renderer_PreservesAllFivePublicRenderJsonOverloads()
    {
        Type renderer = typeof(CliErrorRenderer);
        MethodInfo[] overloads = renderer.GetMethods(
                BindingFlags.Public |
                BindingFlags.Static |
                BindingFlags.DeclaredOnly)
            .Where(method => method.Name == "RenderJson")
            .ToArray();

        Assert.Equal(5, overloads.Length);
        AssertRenderJsonOverload(
            overloads,
            typeof(string),
            typeof(IReadOnlyList<CliDiagnostic>),
            typeof(string));
        AssertRenderJsonOverload(
            overloads,
            typeof(string),
            typeof(int),
            typeof(IReadOnlyList<CliDiagnostic>),
            typeof(string));
        AssertRenderJsonOverload(
            overloads,
            typeof(string),
            typeof(int),
            typeof(IReadOnlyList<CliErrorRenderer.CliErrorDiagnostic>),
            typeof(string));
        AssertRenderJsonOverload(
            overloads,
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(int),
            typeof(IReadOnlyList<CliErrorRenderer.CliErrorDiagnostic>),
            typeof(string));
        AssertRenderJsonOverload(
            overloads,
            typeof(string),
            typeof(Exception),
            typeof(int),
            typeof(string));
    }

    [Fact]
    public void JsonContext_RootsOnlyPayloadAndDiagnosticWithAotOptions()
    {
        Assembly assembly = typeof(CliErrorRenderer).Assembly;
        Type context = assembly.GetType(
            JsonContextTypeName,
            throwOnError: true,
            ignoreCase: false)!;
        Type payload = assembly.GetType(
            PayloadTypeName,
            throwOnError: true,
            ignoreCase: false)!;

        Assert.True(context.IsNotPublic);
        Assert.True(context.IsSealed);
        Assert.True(typeof(JsonSerializerContext).IsAssignableFrom(context));

        Type[] roots = context
            .GetCustomAttributesData()
            .Where(attribute => attribute.AttributeType == typeof(JsonSerializableAttribute))
            .Select(attribute => (Type)Assert.Single(attribute.ConstructorArguments).Value!)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            [
                typeof(CliErrorRenderer.CliErrorDiagnostic),
                payload
            ],
            roots);

        JsonSourceGenerationOptionsAttribute options = Assert.Single(
            context.GetCustomAttributes<JsonSourceGenerationOptionsAttribute>());
        Assert.Equal(JsonKnownNamingPolicy.CamelCase, options.PropertyNamingPolicy);
        Assert.True(options.WriteIndented);
        Assert.Equal(
            JsonIgnoreCondition.WhenWritingNull,
            options.DefaultIgnoreCondition);
    }

    [Fact]
    public void RendererSource_UsesGeneratedTypeInfoWithoutReflectionFallback()
    {
        string source = File.ReadAllText(Path.Combine(
            RepoRoot,
            "src",
            "Bukit-Core",
            "Bukit.Cli.Shared",
            "Cli",
            "Rendering",
            "CliErrorRenderer.cs"));

        Assert.Single(
            Regex.Matches(
                source,
                @"\bJsonSerializer\.Serialize\s*\(",
                RegexOptions.CultureInvariant).Cast<Match>());
        Assert.Contains(
            "JsonSerializer.Serialize(payload, CliErrorJsonContext.Default.CliErrorPayload)",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "JsonSerializer.Serialize(payload)",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "new JsonSerializerOptions",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CurrentBaseline_RecordsFourteenAssemblies449TypesAnd10Candidates()
    {
        using JsonDocument current = ReadJson(
            "docs",
            "governance",
            "bukit-core-public-api-baseline.v1.json");
        JsonElement root = current.RootElement;
        JsonElement[] types = root.GetProperty("types").EnumerateArray().ToArray();

        Assert.Equal(14, root.GetProperty("assemblies").GetArrayLength());
        Assert.Equal(449, types.Length);
        Assert.Equal(10, types.Count(entry =>
            entry.GetProperty("compatibility").GetString() ==
            "2.0-candidate"));
        Assert.DoesNotContain(types, entry =>
            entry.GetProperty("assembly").GetString() ==
            "Bukit.Cli.Shared" &&
            entry.GetProperty("name").GetString() == PayloadTypeName);

        JsonElement renderer = Assert.Single(types, entry =>
            entry.GetProperty("assembly").GetString() ==
            "Bukit.Cli.Shared" &&
            entry.GetProperty("name").GetString() == RendererTypeName);
        Assert.Equal(
            "cross-assembly-implementation",
            renderer.GetProperty("classification").GetString());
        Assert.Equal(
            "1.x-do-not-narrow",
            renderer.GetProperty("compatibility").GetString());

        JsonElement diagnostic = Assert.Single(types, entry =>
            entry.GetProperty("assembly").GetString() ==
            "Bukit.Cli.Shared" &&
            entry.GetProperty("name").GetString() == DiagnosticTypeName);
        Assert.Equal(
            "cross-assembly-implementation",
            diagnostic.GetProperty("classification").GetString());
        Assert.Equal(
            "1.x-do-not-narrow",
            diagnostic.GetProperty("compatibility").GetString());
    }

    [Fact]
    public void ClosedManifest_PreservesHistoricalPayloadAndExactBlob()
    {
        string path = Path.Combine(
            RepoRoot,
            "docs",
            "governance",
            "bukit-core-2.0-public-surface-candidates.v1.json");
        byte[] bytes = File.ReadAllBytes(path);
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        JsonElement[] candidates =
            root.GetProperty("candidates").EnumerateArray().ToArray();

        Assert.Equal("closed", root.GetProperty("declarationState").GetString());
        Assert.Equal(136, root.GetProperty("candidateCount").GetInt32());
        Assert.Equal(136, candidates.Length);

        JsonElement candidate = Assert.Single(candidates, entry =>
            entry.GetProperty("assembly").GetString() ==
            "Bukit.Cli.Shared" &&
            entry.GetProperty("fullName").GetString() == PayloadTypeName);
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

    private static void AssertRenderJsonOverload(
        IEnumerable<MethodInfo> overloads,
        params Type[] parameterTypes)
    {
        MethodInfo method = Assert.Single(
            overloads,
            candidate => candidate.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .SequenceEqual(parameterTypes));

        Assert.True(method.IsPublic);
        Assert.True(method.IsStatic);
        Assert.Equal(typeof(string), method.ReturnType);
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
