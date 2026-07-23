using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class G04D2FProcessGraphTests
{
    private const string CandidateManifestBlob =
        "7b07d6890562387010b52301e9f8716e9bf10ed1";
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void ProcessGraph_RemainsPublicAndExportedAsOneRetainedSeam()
    {
        var assembly = typeof(Bukit.PluginHost.PluginProtocolClient).Assembly;
        var exportedTypeNames = assembly.GetExportedTypes()
            .Select(type => type.FullName)
            .ToHashSet(StringComparer.Ordinal);

        foreach (Type type in RetainedTypes)
        {
            Assert.True(type.IsPublic);
            Assert.Contains(type.FullName, exportedTypeNames);
        }
    }

    [Fact]
    public void RetainedCompanions_PreserveTheCompletePublicPropagationGraph()
    {
        Type protocolClient = typeof(Bukit.PluginHost.PluginProtocolClient);
        Type processInvoker = typeof(Bukit.PluginHost.PluginProcessInvoker);
        Type requestIdFactory = typeof(Bukit.PluginHost.PluginRequestIdFactory);
        Type processRunner = typeof(Bukit.PluginHost.SystemProcessRunner);

        Assert.Contains(
            protocolClient.GetConstructors(BindingFlags.Public | BindingFlags.Instance),
            constructor =>
            {
                Type[] parameterTypes = constructor.GetParameters()
                    .Select(parameter => parameter.ParameterType)
                    .ToArray();
                return parameterTypes.Contains(typeof(Bukit.PluginHost.IPluginProcessInvoker))
                    && parameterTypes.Contains(typeof(Bukit.PluginHost.IPluginRequestIdFactory));
            });

        Assert.Contains(typeof(Bukit.PluginHost.IPluginProcessInvoker), processInvoker.GetInterfaces());
        var invokerConstructor = Assert.Single(
            processInvoker.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Equal(
            [typeof(Bukit.PluginHost.IProcessRunner)],
            invokerConstructor.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray());
        MethodInfo invokeAsync = Assert.Single(
            processInvoker.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => method.Name == "InvokeAsync"));
        Assert.Equal(
            typeof(Task<Bukit.PluginHost.PluginProcessResult>),
            invokeAsync.ReturnType);
        Assert.Equal(
            typeof(Bukit.PluginHost.PluginProcessRequest),
            invokeAsync.GetParameters()[0].ParameterType);

        Assert.Contains(typeof(Bukit.PluginHost.IPluginRequestIdFactory), requestIdFactory.GetInterfaces());
        MethodInfo create = Assert.Single(
            requestIdFactory.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => method.Name == "Create"));
        Assert.Equal(typeof(string), create.ReturnType);

        Assert.Contains(typeof(Bukit.PluginHost.IProcessRunner), processRunner.GetInterfaces());
        MethodInfo runAsync = Assert.Single(
            processRunner.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => method.Name == "RunAsync"));
        Assert.Equal(
            typeof(Task<Bukit.PluginHost.ProcessRunResult>),
            runAsync.ReturnType);
        Assert.Equal(
            typeof(Bukit.PluginHost.ProcessRunRequest),
            runAsync.GetParameters()[0].ParameterType);

        Assert.Equal(
            typeof(Bukit.PluginHost.ProcessOutputStream?),
            typeof(Bukit.PluginHost.PluginProcessResult)
                .GetProperty("OutputLimitStream")!
                .PropertyType);
        Assert.Equal(
            typeof(Bukit.PluginHost.ProcessOutputStream?),
            typeof(Bukit.PluginHost.ProcessRunResult)
                .GetProperty("OutputLimitStream")!
                .PropertyType);
    }

    [Fact]
    public void CurrentBaseline_RetainsExactMetadataAndCounts()
    {
        using var document = ReadJson(
            "docs",
            "governance",
            "bukit-core-public-api-baseline.v1.json");
        var root = document.RootElement;
        var types = root.GetProperty("types").EnumerateArray().ToArray();

        Assert.Equal(14, root.GetProperty("assemblies").GetArrayLength());
        Assert.Equal(497, types.Length);
        Assert.Equal(85, types.Count(entry =>
            entry.GetProperty("compatibility").GetString() == "2.0-candidate"));

        foreach (Type type in RetainedTypes)
        {
            var entry = Assert.Single(types, candidate =>
                candidate.GetProperty("assembly").GetString() == "Bukit.PluginHost" &&
                candidate.GetProperty("name").GetString() == type.FullName);

            Assert.Equal(
                "cross-assembly-implementation",
                entry.GetProperty("classification").GetString());
            Assert.Equal(
                "1.x-do-not-narrow",
                entry.GetProperty("compatibility").GetString());
            Assert.Equal(
                "2.0-review",
                entry.GetProperty("migrationHorizon").GetString());
        }
    }

    [Fact]
    public void ClosedManifest_PreservesAllEightHistoricalCandidatesAndExactBlob()
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

        foreach (Type type in RetainedTypes)
        {
            Assert.Single(candidates, entry =>
                entry.GetProperty("assembly").GetString() == "Bukit.PluginHost" &&
                entry.GetProperty("fullName").GetString() == type.FullName);
        }

        var prefix = Encoding.UTF8.GetBytes($"blob {bytes.Length}\0");
        var blobBytes = new byte[prefix.Length + bytes.Length];
        prefix.CopyTo(blobBytes, 0);
        bytes.CopyTo(blobBytes, prefix.Length);

        Assert.Equal(
            CandidateManifestBlob,
            Convert.ToHexStringLower(SHA1.HashData(blobBytes)));
    }

    private static Type[] RetainedTypes =>
    [
        typeof(Bukit.PluginHost.IPluginProcessInvoker),
        typeof(Bukit.PluginHost.IPluginRequestIdFactory),
        typeof(Bukit.PluginHost.IProcessRunner),
        typeof(Bukit.PluginHost.PluginProcessRequest),
        typeof(Bukit.PluginHost.PluginProcessResult),
        typeof(Bukit.PluginHost.ProcessOutputStream),
        typeof(Bukit.PluginHost.ProcessRunRequest),
        typeof(Bukit.PluginHost.ProcessRunResult)
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
