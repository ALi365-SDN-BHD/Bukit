using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Legacy = Bukit.Shared.Notion;
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

    [Fact]
    public void LegacyTokenizerTriplet_IsAbsentAndCanonicalTripletRemainsPublic()
    {
        Assembly sharedAssembly = typeof(Legacy.NotionBlock).Assembly;
        Assembly notionAssembly =
            typeof(Bukit.Notion.Conversion.HtmlTokenizer).Assembly;
        string[] sharedExports = sharedAssembly.GetExportedTypes()
            .Select(type => type.FullName!)
            .ToArray();
        string[] notionExports = notionAssembly.GetExportedTypes()
            .Select(type => type.FullName!)
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
    public void LegacyModelGraph_RemainsPublicExportedAndComplete()
    {
        Assembly assembly = typeof(Legacy.NotionBlock).Assembly;
        Type[] exportedTypes = assembly.GetExportedTypes();

        foreach (Type type in LegacyModelTypes)
        {
            Assert.True(type.IsPublic);
            Assert.Contains(type, exportedTypes);
        }

        Assert.True(typeof(Legacy.NotionBlock).IsAbstract);
        foreach (Type type in LegacyDerivedBlockTypes)
        {
            Assert.Equal(typeof(Legacy.NotionBlock), type.BaseType);
            Assert.True(typeof(Legacy.NotionBlock).IsAssignableFrom(type));
        }

        Assert.False(typeof(Legacy.NotionBlock)
            .IsAssignableFrom(typeof(Legacy.RichTextSegment)));
    }

    [Fact]
    public void RetainedConverter_PublicConvertStillReturnsExactLegacyModelGraph()
    {
        Type converter = typeof(Legacy.HtmlToNotionBlockConverter);
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

        Assert.Equal(typeof(List<Legacy.NotionBlock>), convert.ReturnType);
        Assert.Equal(
            [typeof(string)],
            convert.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray());
        Assert.Equal(typeof(string), toBlocksJson.ReturnType);
        Assert.Equal(
            [typeof(string)],
            toBlocksJson.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray());
    }

    [Fact]
    public void LegacyRecords_PreserveDefaultsDeconstructionAndListReferenceEquality()
    {
        var richText = new Legacy.RichTextSegment("text");
        var (text, bold, italic, linkUrl) = richText;
        Assert.Equal("text", text);
        Assert.False(bold);
        Assert.False(italic);
        Assert.Null(linkUrl);

        var image = new Legacy.ImageBlock("https://example.com/image.png");
        var (imageUrl, caption) = image;
        Assert.Equal("https://example.com/image.png", imageUrl);
        Assert.Null(caption);

        var code = new Legacy.CodeBlock("Console.WriteLine();");
        var (codeText, language) = code;
        Assert.Equal("Console.WriteLine();", codeText);
        Assert.Equal("plain text", language);

        var callout = new Legacy.CalloutBlock("Notice");
        var (calloutText, icon) = callout;
        Assert.Equal("Notice", calloutText);
        Assert.Equal("📝", icon);

        var firstSegments = new List<Legacy.RichTextSegment>
        {
            new("same")
        };
        var secondSegments = new List<Legacy.RichTextSegment>
        {
            new("same")
        };
        var first = new Legacy.ParagraphBlock(firstSegments);
        var second = new Legacy.ParagraphBlock(secondSegments);
        first.Deconstruct(out List<Legacy.RichTextSegment> deconstructedSegments);

        Assert.Same(firstSegments, deconstructedSegments);
        Assert.NotSame(firstSegments, secondSegments);
        Assert.NotEqual(first, second);
        Assert.Equivalent(first, second, strict: true);
    }

    [Fact]
    public void CurrentBaseline_RecordsFourteenAssemblies480TypesAnd49Candidates()
    {
        using JsonDocument current = ReadJson(
            "docs",
            "governance",
            "bukit-core-public-api-baseline.v1.json");
        JsonElement root = current.RootElement;
        JsonElement[] types = root.GetProperty("types").EnumerateArray().ToArray();

        Assert.Equal(14, root.GetProperty("assemblies").GetArrayLength());
        Assert.Equal(480, types.Length);
        Assert.Equal(49, types.Count(entry =>
            entry.GetProperty("compatibility").GetString() ==
            "2.0-candidate"));

        foreach (string typeName in LegacyTokenizerTypeNames)
        {
            Assert.DoesNotContain(types, entry =>
                entry.GetProperty("assembly").GetString() == "Bukit.Shared" &&
                entry.GetProperty("name").GetString() == typeName);
        }

        foreach (Type type in LegacyModelTypes)
        {
            JsonElement entry = Assert.Single(types, candidate =>
                candidate.GetProperty("assembly").GetString() ==
                "Bukit.Shared" &&
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

        JsonElement converter = Assert.Single(types, entry =>
            entry.GetProperty("assembly").GetString() == "Bukit.Shared" &&
            entry.GetProperty("name").GetString() ==
            "Bukit.Shared.Notion.HtmlToNotionBlockConverter");
        Assert.Equal(
            "cross-assembly-implementation",
            converter.GetProperty("classification").GetString());
        Assert.Equal(
            "1.x-do-not-narrow",
            converter.GetProperty("compatibility").GetString());
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

    private static Type[] LegacyModelTypes =>
    [
        typeof(Legacy.NotionBlock),
        .. LegacyDerivedBlockTypes,
        typeof(Legacy.RichTextSegment)
    ];

    private static Type[] LegacyDerivedBlockTypes =>
    [
        typeof(Legacy.Heading1Block),
        typeof(Legacy.Heading2Block),
        typeof(Legacy.Heading3Block),
        typeof(Legacy.ParagraphBlock),
        typeof(Legacy.BulletedListItemBlock),
        typeof(Legacy.NumberedListItemBlock),
        typeof(Legacy.QuoteBlock),
        typeof(Legacy.ImageBlock),
        typeof(Legacy.ToggleBlock),
        typeof(Legacy.CodeBlock),
        typeof(Legacy.CalloutBlock)
    ];

    private static string[] HistoricalCandidateTypeNames =>
    [
        .. LegacyModelTypes.Select(type => type.FullName!),
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
