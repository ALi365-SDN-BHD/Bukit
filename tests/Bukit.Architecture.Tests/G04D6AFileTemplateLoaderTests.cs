using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bukit.Rendering.Scriban;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class G04D6AFileTemplateLoaderTests
{
    private const string TypeName =
        "Bukit.Rendering.Scriban.FileTemplateLoader";
    private const string InterfaceName =
        "Scriban.Runtime.ITemplateLoader";
    private const string CandidateManifestBlob =
        "7b07d6890562387010b52301e9f8716e9bf10ed1";
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void Loader_ExistsButIsInternalSealedAndNotExported()
    {
        Assembly assembly = typeof(ScribanTemplateRenderer).Assembly;
        Type loader = GetLoaderType(assembly);

        Assert.True(loader.IsNotPublic);
        Assert.True(loader.IsSealed);
        Assert.False(loader.IsAbstract);
        Assert.DoesNotContain(loader, assembly.GetExportedTypes());

        Type loaderInterface = Assert.Single(
            loader.GetInterfaces(),
            candidate => candidate.FullName == InterfaceName);
        Assert.Equal(InterfaceName, loaderInterface.FullName);
    }

    [Fact]
    public void Loader_PreservesConstructorAndScribanInterfaceShape()
    {
        Type loader = GetLoaderType(
            typeof(ScribanTemplateRenderer).Assembly);

        ConstructorInfo constructor = Assert.Single(
            loader.GetConstructors(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly));
        ParameterInfo[] constructorParameters = constructor.GetParameters();
        Assert.Equal(
            [typeof(string), typeof(string), typeof(string)],
            constructorParameters
                .Select(parameter => parameter.ParameterType)
                .ToArray());
        Assert.False(constructorParameters[0].IsOptional);
        Assert.True(constructorParameters[1].IsOptional);
        Assert.Null(constructorParameters[1].DefaultValue);
        Assert.True(constructorParameters[2].IsOptional);
        Assert.Null(constructorParameters[2].DefaultValue);

        Type loaderInterface = Assert.Single(
            loader.GetInterfaces(),
            candidate => candidate.FullName == InterfaceName);
        InterfaceMapping map = loader.GetInterfaceMap(loaderInterface);
        Assert.Equal(3, map.InterfaceMethods.Length);
        Assert.Equal(3, map.TargetMethods.Length);
        Assert.All(map.TargetMethods, method =>
        {
            Assert.True(method.IsPublic);
            Assert.True(method.IsVirtual);
            Assert.True(method.IsFinal);
            Assert.Equal(loader, method.DeclaringType);
        });

        MethodInfo getPath = GetDeclaredPublicMethod(loader, "GetPath");
        Assert.Equal(typeof(string), getPath.ReturnType);
        AssertParameterTypeNames(
            getPath,
            "Scriban.TemplateContext",
            "Scriban.Parsing.SourceSpan",
            "System.String");

        MethodInfo load = GetDeclaredPublicMethod(loader, "Load");
        Assert.Equal(typeof(string), load.ReturnType);
        AssertParameterTypeNames(
            load,
            "Scriban.TemplateContext",
            "Scriban.Parsing.SourceSpan",
            "System.String");

        MethodInfo loadAsync = GetDeclaredPublicMethod(loader, "LoadAsync");
        Assert.True(loadAsync.ReturnType.IsGenericType);
        Assert.Equal(
            typeof(ValueTask<>),
            loadAsync.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(
            typeof(string),
            Assert.Single(loadAsync.ReturnType.GetGenericArguments()));
        AssertParameterTypeNames(
            loadAsync,
            "Scriban.TemplateContext",
            "Scriban.Parsing.SourceSpan",
            "System.String");
    }

    [Fact]
    public void RenderingFriendBoundary_RemainsEngineAndOwnerTestsOnly()
    {
        Assembly assembly = typeof(ScribanTemplateRenderer).Assembly;
        string[] friends = assembly
            .GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(attribute => attribute.AssemblyName.Split(',')[0])
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "Bukit.Engine",
                "Bukit.Rendering.Tests"
            ],
            friends);
    }

    [Fact]
    public void CurrentBaseline_RecordsFourteenAssemblies469TypesAnd31Candidates()
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
        Assert.DoesNotContain(types, entry =>
            entry.GetProperty("assembly").GetString() ==
            "Bukit.Rendering" &&
            entry.GetProperty("name").GetString() == TypeName);
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

        Assert.Equal("closed", root.GetProperty("declarationState").GetString());
        Assert.Equal(136, root.GetProperty("candidateCount").GetInt32());
        Assert.Equal(136, candidates.Length);

        JsonElement candidate = Assert.Single(candidates, entry =>
            entry.GetProperty("assembly").GetString() ==
            "Bukit.Rendering" &&
            entry.GetProperty("fullName").GetString() == TypeName);
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

    private static Type GetLoaderType(Assembly assembly) =>
        assembly.GetType(
            TypeName,
            throwOnError: true,
            ignoreCase: false)!;

    private static MethodInfo GetDeclaredPublicMethod(
        Type type,
        string name) =>
        Assert.Single(type.GetMethods(
            BindingFlags.Public |
            BindingFlags.Instance |
            BindingFlags.DeclaredOnly), method => method.Name == name);

    private static void AssertParameterTypeNames(
        MethodInfo method,
        params string[] expected) =>
        Assert.Equal(
            expected,
            method.GetParameters()
                .Select(parameter => parameter.ParameterType.FullName)
                .ToArray());

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
