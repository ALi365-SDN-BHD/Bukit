using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class G04D2GExecutionReportTests
{
    private const string CandidateManifestBlob =
        "7b07d6890562387010b52301e9f8716e9bf10ed1";
    private static readonly string[] CandidateTypeNames =
    [
        "Bukit.PluginHost.PluginExecutionReport",
        "Bukit.PluginHost.PluginExecutionReporter",
        "Bukit.PluginHost.PluginExecutionResponseSummary"
    ];
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void ExecutionReportClrTypes_ExistButAreInternalAndNotExported()
    {
        var assembly = typeof(Bukit.PluginHost.PluginProtocolClient).Assembly;
        var exportedTypeNames = assembly.GetExportedTypes()
            .Select(type => type.FullName)
            .ToHashSet(StringComparer.Ordinal);

        foreach (string typeName in CandidateTypeNames)
        {
            Type type = assembly.GetType(
                typeName,
                throwOnError: true,
                ignoreCase: false)!;

            Assert.False(type.IsPublic);
            Assert.DoesNotContain(typeName, exportedTypeNames);
        }
    }

    [Fact]
    public void ProtocolClient_PublicConstructionUsesOnlyRetainedProcessSeams()
    {
        Type client = typeof(Bukit.PluginHost.PluginProtocolClient);
        ConstructorInfo publicConstructor = Assert.Single(
            client.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Equal(
            [
                typeof(Bukit.PluginHost.IPluginProcessInvoker),
                typeof(Bukit.PluginHost.IPluginRequestIdFactory)
            ],
            publicConstructor.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray());

        Type reporter = client.Assembly.GetType(
            "Bukit.PluginHost.PluginExecutionReporter",
            throwOnError: true,
            ignoreCase: false)!;
        ConstructorInfo injectionConstructor = Assert.Single(
            client.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance),
            constructor =>
                constructor.GetParameters().Length == 3 &&
                constructor.GetParameters()[2].ParameterType == reporter);
        ParameterInfo reporterParameter = injectionConstructor.GetParameters()[2];

        Assert.True(injectionConstructor.IsAssembly);
        Assert.False(reporterParameter.IsOptional);
        Assert.False(reporterParameter.HasDefaultValue);
        Assert.Equal(
            NullabilityState.NotNull,
            new NullabilityInfoContext().Create(reporterParameter).ReadState);
    }

    [Fact]
    public void CurrentBaseline_RemovesClrGraphAndRetainsTwoDependencyConstructor()
    {
        using var document = ReadJson(
            "docs",
            "governance",
            "bukit-core-public-api-baseline.v1.json");
        var root = document.RootElement;
        var types = root.GetProperty("types").EnumerateArray().ToArray();

        Assert.Equal(14, root.GetProperty("assemblies").GetArrayLength());
        Assert.Equal(485, types.Length);
        Assert.Equal(59, types.Count(entry =>
            entry.GetProperty("compatibility").GetString() == "2.0-candidate"));
        Assert.All(CandidateTypeNames, typeName =>
            Assert.DoesNotContain(types, entry =>
                entry.GetProperty("assembly").GetString() == "Bukit.PluginHost" &&
                entry.GetProperty("name").GetString() == typeName));

        JsonElement client = Assert.Single(types, entry =>
            entry.GetProperty("assembly").GetString() == "Bukit.PluginHost" &&
            entry.GetProperty("name").GetString() ==
            "Bukit.PluginHost.PluginProtocolClient");
        string[] members = client.GetProperty("publicMembers")
            .EnumerateArray()
            .Select(member => member.GetString()!)
            .ToArray();

        Assert.Contains(
            "public .ctor(Bukit.PluginHost.IPluginProcessInvoker! processInvoker, Bukit.PluginHost.IPluginRequestIdFactory! requestIdFactory)",
            members);
        Assert.DoesNotContain(members, member =>
            member.Contains("PluginExecutionReporter", StringComparison.Ordinal));
    }

    [Fact]
    public void ExecutionReportV1SchemaAndGoldenFixture_AreOutOfBandContracts()
    {
        string schemaPath = Path.Combine(
            RepoRoot,
            "docs",
            "schemas",
            "plugin-execution-report.v1.schema.json");
        string goldenPath = Path.Combine(
            RepoRoot,
            "tests",
            "fixtures",
            "plugin-contracts",
            "plugin-execution-report.v1.json");

        Assert.True(File.Exists(schemaPath));
        Assert.True(File.Exists(goldenPath));

        using JsonDocument schema = JsonDocument.Parse(File.ReadAllText(schemaPath));
        using JsonDocument golden = JsonDocument.Parse(File.ReadAllText(goldenPath));
        Assert.Equal(
            "https://bukit.dev/schemas/plugin-execution-report.v1.schema.json",
            schema.RootElement.GetProperty("$id").GetString());
        Assert.False(
            schema.RootElement.GetProperty("additionalProperties").GetBoolean());
        Assert.False(golden.RootElement.TryGetProperty("schemaVersion", out _));
        Assert.False(golden.RootElement.TryGetProperty("stdout", out _));
        Assert.True(golden.RootElement.TryGetProperty("stdoutBytes", out _));
    }

    [Fact]
    public void ClosedManifest_PreservesAllThreeHistoricalCandidatesAndExactBlob()
    {
        string path = Path.Combine(
            RepoRoot,
            "docs",
            "governance",
            "bukit-core-2.0-public-surface-candidates.v1.json");
        byte[] bytes = File.ReadAllBytes(path);
        using JsonDocument document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        var candidates = root.GetProperty("candidates").EnumerateArray().ToArray();

        Assert.Equal("closed", root.GetProperty("declarationState").GetString());
        Assert.Equal(136, root.GetProperty("candidateCount").GetInt32());
        Assert.Equal(136, candidates.Length);
        foreach (string typeName in CandidateTypeNames)
        {
            JsonElement candidate = Assert.Single(candidates, entry =>
                entry.GetProperty("assembly").GetString() == "Bukit.PluginHost" &&
                entry.GetProperty("fullName").GetString() == typeName);
            Assert.Equal(
                "consumer-declaration-pending",
                candidate.GetProperty("declarationStatus").GetString());
        }

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
