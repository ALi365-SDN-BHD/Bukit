using System.Reflection;
using System.Xml.Linq;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class Ad03C3SharedNotionConversionGraphRemovalTests
{
    private const string LegacyMapperTypeName =
        "Bukit.Shared.Notion.NotionCompatibilityMapper";
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string[] LegacyPublicTypeNames =
    [
        "Bukit.Shared.Notion.BulletedListItemBlock",
        "Bukit.Shared.Notion.CalloutBlock",
        "Bukit.Shared.Notion.CodeBlock",
        "Bukit.Shared.Notion.Heading1Block",
        "Bukit.Shared.Notion.Heading2Block",
        "Bukit.Shared.Notion.Heading3Block",
        "Bukit.Shared.Notion.HtmlToNotionBlockConverter",
        "Bukit.Shared.Notion.ImageBlock",
        "Bukit.Shared.Notion.NotionBlock",
        "Bukit.Shared.Notion.NumberedListItemBlock",
        "Bukit.Shared.Notion.ParagraphBlock",
        "Bukit.Shared.Notion.QuoteBlock",
        "Bukit.Shared.Notion.RichTextSegment",
        "Bukit.Shared.Notion.ToggleBlock"
    ];
    private static readonly string[] CanonicalPublicTypeNames =
    [
        "Bukit.Notion.Blocks.BulletedListItemBlock",
        "Bukit.Notion.Blocks.CalloutBlock",
        "Bukit.Notion.Blocks.CodeBlock",
        "Bukit.Notion.Blocks.Heading1Block",
        "Bukit.Notion.Blocks.Heading2Block",
        "Bukit.Notion.Blocks.Heading3Block",
        "Bukit.Notion.Conversion.HtmlToNotionBlockConverter",
        "Bukit.Notion.Blocks.ImageBlock",
        "Bukit.Notion.Blocks.NotionBlock",
        "Bukit.Notion.Blocks.NumberedListItemBlock",
        "Bukit.Notion.Blocks.ParagraphBlock",
        "Bukit.Notion.Blocks.QuoteBlock",
        "Bukit.Notion.Blocks.RichTextSegment",
        "Bukit.Notion.Blocks.ToggleBlock"
    ];

    [Fact]
    public void LegacySharedConversionGraph_IsAbsentAndCanonicalOwnerRemainsPublic()
    {
        Assembly sharedAssembly = typeof(Bukit.Shared.BukitException).Assembly;
        Assembly notionAssembly =
            typeof(Bukit.Notion.Conversion.HtmlToNotionBlockConverter).Assembly;
        string[] sharedExports = sharedAssembly.GetExportedTypes()
            .Select(static type => type.FullName!)
            .ToArray();
        string[] notionExports = notionAssembly.GetExportedTypes()
            .Select(static type => type.FullName!)
            .ToArray();

        foreach (string typeName in LegacyPublicTypeNames)
        {
            Assert.Null(sharedAssembly.GetType(
                typeName,
                throwOnError: false,
                ignoreCase: false));
            Assert.DoesNotContain(typeName, sharedExports);
        }

        Assert.Null(sharedAssembly.GetType(
            LegacyMapperTypeName,
            throwOnError: false,
            ignoreCase: false));

        foreach (string sourceFileName in new[]
                 {
                     "HtmlToNotionBlockConverter.cs",
                     "NotionBlockTypes.cs",
                     "NotionCompatibilityMapper.cs"
                 })
        {
            Assert.False(File.Exists(Path.Combine(
                RepoRoot,
                "src",
                "Bukit-Core",
                "Bukit.Shared",
                "Notion",
                sourceFileName)));
        }

        XDocument sharedProject = XDocument.Load(Path.Combine(
            RepoRoot,
            "src",
            "Bukit-Core",
            "Bukit.Shared",
            "Bukit.Shared.csproj"));
        Assert.Empty(sharedProject.Descendants("ProjectReference"));

        foreach (string typeName in CanonicalPublicTypeNames)
        {
            Type type = notionAssembly.GetType(
                typeName,
                throwOnError: true,
                ignoreCase: false)!;

            Assert.True(type.IsPublic || type.IsNestedPublic);
            Assert.Contains(typeName, notionExports);
            Assert.Equal("Bukit.Notion", type.Assembly.GetName().Name);
        }
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

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
