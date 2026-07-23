using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bukit.Rendering;
using Bukit.Rendering.Scriban;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class G04D6BScribanModelBinderTests
{
    private const string TypeName =
        "Bukit.Rendering.Scriban.ScribanModelBinder";
    private const string CandidateManifestBlob =
        "7b07d6890562387010b52301e9f8716e9bf10ed1";
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void Binder_ExistsButIsInternalStaticAndNotExported()
    {
        Assembly assembly = typeof(ScribanTemplateRenderer).Assembly;
        Type binder = GetBinderType(assembly);

        Assert.True(binder.IsNotPublic);
        Assert.True(binder.IsAbstract);
        Assert.True(binder.IsSealed);
        Assert.DoesNotContain(binder, assembly.GetExportedTypes());
    }

    [Fact]
    public void Binder_PreservesBothPublicStaticOverloads()
    {
        Type binder = GetBinderType(
            typeof(ScribanTemplateRenderer).Assembly);
        MethodInfo[] overloads = binder
            .GetMethods(
                BindingFlags.Public |
                BindingFlags.Static |
                BindingFlags.DeclaredOnly)
            .Where(method => method.Name == "ToScriptObject")
            .OrderBy(
                method => method.GetParameters()[0].ParameterType.FullName,
                StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(2, overloads.Length);
        Assert.All(overloads, method =>
        {
            Assert.Equal(
                "Scriban.Runtime.ScriptObject",
                method.ReturnType.FullName);
            Assert.True(method.IsPublic);
            Assert.True(method.IsStatic);
            Assert.False(method.IsGenericMethod);
            Assert.Single(method.GetParameters());
        });
        Assert.Equal(
            [typeof(ListPageModel), typeof(PageModel)],
            overloads
                .Select(method => method.GetParameters()[0].ParameterType)
                .ToArray());
    }

    [Fact]
    public void PublicRenderer_KeepsBothDirectBinderRoots()
    {
        string rendererSource = File.ReadAllText(Path.Combine(
            RepoRoot,
            "src",
            "Bukit-Core",
            "Bukit.Rendering",
            "Scriban",
            "ScribanTemplateRenderer.cs"));

        Assert.Equal(
            2,
            Regex.Matches(
                    rendererSource,
                    @"\bScribanModelBinder\.ToScriptObject\(model\)",
                    RegexOptions.CultureInvariant)
                .Count);
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
    public void CurrentBaseline_RecordsFourteenAssemblies484TypesAnd57Candidates()
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
        Assert.Equal(484, types.Length);
        Assert.Equal(57, types.Count(entry =>
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

    private static Type GetBinderType(Assembly assembly) =>
        assembly.GetType(
            TypeName,
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
