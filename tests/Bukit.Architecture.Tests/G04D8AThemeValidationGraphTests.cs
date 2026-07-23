using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Bukit.Theme;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class G04D8AThemeValidationGraphTests
{
    private const string ErrorTypeName =
        "Bukit.Theme.SchemaValidationError";
    private const string ExceptionTypeName =
        "Bukit.Theme.SchemaValidationException";
    private const string CandidateManifestBlob =
        "7b07d6890562387010b52301e9f8716e9bf10ed1";
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void ValidationError_RemainsPublicSealedRecordShape()
    {
        Assembly assembly = typeof(SectionSchemaValidator).Assembly;
        Type error = GetType(assembly, ErrorTypeName);

        Assert.True(error.IsPublic);
        Assert.True(error.IsSealed);
        Assert.False(error.IsAbstract);
        Assert.Contains(error, assembly.GetExportedTypes());

        ConstructorInfo constructor = Assert.Single(
            error.GetConstructors(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly));
        Assert.Equal(
            [typeof(string), typeof(string)],
            constructor
                .GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray());

        PropertyInfo[] properties = error
            .GetProperties(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly)
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(["Message", "Section"], properties.Select(
            property => property.Name).ToArray());
        Assert.All(properties, property =>
        {
            Assert.Equal(typeof(string), property.PropertyType);
            Assert.True(property.GetMethod!.IsPublic);
            Assert.True(property.SetMethod!.IsPublic);
        });

        MethodInfo toString = error.GetMethod(
            nameof(ToString),
            BindingFlags.Public |
            BindingFlags.Instance |
            BindingFlags.DeclaredOnly,
            binder: null,
            Type.EmptyTypes,
            modifiers: null)!;
        Assert.NotNull(toString);
        Assert.Equal(typeof(string), toString.ReturnType);
    }

    [Fact]
    public void ValidationException_ExistsButIsInternalAndNotExported()
    {
        Assembly assembly = typeof(SectionSchemaValidator).Assembly;
        Type exception = GetType(assembly, ExceptionTypeName);

        Assert.True(exception.IsNotPublic);
        Assert.True(exception.IsSealed);
        Assert.False(exception.IsAbstract);
        Assert.Equal(typeof(Exception), exception.BaseType);
        Assert.DoesNotContain(exception, assembly.GetExportedTypes());

        ConstructorInfo constructor = Assert.Single(
            exception.GetConstructors(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly));
        ParameterInfo parameter = Assert.Single(constructor.GetParameters());
        Assert.Equal(typeof(string), parameter.ParameterType);
        Assert.False(parameter.IsOptional);
    }

    [Fact]
    public void PublicValidator_KeepsValidationErrorReturnGraph()
    {
        Type validator = typeof(SectionSchemaValidator);
        Assembly assembly = validator.Assembly;
        Type error = GetType(assembly, ErrorTypeName);
        MethodInfo validate = Assert.Single(
            validator.GetMethods(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly),
            method => method.Name == "Validate");

        Assert.True(validate.ReturnType.IsGenericType);
        Assert.Equal(
            typeof(List<>),
            validate.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(
            error,
            Assert.Single(validate.ReturnType.GetGenericArguments()));

        ParameterInfo[] parameters = validate.GetParameters();
        Assert.Equal(
            [
                typeof(string),
                typeof(ThemeSectionDefinition),
                typeof(IReadOnlyDictionary<string, object>)
            ],
            parameters
                .Select(parameter => parameter.ParameterType)
                .ToArray());
        Assert.All(parameters, parameter => Assert.False(parameter.IsOptional));
    }

    [Fact]
    public void ThemeFriendBoundary_RemainsEmpty()
    {
        string[] friends = typeof(SectionSchemaValidator)
            .Assembly
            .GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(attribute => attribute.AssemblyName.Split(',')[0])
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(friends);
    }

    [Fact]
    public void ThemeJsonContext_DoesNotRootValidationResultOrException()
    {
        Assembly assembly = typeof(SectionSchemaValidator).Assembly;
        Type context = GetType(assembly, "Bukit.Theme.JsonContext");
        Type error = GetType(assembly, ErrorTypeName);
        Type exception = GetType(assembly, ExceptionTypeName);
        Type[] roots = context
            .GetProperties(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly)
            .Select(property => property.PropertyType)
            .Where(type =>
                type.IsGenericType &&
                type.GetGenericTypeDefinition() == typeof(JsonTypeInfo<>))
            .Select(type => type.GetGenericArguments()[0])
            .ToArray();

        Assert.Contains(typeof(SectionSchema), roots);
        Assert.Contains(typeof(SchemaPropDefinition), roots);
        Assert.DoesNotContain(error, roots);
        Assert.DoesNotContain(exception, roots);
    }

    [Fact]
    public void CurrentBaseline_RecordsRetainedErrorAnd478Types40Candidates()
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
        Assert.Equal(478, types.Length);
        Assert.Equal(40, types.Count(entry =>
            entry.GetProperty("compatibility").GetString() ==
            "2.0-candidate"));

        JsonElement error = Assert.Single(types, entry =>
            entry.GetProperty("assembly").GetString() == "Bukit.Theme" &&
            entry.GetProperty("name").GetString() == ErrorTypeName);
        Assert.Equal(
            "cross-assembly-implementation",
            error.GetProperty("classification").GetString());
        Assert.Equal(
            "1.x-do-not-narrow",
            error.GetProperty("compatibility").GetString());
        Assert.Equal(
            "2.0-review",
            error.GetProperty("migrationHorizon").GetString());
        Assert.Contains(
            error.GetProperty("publicMembers").EnumerateArray(),
            member => member.GetString() ==
                "public System.String! Message { get; init; }");
        Assert.Contains(
            error.GetProperty("publicMembers").EnumerateArray(),
            member => member.GetString() ==
                "public System.String! Section { get; init; }");

        Assert.DoesNotContain(types, entry =>
            entry.GetProperty("assembly").GetString() == "Bukit.Theme" &&
            entry.GetProperty("name").GetString() == ExceptionTypeName);

        JsonElement validator = Assert.Single(types, entry =>
            entry.GetProperty("assembly").GetString() == "Bukit.Theme" &&
            entry.GetProperty("name").GetString() ==
            "Bukit.Theme.SectionSchemaValidator");
        Assert.Contains(
            validator.GetProperty("publicMembers").EnumerateArray(),
            member => member.GetString()!.Contains(
                "List<Bukit.Theme.SchemaValidationError",
                StringComparison.Ordinal));
    }

    [Fact]
    public void ClosedManifest_PreservesBothHistoricalCandidatesAndExactBlob()
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

        string[] expected = [ErrorTypeName, ExceptionTypeName];
        JsonElement[] historical = candidates
            .Where(entry =>
                entry.GetProperty("assembly").GetString() == "Bukit.Theme" &&
                expected.Contains(
                    entry.GetProperty("fullName").GetString()!,
                    StringComparer.Ordinal))
            .OrderBy(
                entry => entry.GetProperty("fullName").GetString()!,
                StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(2, historical.Length);
        Assert.Equal(
            expected.OrderBy(name => name, StringComparer.Ordinal),
            historical.Select(entry =>
                entry.GetProperty("fullName").GetString()!));
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
