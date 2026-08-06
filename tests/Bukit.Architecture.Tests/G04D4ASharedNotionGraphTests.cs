using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Canonical = Bukit.Notion.Blocks;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class G04D4ASharedNotionGraphTests
{
    private const string CandidateManifestBlob =
        "7b07d6890562387010b52301e9f8716e9bf10ed1";
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string[] LegacyTokenizerTypeNames =
    [
        "Bukit.Shared.Notion.HtmlTokenizer",
        "Bukit.Shared.Notion.HtmlTokenizer+HtmlToken",
        "Bukit.Shared.Notion.HtmlTokenizer+HtmlTokenType"
    ];
    private static readonly string[] CanonicalTokenizerTypeNames =
    [
        "Bukit.Notion.Conversion.HtmlTokenizer",
        "Bukit.Notion.Conversion.HtmlTokenizer+HtmlToken",
        "Bukit.Notion.Conversion.HtmlTokenizer+HtmlTokenType"
    ];
    private static readonly string[] LegacyModelTypeNames =
    [
        "Bukit.Shared.Notion.NotionBlock",
        "Bukit.Shared.Notion.Heading1Block",
        "Bukit.Shared.Notion.Heading2Block",
        "Bukit.Shared.Notion.Heading3Block",
        "Bukit.Shared.Notion.ParagraphBlock",
        "Bukit.Shared.Notion.BulletedListItemBlock",
        "Bukit.Shared.Notion.NumberedListItemBlock",
        "Bukit.Shared.Notion.QuoteBlock",
        "Bukit.Shared.Notion.ImageBlock",
        "Bukit.Shared.Notion.ToggleBlock",
        "Bukit.Shared.Notion.CodeBlock",
        "Bukit.Shared.Notion.CalloutBlock",
        "Bukit.Shared.Notion.RichTextSegment"
    ];

    [Fact]
    public void LegacyTokenizerTriplet_IsAbsentAndCanonicalTripletRemainsPublic()
    {
        Assembly sharedAssembly = typeof(Bukit.Shared.BukitException).Assembly;
        Assembly notionAssembly =
            typeof(Bukit.Notion.Conversion.HtmlTokenizer).Assembly;
        string[] sharedExports = sharedAssembly.GetExportedTypes()
            .Select(static type => type.FullName!)
            .ToArray();
        string[] notionExports = notionAssembly.GetExportedTypes()
            .Select(static type => type.FullName!)
            .ToArray();

        foreach (string typeName in LegacyTokenizerTypeNames)
        {
            Assert.Null(sharedAssembly.GetType(
                typeName,
                throwOnError: false,
                ignoreCase: false));
            Assert.DoesNotContain(typeName, sharedExports);
        }

        foreach (string typeName in CanonicalTokenizerTypeNames)
        {
            Type type = notionAssembly.GetType(
                typeName,
                throwOnError: true,
                ignoreCase: false)!;

            Assert.True(type.IsPublic || type.IsNestedPublic);
            Assert.Contains(typeName, notionExports);
        }
    }

    [Fact]
    public void LegacyModelGraph_IsAbsentAndCanonicalModelGraphRemainsComplete()
    {
        Assembly sharedAssembly = typeof(Bukit.Shared.BukitException).Assembly;
        string[] sharedExports = sharedAssembly.GetExportedTypes()
            .Select(static type => type.FullName!)
            .ToArray();
        Type[] notionExports = typeof(Canonical.NotionBlock).Assembly.GetExportedTypes();

        foreach (string typeName in LegacyModelTypeNames)
        {
            Assert.Null(sharedAssembly.GetType(
                typeName,
                throwOnError: false,
                ignoreCase: false));
            Assert.DoesNotContain(typeName, sharedExports);
        }

        Assert.True(typeof(Canonical.NotionBlock).IsAbstract);
        foreach (Type type in CanonicalModelTypes)
        {
            Assert.True(type.IsPublic);
            Assert.Contains(type, notionExports);
        }

        foreach (Type type in CanonicalDerivedBlockTypes)
        {
            Assert.Equal(typeof(Canonical.NotionBlock), type.BaseType);
            Assert.True(typeof(Canonical.NotionBlock).IsAssignableFrom(type));
        }

        Assert.False(typeof(Canonical.NotionBlock)
            .IsAssignableFrom(typeof(Canonical.RichTextSegment)));
    }

    [Fact]
    public void CanonicalConverter_PublicMethodsReturnExactCanonicalModelGraph()
    {
        Type converter =
            typeof(Bukit.Notion.Conversion.HtmlToNotionBlockConverter);
        MethodInfo convert = Assert.Single(
            converter.GetMethods(
                BindingFlags.Public |
                BindingFlags.Static |
                BindingFlags.DeclaredOnly),
            method => method.Name == "Convert");
        MethodInfo toBlocksJson = Assert.Single(
            converter.GetMethods(
                BindingFlags.Public |
                BindingFlags.Static |
                BindingFlags.DeclaredOnly),
            method => method.Name == "ToBlocksJson");

        Assert.Equal(typeof(List<Canonical.NotionBlock>), convert.ReturnType);
        Assert.Equal(
            [typeof(string)],
            convert.GetParameters()
                .Select(static parameter => parameter.ParameterType)
                .ToArray());
        Assert.Equal(typeof(string), toBlocksJson.ReturnType);
        Assert.Equal(
            [typeof(string)],
            toBlocksJson.GetParameters()
                .Select(static parameter => parameter.ParameterType)
                .ToArray());
    }

    [Fact]
    public void CanonicalRecords_PreserveDefaultsDeconstructionAndListReferenceEquality()
    {
        var richText = new Canonical.RichTextSegment("text");
        var (text, bold, italic, linkUrl) = richText;
        Assert.Equal("text", text);
        Assert.False(bold);
        Assert.False(italic);
        Assert.Null(linkUrl);

        var image = new Canonical.ImageBlock("https://example.com/image.png");
        var (imageUrl, caption) = image;
        Assert.Equal("https://example.com/image.png", imageUrl);
        Assert.Null(caption);

        var code = new Canonical.CodeBlock("Console.WriteLine();");
        var (codeText, language) = code;
        Assert.Equal("Console.WriteLine();", codeText);
        Assert.Equal("plain text", language);

        var callout = new Canonical.CalloutBlock("Notice");
        var (calloutText, icon) = callout;
        Assert.Equal("Notice", calloutText);
        Assert.Equal("📝", icon);

        var firstSegments = new List<Canonical.RichTextSegment>
        {
            new("same")
        };
        var secondSegments = new List<Canonical.RichTextSegment>
        {
            new("same")
        };
        var first = new Canonical.ParagraphBlock(firstSegments);
        var second = new Canonical.ParagraphBlock(secondSegments);
        first.Deconstruct(
            out List<Canonical.RichTextSegment> deconstructedSegments);

        Assert.Same(firstSegments, deconstructedSegments);
        Assert.NotSame(firstSegments, secondSegments);
        Assert.NotEqual(first, second);
        Assert.Equivalent(first, second, strict: true);
    }

    [Fact]
    public void FinalBaseline_RecordsFourteenAssemblies427TypesAndNoSharedLegacyNotionEntries()
    {
        using JsonDocument current = ReadJson(
            "docs",
            "governance",
            "bukit-core-public-api-baseline.v1.json");
        JsonElement root = current.RootElement;
        JsonElement[] types = root.GetProperty("types").EnumerateArray().ToArray();

        Assert.Equal(14, root.GetProperty("assemblies").GetArrayLength());
        Assert.Equal(427, types.Length);
        Assert.Equal(0, types.Count(entry =>
            entry.GetProperty("compatibility").GetString() ==
            "2.0-candidate"));

        foreach (string typeName in LegacyTokenizerTypeNames)
        {
            Assert.DoesNotContain(types, entry =>
                entry.GetProperty("assembly").GetString() == "Bukit.Shared" &&
                entry.GetProperty("name").GetString() == typeName);
        }

        foreach (string typeName in LegacyModelTypeNames)
        {
            Assert.DoesNotContain(types, entry =>
                entry.GetProperty("assembly").GetString() == "Bukit.Shared" &&
                entry.GetProperty("name").GetString() == typeName);
        }

        Assert.All(
            new[]
            {
                "Bukit.Shared.Notion.HtmlToNotionBlockConverter",
                "Bukit.Shared.Notion.NotionApiUrls"
            },
            typeName =>
                Assert.DoesNotContain(types, entry =>
                    entry.GetProperty("assembly").GetString() ==
                    "Bukit.Shared" &&
                    entry.GetProperty("name").GetString() == typeName));
    }

    [Fact]
    public void ClosedManifest_PreservesAllSixteenHistoricalCandidatesAndExactBlob()
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

        foreach (string typeName in HistoricalCandidateTypeNames)
        {
            JsonElement candidate = Assert.Single(candidates, entry =>
                entry.GetProperty("assembly").GetString() == "Bukit.Shared" &&
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

    private static Type[] CanonicalModelTypes =>
    [
        typeof(Canonical.NotionBlock),
        .. CanonicalDerivedBlockTypes,
        typeof(Canonical.RichTextSegment)
    ];

    private static Type[] CanonicalDerivedBlockTypes =>
    [
        typeof(Canonical.Heading1Block),
        typeof(Canonical.Heading2Block),
        typeof(Canonical.Heading3Block),
        typeof(Canonical.ParagraphBlock),
        typeof(Canonical.BulletedListItemBlock),
        typeof(Canonical.NumberedListItemBlock),
        typeof(Canonical.QuoteBlock),
        typeof(Canonical.ImageBlock),
        typeof(Canonical.ToggleBlock),
        typeof(Canonical.CodeBlock),
        typeof(Canonical.CalloutBlock)
    ];

    private static string[] HistoricalCandidateTypeNames =>
    [
        .. LegacyModelTypeNames,
        .. LegacyTokenizerTypeNames
    ];

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
