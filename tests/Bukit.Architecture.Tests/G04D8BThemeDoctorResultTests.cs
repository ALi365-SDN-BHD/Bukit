using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Bukit.Cli.Commands;
using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Theme;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class G04D8BThemeDoctorResultTests
{
    private const string DoctorResultTypeName =
        "Bukit.Theme.ThemeDoctorCommand+DoctorResult";
    private const string CandidateManifestBlob =
        "7b07d6890562387010b52301e9f8716e9bf10ed1";
    private const string CurrentBaselineStatement =
        "The current public API baseline contains 449 types, including 10 `2.0-candidate` entries.";
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void DoctorResult_RemainsPublicNestedSealedRecordWithListShape()
    {
        Assembly assembly = typeof(ThemeDoctorCommand).Assembly;
        Type result = GetType(assembly, DoctorResultTypeName);

        Assert.True(result.IsNestedPublic);
        Assert.True(result.IsSealed);
        Assert.False(result.IsAbstract);
        Assert.Contains(result, assembly.GetExportedTypes());
        Assert.Contains(
            typeof(IEquatable<ThemeDoctorCommand.DoctorResult>),
            result.GetInterfaces());

        ConstructorInfo constructor = Assert.Single(
            result.GetConstructors(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly));
        Assert.Equal(
            [typeof(bool), typeof(bool), typeof(List<string>)],
            constructor
                .GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray());

        PropertyInfo[] properties = result
            .GetProperties(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly)
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            ["HasErrors", "HasWarnings", "Issues"],
            properties.Select(property => property.Name).ToArray());
        Assert.Equal(
            [typeof(bool), typeof(bool), typeof(List<string>)],
            properties.Select(property => property.PropertyType).ToArray());
        Assert.All(properties, property =>
        {
            Assert.True(property.GetMethod!.IsPublic);
            Assert.True(property.SetMethod!.IsPublic);
        });
    }

    [Fact]
    public void PublicThemeDoctorFacade_KeepsExactDoctorResultGraph()
    {
        Type facade = typeof(ThemeDoctorCommand);
        Type result = typeof(ThemeDoctorCommand.DoctorResult);
        MethodInfo diagnose = Assert.Single(
            facade.GetMethods(
                BindingFlags.Public |
                BindingFlags.Static |
                BindingFlags.DeclaredOnly),
            method => method.Name == nameof(ThemeDoctorCommand.Diagnose));
        MethodInfo printReport = Assert.Single(
            facade.GetMethods(
                BindingFlags.Public |
                BindingFlags.Static |
                BindingFlags.DeclaredOnly),
            method => method.Name == nameof(ThemeDoctorCommand.PrintReport));

        Assert.Equal(result, diagnose.ReturnType);
        Assert.Equal(
            [
                typeof(string),
                typeof(ThemeManifestV2),
                typeof(ThemeComponentRegistry)
            ],
            diagnose
                .GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray());
        Assert.All(
            diagnose.GetParameters(),
            parameter => Assert.False(parameter.IsOptional));

        Assert.Equal(typeof(void), printReport.ReturnType);
        ParameterInfo printParameter =
            Assert.Single(printReport.GetParameters());
        Assert.Equal(result, printParameter.ParameterType);
        Assert.False(printParameter.IsOptional);
    }

    [Fact]
    public void CoreCliDoctor_RemainsIndependentTextAndExitCodePipeline()
    {
        string source = File.ReadAllText(Path.Combine(
            RepoRoot,
            "src",
            "Bukit-Core",
            "Bukit.Cli",
            "Commands",
            "DoctorCommand.cs"));
        string project = File.ReadAllText(Path.Combine(
            RepoRoot,
            "src",
            "Bukit-Core",
            "Bukit.Cli",
            "Bukit.Cli.csproj"));

        Assert.DoesNotContain(
            "ThemeDoctorCommand",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "DoctorResult",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Bukit.Theme.csproj",
            project,
            StringComparison.Ordinal);

        MethodInfo runAsync = Assert.Single(
            typeof(DoctorCommand).GetMethods(
                BindingFlags.Public |
                BindingFlags.Static |
                BindingFlags.DeclaredOnly),
            method => method.Name == nameof(DoctorCommand.RunAsync));
        Assert.Equal(typeof(Task<int>), runAsync.ReturnType);
        ParameterInfo parameter = Assert.Single(runAsync.GetParameters());
        Assert.Equal(typeof(CliBoundCommand), parameter.ParameterType);
        Assert.False(parameter.IsOptional);
    }

    [Fact]
    public void ThemeJsonContexts_DoNotRootDoctorResult()
    {
        Assembly assembly = typeof(ThemeDoctorCommand).Assembly;
        Type result = typeof(ThemeDoctorCommand.DoctorResult);
        Type[] contexts = assembly
            .GetTypes()
            .Where(type =>
                type != typeof(JsonSerializerContext) &&
                typeof(JsonSerializerContext).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();
        Type[] roots = contexts
            .SelectMany(context =>
                context.GetCustomAttributesData())
            .Where(attribute =>
                attribute.AttributeType ==
                typeof(JsonSerializableAttribute))
            .Select(attribute =>
                (Type)Assert.Single(
                    attribute.ConstructorArguments).Value!)
            .Distinct()
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(2, contexts.Length);
        Type[] expectedRoots =
        [
            typeof(Dictionary<string, SchemaPropDefinition>),
            typeof(SchemaPropDefinition),
            typeof(SectionSchema),
            typeof(ThemeCatalog),
            typeof(ThemeCatalogComponentEntry),
            typeof(ThemeCatalogSectionEntry)
        ];
        Assert.Equal(
            expectedRoots.OrderBy(
                type => type.FullName,
                StringComparer.Ordinal),
            roots);
        Assert.DoesNotContain(result, roots);

        Assert.All(contexts, context =>
        {
            PropertyInfo[] typeInfoProperties = context
                .GetProperties(
                    BindingFlags.Public |
                    BindingFlags.Instance |
                    BindingFlags.DeclaredOnly)
                .Where(property =>
                    property.PropertyType.IsGenericType &&
                    property.PropertyType.GetGenericTypeDefinition() ==
                    typeof(JsonTypeInfo<>))
                .ToArray();
            Assert.DoesNotContain(
                typeInfoProperties,
                property =>
                    property.PropertyType.GetGenericArguments()[0] == result);
        });
    }

    [Fact]
    public void CurrentBaseline_RetainsDoctorResultAndRecords449Types10Candidates()
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

        JsonElement result = Assert.Single(types, entry =>
            entry.GetProperty("assembly").GetString() == "Bukit.Theme" &&
            entry.GetProperty("name").GetString() == DoctorResultTypeName);
        Assert.Equal(
            "Theme runtime",
            result.GetProperty("owner").GetString());
        Assert.Equal(
            "cross-assembly-implementation",
            result.GetProperty("classification").GetString());
        Assert.Equal(
            "1.x-do-not-narrow",
            result.GetProperty("compatibility").GetString());
        Assert.Equal(
            "2.0-review",
            result.GetProperty("migrationHorizon").GetString());
        Assert.Contains(
            result.GetProperty("publicMembers").EnumerateArray(),
            member => member.GetString() ==
                "public System.Boolean HasErrors { get; init; }");
        Assert.Contains(
            result.GetProperty("publicMembers").EnumerateArray(),
            member => member.GetString() ==
                "public System.Boolean HasWarnings { get; init; }");
        Assert.Contains(
            result.GetProperty("publicMembers").EnumerateArray(),
            member => member.GetString() ==
                "public System.Collections.Generic.List<System.String!>! Issues { get; init; }");
    }

    [Fact]
    public void ActiveGovernance_RecordsFinalGroupThreeBaseline()
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
                "The current public API baseline contains 484 types, " +
                "including 57 `2.0-candidate` entries.",
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

        Assert.Equal("closed", root.GetProperty("declarationState").GetString());
        Assert.Equal(136, root.GetProperty("candidateCount").GetInt32());
        Assert.Equal(136, candidates.Length);

        JsonElement historical = Assert.Single(candidates, entry =>
            entry.GetProperty("assembly").GetString() == "Bukit.Theme" &&
            entry.GetProperty("fullName").GetString() ==
            DoctorResultTypeName);
        Assert.Equal(
            "consumer-declaration-pending",
            historical.GetProperty("declarationStatus").GetString());
        Assert.Equal(
            "unknown-until-voluntary-declaration",
            historical.GetProperty("privateConsumerStatus").GetString());
        Assert.Equal(
            "no-public-match-found",
            historical.GetProperty("externalEvidence")
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
