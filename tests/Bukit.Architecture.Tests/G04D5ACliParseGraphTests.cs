using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Cli.Shared.Cli.Metadata;
using Bukit.Cli.Shared.Cli.Parsing;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class G04D5ACliParseGraphTests
{
    private const string FactoryTypeName =
        "Bukit.Cli.Shared.Cli.Binding.CliBoundCommandFactory";
    private const string BaseTypeName =
        "Bukit.Cli.Shared.Cli.Parsing.CliParseResult";
    private const string SimpleTypeName =
        "Bukit.Cli.Shared.Cli.Parsing.SimpleParseResult";
    private const string SubcommandTypeName =
        "Bukit.Cli.Shared.Cli.Parsing.SubcommandParseResult";
    private const string CandidateManifestBlob =
        "7b07d6890562387010b52301e9f8716e9bf10ed1";
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void FactoryAndConcreteResults_ExistButAreInternalAndNotExported()
    {
        Assembly assembly = typeof(CliParseResult).Assembly;
        Type[] exportedTypes = assembly.GetExportedTypes();

        Type factory = GetType(assembly, FactoryTypeName);
        Type simple = GetType(assembly, SimpleTypeName);
        Type subcommand = GetType(assembly, SubcommandTypeName);

        foreach (Type type in new[] { factory, simple, subcommand })
        {
            Assert.True(type.IsNotPublic);
            Assert.DoesNotContain(type, exportedTypes);
        }

        Assert.True(factory.IsAbstract);
        Assert.True(factory.IsSealed);
        Assert.Equal(typeof(CliParseResult), simple.BaseType);
        Assert.True(simple.IsSealed);
        Assert.Equal(typeof(CliParseResult), subcommand.BaseType);
        Assert.True(subcommand.IsSealed);
    }

    [Fact]
    public void CliParseResult_RemainsPublicAbstractRecordWithExactShape()
    {
        Type type = typeof(CliParseResult);

        Assert.Equal(BaseTypeName, type.FullName);
        Assert.True(type.IsPublic);
        Assert.True(type.IsAbstract);
        Assert.False(type.IsSealed);
        Assert.Equal(typeof(object), type.BaseType);
        Assert.Contains(typeof(IEquatable<CliParseResult>), type.GetInterfaces());
        Assert.Equal(
            ["BoundCommand", "Command", "Diagnostics", "IsSuccess"],
            type.GetProperties(
                    BindingFlags.Public |
                    BindingFlags.Instance |
                    BindingFlags.DeclaredOnly)
                .Select(property => property.Name)
                .Order(StringComparer.Ordinal)
                .ToArray());

        AssertInitProperty(type, "Command", typeof(CliCommandSpec));
        AssertInitProperty(type, "BoundCommand", typeof(CliBoundCommand));
        AssertInitProperty(
            type,
            "Diagnostics",
            typeof(IReadOnlyList<CliDiagnostic>));

        PropertyInfo isSuccess = Assert.Single(
            type.GetProperties(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly),
            property => property.Name == "IsSuccess");
        Assert.Equal(typeof(bool), isSuccess.PropertyType);
        Assert.NotNull(isSuccess.GetMethod);
        Assert.True(isSuccess.GetMethod!.IsPublic);
        Assert.Null(isSuccess.SetMethod);

        PropertyInfo equalityContract = Assert.Single(
            type.GetProperties(
                BindingFlags.NonPublic |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly),
            property => property.Name == "EqualityContract");
        Assert.Equal(typeof(Type), equalityContract.PropertyType);
        Assert.NotNull(equalityContract.GetMethod);
        Assert.True(equalityContract.GetMethod!.IsFamily);
        Assert.True(equalityContract.GetMethod.IsVirtual);

        ConstructorInfo[] constructors = type.GetConstructors(
            BindingFlags.NonPublic |
            BindingFlags.Instance |
            BindingFlags.DeclaredOnly);
        Assert.Equal(2, constructors.Length);
        Assert.Contains(
            constructors,
            constructor =>
                constructor.IsFamily &&
                HasParameterTypes(
                    constructor,
                    typeof(CliCommandSpec),
                    typeof(CliBoundCommand),
                    typeof(IReadOnlyList<CliDiagnostic>)));
        Assert.Contains(
            constructors,
            constructor =>
                constructor.IsFamily &&
                HasParameterTypes(constructor, typeof(CliParseResult)));

        MethodInfo deconstruct = Assert.Single(
            type.GetMethods(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly),
            method => method.Name == "Deconstruct");
        Assert.Equal(typeof(void), deconstruct.ReturnType);
        Assert.True(HasParameterTypes(
            deconstruct,
            typeof(CliCommandSpec).MakeByRefType(),
            typeof(CliBoundCommand).MakeByRefType(),
            typeof(IReadOnlyList<CliDiagnostic>).MakeByRefType()));

        MethodInfo clone = Assert.Single(
            type.GetMethods(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly),
            method => method.Name == "<Clone>$");
        Assert.Equal(typeof(CliParseResult), clone.ReturnType);
        Assert.True(clone.IsAbstract);

        MethodInfo printMembers = Assert.Single(
            type.GetMethods(
                BindingFlags.NonPublic |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly),
            method => method.Name == "PrintMembers");
        Assert.True(printMembers.IsFamily);
        Assert.True(printMembers.IsVirtual);
        Assert.Equal(typeof(bool), printMembers.ReturnType);
        Assert.True(HasParameterTypes(
            printMembers,
            typeof(StringBuilder)));
    }

    [Fact]
    public void CliParserAndCommandDescriptor_PreserveBaseTypeSignatures()
    {
        MethodInfo parse = Assert.Single(
            typeof(CliParser).GetMethods(
                BindingFlags.Public |
                BindingFlags.Static |
                BindingFlags.DeclaredOnly),
            method => method.Name == "Parse");
        Assert.Equal(typeof(CliParseResult), parse.ReturnType);
        Assert.True(HasParameterTypes(
            parse,
            typeof(CliCommandSpec),
            typeof(IReadOnlyList<string>)));

        MethodInfo dispatch = Assert.Single(
            typeof(CommandDescriptor).GetMethods(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly),
            method => method.Name == "DispatchAsync");
        Assert.Equal(typeof(Task<int>), dispatch.ReturnType);
        Assert.True(HasParameterTypes(dispatch, typeof(CliParseResult)));
    }

    [Fact]
    public void CliShared_DoesNotAddFriendAssembliesForTheNarrowing()
    {
        Assembly assembly = typeof(CliParseResult).Assembly;

        Assert.Empty(
            assembly.GetCustomAttributes<InternalsVisibleToAttribute>());
    }

    [Fact]
    public void CurrentBaseline_RecordsFourteenAssemblies444TypesAnd5Candidates()
    {
        using JsonDocument current = ReadJson(
            "docs",
            "governance",
            "bukit-core-public-api-baseline.v1.json");
        JsonElement root = current.RootElement;
        JsonElement[] types = root.GetProperty("types").EnumerateArray().ToArray();

        Assert.Equal(14, root.GetProperty("assemblies").GetArrayLength());
        Assert.Equal(444, types.Length);
        Assert.Equal(5, types.Count(entry =>
            entry.GetProperty("compatibility").GetString() ==
            "2.0-candidate"));

        foreach (string typeName in new[]
                 {
                     FactoryTypeName,
                     SimpleTypeName,
                     SubcommandTypeName
                 })
        {
            Assert.DoesNotContain(types, entry =>
                entry.GetProperty("assembly").GetString() ==
                "Bukit.Cli.Shared" &&
                entry.GetProperty("name").GetString() == typeName);
        }

        JsonElement parseResult = Assert.Single(types, entry =>
            entry.GetProperty("assembly").GetString() ==
            "Bukit.Cli.Shared" &&
            entry.GetProperty("name").GetString() == BaseTypeName);
        Assert.Equal(
            "cross-assembly-implementation",
            parseResult.GetProperty("classification").GetString());
        Assert.Equal(
            "1.x-do-not-narrow",
            parseResult.GetProperty("compatibility").GetString());
        Assert.Equal(
            "2.0-review",
            parseResult.GetProperty("migrationHorizon").GetString());
        Assert.Equal(
            "public abstract class Bukit.Cli.Shared.Cli.Parsing.CliParseResult : System.IEquatable<Bukit.Cli.Shared.Cli.Parsing.CliParseResult~>~",
            parseResult.GetProperty("signature").GetString());
        Assert.Equal(
            [
                "public Bukit.Cli.Shared.Cli.Binding.CliBoundCommand! BoundCommand { get; init; }",
                "public Bukit.Cli.Shared.Cli.Metadata.CliCommandSpec! Command { get; init; }",
                "public System.Boolean IsSuccess { get; }",
                "public System.Collections.Generic.IReadOnlyList<Bukit.Cli.Shared.Cli.Parsing.CliDiagnostic!>! Diagnostics { get; init; }",
                "public System.Void Deconstruct(out Bukit.Cli.Shared.Cli.Metadata.CliCommandSpec! Command, out Bukit.Cli.Shared.Cli.Binding.CliBoundCommand! BoundCommand, out System.Collections.Generic.IReadOnlyList<Bukit.Cli.Shared.Cli.Parsing.CliDiagnostic!>! Diagnostics)",
                "public abstract Bukit.Cli.Shared.Cli.Parsing.CliParseResult! <Clone>$()",
                "public static System.Boolean op_Equality(Bukit.Cli.Shared.Cli.Parsing.CliParseResult? left, Bukit.Cli.Shared.Cli.Parsing.CliParseResult? right)",
                "public static System.Boolean op_Inequality(Bukit.Cli.Shared.Cli.Parsing.CliParseResult? left, Bukit.Cli.Shared.Cli.Parsing.CliParseResult? right)",
                "public virtual System.Boolean Equals(Bukit.Cli.Shared.Cli.Parsing.CliParseResult? other)",
                "public virtual System.Boolean Equals(System.Object? obj)",
                "public virtual System.Int32 GetHashCode()",
                "public virtual System.String! ToString()"
            ],
            parseResult.GetProperty("publicMembers")
                .EnumerateArray()
                .Select(member => member.GetString()!)
                .ToArray());
        Assert.Equal(
            [
                "protected .ctor(Bukit.Cli.Shared.Cli.Metadata.CliCommandSpec! Command, Bukit.Cli.Shared.Cli.Binding.CliBoundCommand! BoundCommand, System.Collections.Generic.IReadOnlyList<Bukit.Cli.Shared.Cli.Parsing.CliDiagnostic!>! Diagnostics)",
                "protected .ctor(Bukit.Cli.Shared.Cli.Parsing.CliParseResult! original)",
                "protected virtual System.Boolean PrintMembers(System.Text.StringBuilder! builder)",
                "protected virtual System.Type! EqualityContract { get; }"
            ],
            parseResult.GetProperty("protectedMembers")
                .EnumerateArray()
                .Select(member => member.GetString()!)
                .ToArray());
    }

    [Fact]
    public void ClosedManifest_PreservesFourHistoricalCandidatesAndExactBlob()
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

        foreach (string typeName in new[]
                 {
                     FactoryTypeName,
                     BaseTypeName,
                     SimpleTypeName,
                     SubcommandTypeName
                 })
        {
            JsonElement candidate = Assert.Single(candidates, entry =>
                entry.GetProperty("assembly").GetString() ==
                "Bukit.Cli.Shared" &&
                entry.GetProperty("fullName").GetString() == typeName);
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
        }

        byte[] prefix = Encoding.UTF8.GetBytes($"blob {bytes.Length}\0");
        var blobBytes = new byte[prefix.Length + bytes.Length];
        prefix.CopyTo(blobBytes, 0);
        bytes.CopyTo(blobBytes, prefix.Length);

        Assert.Equal(
            CandidateManifestBlob,
            Convert.ToHexStringLower(SHA1.HashData(blobBytes)));
    }

    private static void AssertInitProperty(
        Type type,
        string name,
        Type propertyType)
    {
        PropertyInfo property = Assert.Single(
            type.GetProperties(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly),
            candidate => candidate.Name == name);

        Assert.Equal(propertyType, property.PropertyType);
        Assert.NotNull(property.GetMethod);
        Assert.True(property.GetMethod!.IsPublic);
        Assert.NotNull(property.SetMethod);
        Assert.True(property.SetMethod!.IsPublic);
        Assert.Contains(
            typeof(IsExternalInit),
            property.SetMethod.ReturnParameter.GetRequiredCustomModifiers());
    }

    private static Type GetType(Assembly assembly, string typeName)
        => assembly.GetType(
            typeName,
            throwOnError: true,
            ignoreCase: false)!;

    private static bool HasParameterTypes(
        MethodBase method,
        params Type[] parameterTypes)
        => method.GetParameters()
            .Select(parameter => parameter.ParameterType)
            .SequenceEqual(parameterTypes);

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
