using System.Reflection;
using System.Text.Json;
using Bukit.Engine.Abstractions.Content;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class Ad03C5NotionPropertyParserRetentionTests
{
    private const string ParserTypeName =
        "Bukit.Content.Notion.NotionPropertyParser";
    private const string DecisionLedgerPath =
        "docs/analysis/bukit-core-ad03c5-notion-property-parser-retention-decision-2026-07-24.zh-CN.md";
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void LegacyParser_RemainsTheExactPublicBukitContentFacade()
    {
        Type parser = typeof(Bukit.Content.Notion.NotionPropertyParser);

        Assert.Equal("Bukit.Content", parser.Assembly.GetName().Name);
        Assert.Equal(ParserTypeName, parser.FullName);
        Assert.True(parser.IsPublic);
        Assert.True(parser.IsAbstract);
        Assert.True(parser.IsSealed);

        MethodInfo[] methods = parser
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .OrderBy(method => method.Name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["ExtractAllFields", "ExtractFields"], methods.Select(method => method.Name));
        Assert.All(methods, method =>
        {
            ParameterInfo parameter = Assert.Single(method.GetParameters());
            Assert.Equal("properties", parameter.Name);
            Assert.Equal(typeof(JsonElement), parameter.ParameterType);
            Assert.Equal(
                typeof(IReadOnlyDictionary<string, ContentField>),
                method.ReturnType);
        });

        MemberInfo[] publicDeclaredMembers = parser
            .GetMembers(
                BindingFlags.Public |
                BindingFlags.Static |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly)
            .OrderBy(member => member.Name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(methods, publicDeclaredMembers);
    }

    [Fact]
    public void CanonicalAdapter_KeepsParserImplementationsInternalWithoutPublicReplacement()
    {
        Assembly adapterAssembly =
            typeof(Bukit.Content.Notion.NotionContentSource).Assembly;

        Assert.Equal("Bukit.Content.Notion", adapterAssembly.GetName().Name);
        Assert.All(
            new[]
            {
                "Bukit.Content.Notion.NotionContentPropertyParser",
                "Bukit.Content.Notion.NotionPropertyTypeParser"
            },
            typeName =>
            {
                Type implementation = adapterAssembly.GetType(
                    typeName,
                    throwOnError: true,
                    ignoreCase: false)!;
                Assert.True(implementation.IsNotPublic);
                Assert.DoesNotContain(implementation, adapterAssembly.GetExportedTypes());
            });

        Assert.DoesNotContain(
            adapterAssembly.GetExportedTypes(),
            type =>
                string.Equals(
                    type.Namespace,
                    "Bukit.Content.Notion",
                    StringComparison.Ordinal) &&
                type.Name.Contains("PropertyParser", StringComparison.Ordinal));
    }

    [Fact]
    public void GovernedBaseline_ClassifiesTheRetainedParserAsImplementationPublic()
    {
        using JsonDocument baseline = ReadJson(
            "docs",
            "governance",
            "bukit-core-public-api-baseline.v1.json");
        JsonElement parser = Assert.Single(
            baseline.RootElement.GetProperty("types").EnumerateArray(),
            entry =>
                entry.GetProperty("assembly").GetString() == "Bukit.Content" &&
                entry.GetProperty("name").GetString() == ParserTypeName);

        Assert.Equal(
            "implementation-public",
            parser.GetProperty("classification").GetString());
        Assert.Equal(
            "1.x-do-not-narrow",
            parser.GetProperty("compatibility").GetString());
        Assert.Equal(
            "2.0-review",
            parser.GetProperty("migrationHorizon").GetString());
    }

    [Fact]
    public void ActiveGovernanceAndDecisionLedger_RecordRetentionAndReviewTriggers()
    {
        string governance = File.ReadAllText(Path.Combine(
            RepoRoot,
            "guide",
            "dev",
            "public-api-governance.md"));
        string ledger = File.ReadAllText(Path.Combine(RepoRoot, DecisionLedgerPath));

        Assert.Contains(
            "### AD-03C5 Notion Property Parser Retention",
            governance,
            StringComparison.Ordinal);
        Assert.Contains("retain-by-design", governance, StringComparison.Ordinal);
        Assert.Contains("no public canonical replacement", governance, StringComparison.Ordinal);
        Assert.Contains("security or correctness defect", governance, StringComparison.Ordinal);
        Assert.Contains("direct consumer declaration", governance, StringComparison.Ordinal);
        Assert.Contains("CLR SDK productization", governance, StringComparison.Ordinal);

        Assert.Contains("# Bukit Core AD-03C5", ledger, StringComparison.Ordinal);
        Assert.Contains("retain-by-design", ledger, StringComparison.Ordinal);
        Assert.Contains("no public canonical replacement", ledger, StringComparison.Ordinal);
        Assert.Contains("private", ledger, StringComparison.Ordinal);
        Assert.Contains("binary-only", ledger, StringComparison.Ordinal);
        Assert.Contains("reflection", ledger, StringComparison.Ordinal);
        Assert.Contains("security or correctness defect", ledger, StringComparison.Ordinal);
        Assert.Contains("direct consumer declaration", ledger, StringComparison.Ordinal);
        Assert.Contains("CLR SDK productization", ledger, StringComparison.Ordinal);
        Assert.Contains("migration and versioning plan", ledger, StringComparison.Ordinal);
    }

    private static JsonDocument ReadJson(params string[] relativeSegments)
        => JsonDocument.Parse(File.ReadAllText(
            Path.Combine([RepoRoot, .. relativeSegments])));

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
