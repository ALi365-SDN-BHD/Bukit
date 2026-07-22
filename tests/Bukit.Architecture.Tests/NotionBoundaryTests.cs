using System.Xml.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class NotionBoundaryTests
{
    [Fact]
    public void Notion_Project_MustExist_AndRemainBclOnly()
    {
        var repoRoot = FindRepoRoot();
        var projectPath = Path.Combine(
            repoRoot,
            "src",
            "Bukit-Core",
            "Bukit.Notion",
            "Bukit.Notion.csproj");

        Assert.True(File.Exists(projectPath), $"Missing Notion project: {projectPath}");

        var project = XDocument.Load(projectPath);
        Assert.Empty(project.Descendants("ProjectReference"));
        Assert.Empty(project.Descendants("PackageReference"));
    }

    [Fact]
    public void Shared_MayReferenceNotion_OnlyForOneXCompatibility()
    {
        var repoRoot = FindRepoRoot();
        var project = XDocument.Load(Path.Combine(
            repoRoot,
            "src",
            "Bukit-Core",
            "Bukit.Shared",
            "Bukit.Shared.csproj"));

        var references = project.Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension(
                reference.Attribute("Include")?.Value.Replace('\\', Path.DirectorySeparatorChar)) ?? string.Empty)
            .ToArray();

        Assert.Equal(["Bukit.Notion"], references);
    }

    [Fact]
    public void ContentNotion_Project_MustExist_WithExactAdapterDependencies()
    {
        var repoRoot = FindRepoRoot();
        var projectPath = Path.Combine(
            repoRoot,
            "src",
            "Bukit-Core",
            "Bukit.Content.Notion",
            "Bukit.Content.Notion.csproj");

        Assert.True(File.Exists(projectPath), $"Missing Notion content adapter project: {projectPath}");

        var project = XDocument.Load(projectPath);
        var references = project.Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension(
                reference.Attribute("Include")?.Value.Replace('\\', Path.DirectorySeparatorChar)) ?? string.Empty)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            ["Bukit.Config", "Bukit.Engine.Abstractions", "Bukit.Notion", "Bukit.Shared"],
            references);
        Assert.Empty(project.Descendants("PackageReference"));
    }

    [Fact]
    public void Content_MustReferenceContentNotionCompatibilityAdapter()
    {
        var repoRoot = FindRepoRoot();
        var project = XDocument.Load(Path.Combine(
            repoRoot,
            "src",
            "Bukit-Core",
            "Bukit.Content",
            "Bukit.Content.csproj"));

        var references = project.Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension(
                reference.Attribute("Include")?.Value.Replace('\\', Path.DirectorySeparatorChar)) ?? string.Empty)
            .ToArray();

        Assert.Contains("Bukit.Content.Notion", references);
    }

    [Fact]
    public void ProductionNotionHttpContract_MustBeOwnedByNotionAssembly()
    {
        var repoRoot = FindRepoRoot();
        var coreRoot = Path.Combine(repoRoot, "src", "Bukit-Core");
        var notionRoot = Path.Combine(coreRoot, "Bukit.Notion") + Path.DirectorySeparatorChar;
        var forbidden = new[]
        {
            "api.notion.com/v1",
            "\"Notion-Version\"",
            "AuthenticationHeaderValue(\"Bearer\"",
            ".Headers.Authorization"
        };

        var violations = Directory.EnumerateFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.StartsWith(notionRoot, StringComparison.Ordinal))
            .Where(path => !IsBuildOutput(path))
            .SelectMany(path => forbidden
                .Where(token => File.ReadAllText(path).Contains(token, StringComparison.Ordinal))
                .Select(token => $"{Path.GetRelativePath(repoRoot, path)}: {token}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void NewNotionProjects_MustNotUseReflectionBasedJsonSerialization()
    {
        var repoRoot = FindRepoRoot();
        var reflectionSerializer = new Regex(
            @"JsonSerializer\s*\.\s*(?:Serialize|Deserialize)(?:Async)?\s*(?:<|\()",
            RegexOptions.CultureInvariant);
        var roots = new[]
        {
            Path.Combine(repoRoot, "src", "Bukit-Core", "Bukit.Notion"),
            Path.Combine(repoRoot, "src", "Bukit-Core", "Bukit.Content.Notion")
        };

        var violations = roots
            .SelectMany(root => Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            .Where(path => !IsBuildOutput(path))
            .Where(path => reflectionSerializer.IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(repoRoot, path))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void LegacyNotionTypes_MustResolveFromOriginalAssemblies()
    {
        var contentAssembly = typeof(Bukit.Content.Notion.NotionContentProvider).Assembly;
        var sharedAssembly = typeof(Bukit.Shared.Notion.NotionBlock).Assembly;

        Assert.Equal("Bukit.Content", contentAssembly.GetName().Name);
        Assert.Equal("Bukit.Shared", sharedAssembly.GetName().Name);

        AssertTypesResolve(contentAssembly, LegacyContentNotionTypes);
        AssertTypesResolve(sharedAssembly, LegacySharedNotionTypes);
    }

    private static void AssertTypesResolve(System.Reflection.Assembly assembly, IEnumerable<string> typeNames)
    {
        foreach (var typeName in typeNames)
        {
            Assert.NotNull(assembly.GetType(typeName, throwOnError: false, ignoreCase: false));
            Assert.NotNull(Type.GetType($"{typeName}, {assembly.GetName().Name}", throwOnError: false, ignoreCase: false));
        }
    }

    private static bool IsBuildOutput(string path)
    {
        var separator = Path.DirectorySeparatorChar;
        return path.Contains($"{separator}bin{separator}", StringComparison.Ordinal) ||
               path.Contains($"{separator}obj{separator}", StringComparison.Ordinal);
    }

    private static readonly string[] LegacyContentNotionTypes =
    [
        "Bukit.Content.Notion.BlockRenderers.AudioBlockRenderer",
        "Bukit.Content.Notion.BlockRenderers.BookmarkBlockRenderer",
        "Bukit.Content.Notion.BlockRenderers.CalloutBlockRenderer",
        "Bukit.Content.Notion.BlockRenderers.ChildEntityBlockRenderer",
        "Bukit.Content.Notion.BlockRenderers.CodeBlockRenderer",
        "Bukit.Content.Notion.BlockRenderers.ColumnBlockRenderer",
        "Bukit.Content.Notion.BlockRenderers.ColumnListBlockRenderer",
        "Bukit.Content.Notion.BlockRenderers.DividerBlockRenderer",
        "Bukit.Content.Notion.BlockRenderers.EmbedBlockRenderer",
        "Bukit.Content.Notion.BlockRenderers.EquationBlockRenderer",
        "Bukit.Content.Notion.BlockRenderers.FileBlockRenderer",
        "Bukit.Content.Notion.BlockRenderers.ImageBlockRenderer",
        "Bukit.Content.Notion.BlockRenderers.LinkPreviewBlockRenderer",
        "Bukit.Content.Notion.BlockRenderers.LinkToPageBlockRenderer",
        "Bukit.Content.Notion.BlockRenderers.NoOpBlockRenderer",
        "Bukit.Content.Notion.BlockRenderers.PdfBlockRenderer",
        "Bukit.Content.Notion.BlockRenderers.RichTextContainerRenderer",
        "Bukit.Content.Notion.BlockRenderers.SyncedBlockRenderer",
        "Bukit.Content.Notion.BlockRenderers.TableBlockRenderer",
        "Bukit.Content.Notion.BlockRenderers.TableOfContentsBlockRenderer",
        "Bukit.Content.Notion.BlockRenderers.ToDoBlockRenderer",
        "Bukit.Content.Notion.BlockRenderers.ToggleBlockRenderer",
        "Bukit.Content.Notion.BlockRenderers.VideoBlockRenderer",
        "Bukit.Content.Notion.INotionBlockRenderer",
        "Bukit.Content.Notion.NotionApiClient",
        "Bukit.Content.Notion.NotionBlockRendererRegistry",
        "Bukit.Content.Notion.NotionBlockTransformer",
        "Bukit.Content.Notion.NotionBlocksRenderer",
        "Bukit.Content.Notion.NotionClientStats",
        "Bukit.Content.Notion.NotionColorPalette",
        "Bukit.Content.Notion.NotionContentProvider",
        "Bukit.Content.Notion.NotionPropertyParser",
        "Bukit.Content.Notion.NotionProviderOptions",
        "Bukit.Content.Notion.NotionRenderContext",
        "Bukit.Content.Notion.NotionRichTextRenderer"
    ];

    private static readonly string[] LegacySharedNotionTypes =
    [
        "Bukit.Shared.Notion.BulletedListItemBlock",
        "Bukit.Shared.Notion.CalloutBlock",
        "Bukit.Shared.Notion.CodeBlock",
        "Bukit.Shared.Notion.Heading1Block",
        "Bukit.Shared.Notion.Heading2Block",
        "Bukit.Shared.Notion.Heading3Block",
        "Bukit.Shared.Notion.HtmlToNotionBlockConverter",
        "Bukit.Shared.Notion.HtmlTokenizer",
        "Bukit.Shared.Notion.HtmlTokenizer+HtmlToken",
        "Bukit.Shared.Notion.HtmlTokenizer+HtmlTokenType",
        "Bukit.Shared.Notion.ImageBlock",
        "Bukit.Shared.Notion.NotionApiUrls",
        "Bukit.Shared.Notion.NotionBlock",
        "Bukit.Shared.Notion.NumberedListItemBlock",
        "Bukit.Shared.Notion.ParagraphBlock",
        "Bukit.Shared.Notion.QuoteBlock",
        "Bukit.Shared.Notion.RichTextSegment",
        "Bukit.Shared.Notion.ToggleBlock"
    ];

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "bukit-core.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Bukit repository root.");
    }
}
