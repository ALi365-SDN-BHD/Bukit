using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class G04D1CM2AtomicRemovalTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static readonly string[] RemovedLegacyTypes =
    [
        "Bukit.Content.Notion.INotionBlockRenderer",
        "Bukit.Content.Notion.NotionBlockTransformer",
        "Bukit.Content.Notion.NotionBlockRendererRegistry",
        "Bukit.Content.Notion.NotionRenderContext",
        "Bukit.Content.Notion.NotionBlocksRenderer"
    ];

    private static readonly string[] CanonicalReplacementTypes =
    [
        "Bukit.Notion.Rendering.INotionBlockRenderer",
        "Bukit.Notion.Rendering.NotionBlockTransformer",
        "Bukit.Notion.Rendering.NotionBlockRendererRegistry",
        "Bukit.Notion.Rendering.NotionRenderContext",
        "Bukit.Notion.Rendering.NotionBlocksRenderer"
    ];

    [Fact]
    public void BukitContent_DoesNotExposeApprovedLegacyExtensionGraph()
    {
        var contentAssembly = typeof(Bukit.Content.Notion.NotionApiClient).Assembly;

        Assert.All(RemovedLegacyTypes, name =>
            Assert.Null(contentAssembly.GetType(name, throwOnError: false, ignoreCase: false)));
    }

    [Fact]
    public void LegacyCompatibilitySourceFiles_AreRemovedAsOneBatch()
    {
        string[] files =
        [
            "INotionBlockRenderer.cs",
            "NotionBlockRendererRegistry.cs",
            "NotionRenderContext.cs",
            "NotionBlocksRenderer.cs"
        ];
        var directory = Path.Combine(
            RepoRoot,
            "src",
            "Bukit-Core",
            "Bukit.Content",
            "Notion");

        Assert.All(files, file => Assert.False(
            File.Exists(Path.Combine(directory, file)),
            $"Approved M2 compatibility source still exists: {file}"));
    }

    [Fact]
    public void CanonicalReplacements_RemainPublicInBukitNotion()
    {
        var notionAssembly = typeof(Bukit.Notion.Transport.NotionClient).Assembly;

        Assert.Equal("Bukit.Notion", notionAssembly.GetName().Name);
        Assert.All(CanonicalReplacementTypes, name =>
        {
            var type = notionAssembly.GetType(name, throwOnError: false, ignoreCase: false);

            Assert.NotNull(type);
            Assert.True(type.IsPublic, $"Canonical replacement is not public: {name}");
        });
    }

    [Fact]
    public void ExplicitlyExcludedLegacyTypes_RemainPublicWithExactIdentities()
    {
        var contentAssembly = typeof(Bukit.Content.Notion.NotionApiClient).Assembly;
        string[] retainedTypes =
        [
            "Bukit.Content.Notion.NotionApiClient",
            "Bukit.Content.Notion.NotionProviderOptions",
            "Bukit.Content.Notion.NotionClientStats"
        ];

        Assert.All(retainedTypes, name =>
        {
            var type = contentAssembly.GetType(name, throwOnError: false, ignoreCase: false);

            Assert.NotNull(type);
            Assert.True(type.IsPublic, $"Explicitly excluded type is not public: {name}");
        });
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

        throw new DirectoryNotFoundException("Could not locate the Bukit repository root.");
    }
}
