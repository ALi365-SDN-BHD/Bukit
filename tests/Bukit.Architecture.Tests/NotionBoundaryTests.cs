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
    public void CanonicalNotionProjects_MustRemainNonPackableMonorepoComponents()
    {
        var repoRoot = FindRepoRoot();
        var coreRoot = Path.Combine(repoRoot, "src", "Bukit-Core");
        var projectNames = new[] { "Bukit.Notion", "Bukit.Content.Notion" };

        foreach (var projectName in projectNames)
        {
            var project = XDocument.Load(Path.Combine(
                coreRoot,
                projectName,
                $"{projectName}.csproj"));

            Assert.Collection(
                project.Descendants("IsPackable"),
                element => Assert.Equal("false", element.Value));
            Assert.Empty(project.Descendants("PackageId"));
            Assert.Empty(project.Descendants("GeneratePackageOnBuild"));
        }

        var governance = File.ReadAllText(Path.Combine(
            repoRoot,
            "guide",
            "dev",
            "public-api-governance.md"));
        Assert.Contains("## Notion Assembly Distribution Boundary", governance, StringComparison.Ordinal);
        Assert.Contains("monorepo Core components", governance, StringComparison.Ordinal);
        Assert.Contains("not supported NuGet SDKs", governance, StringComparison.Ordinal);
        Assert.Contains("`1.x-do-not-narrow`", governance, StringComparison.Ordinal);
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
    public void ContentNotion_MustUseCanonicalNotionEndpointOwner()
    {
        var repoRoot = FindRepoRoot();
        var root = Path.Combine(repoRoot, "src", "Bukit-Core", "Bukit.Content.Notion");
        var forbidden = new[]
        {
            "using Bukit.Shared.Notion;",
            "Bukit.Shared.Notion.NotionApiUrls"
        };

        var violations = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .SelectMany(path => forbidden
                .Where(token => File.ReadAllText(path).Contains(token, StringComparison.Ordinal))
                .Select(token => $"{Path.GetRelativePath(repoRoot, path)}: {token}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
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
    public void Engine_MustUseContentCompatibilityBoundaryForNotionAdapterInternals()
    {
        var repoRoot = FindRepoRoot();
        var coreRoot = Path.Combine(repoRoot, "src", "Bukit-Core");
        var engineProject = XDocument.Load(Path.Combine(
            coreRoot,
            "Bukit.Engine",
            "Bukit.Engine.csproj"));
        var engineReferences = engineProject.Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension(
                reference.Attribute("Include")?.Value.Replace('\\', Path.DirectorySeparatorChar)) ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain("Bukit.Content.Notion", engineReferences);

        var adapterProject = XDocument.Load(Path.Combine(
            coreRoot,
            "Bukit.Content.Notion",
            "Bukit.Content.Notion.csproj"));
        var adapterFriends = adapterProject.Descendants("InternalsVisibleTo")
            .Select(friend => friend.Attribute("Include")?.Value ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain("Bukit.Engine", adapterFriends);
        Assert.DoesNotContain("Bukit.Engine.Tests", adapterFriends);

        var forbidden = new[]
        {
            "NotionContentSourceOptions",
            "NotionContentClient",
            "NotionDatabaseOptionReader",
            "NotionPageQuery"
        };
        var engineRoot = Path.Combine(coreRoot, "Bukit.Engine");
        var violations = Directory.EnumerateFiles(engineRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .SelectMany(path => forbidden
                .Where(token => File.ReadAllText(path).Contains(token, StringComparison.Ordinal))
                .Select(token => $"{Path.GetRelativePath(repoRoot, path)}: {token}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
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
    public void LegacyRendererRegistry_MustDelegateDefaultOwnershipToCanonicalRegistry()
    {
        var repoRoot = FindRepoRoot();
        var legacySource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "Bukit-Core",
            "Bukit.Content",
            "Notion",
            "NotionBlockRendererRegistry.cs"));
        var canonicalSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "Bukit-Core",
            "Bukit.Notion",
            "Rendering",
            "NotionBlockRendererRegistry.cs"));

        Assert.Contains(
            "Bukit.Notion.Rendering.NotionBlockRendererRegistry.CreateDefault()",
            legacySource,
            StringComparison.Ordinal);
        Assert.DoesNotContain("registry.Register(", legacySource, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(
            canonicalSource,
            "registry\\.Register\\(\"paragraph\"",
            RegexOptions.CultureInvariant).Cast<Match>());
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

    [Fact]
    public void RemainingLegacyNotionFacades_MustMatchGovernedTwoZeroBaseline()
    {
        var contentAssembly = typeof(Bukit.Content.Notion.NotionContentProvider).Assembly;
        var sharedAssembly = typeof(Bukit.Shared.Notion.NotionBlock).Assembly;

        AssertLegacyFacadeExports(
            contentAssembly,
            "Bukit.Content.Notion",
            LegacyContentNotionTypes);
        AssertLegacyFacadeExports(
            sharedAssembly,
            "Bukit.Shared.Notion",
            LegacySharedNotionTypes);

        var governance = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "guide",
            "dev",
            "public-api-governance.md"));
        Assert.Contains("## Legacy Notion Facade Freeze", governance, StringComparison.Ordinal);
        Assert.Contains("compatibility, correctness, and security fixes only", governance, StringComparison.Ordinal);
        Assert.Contains("New Notion capabilities must be implemented in the canonical projects", governance, StringComparison.Ordinal);
    }

    private static void AssertTypesResolve(System.Reflection.Assembly assembly, IEnumerable<string> typeNames)
    {
        foreach (var typeName in typeNames)
        {
            Assert.NotNull(assembly.GetType(typeName, throwOnError: false, ignoreCase: false));
            Assert.NotNull(Type.GetType($"{typeName}, {assembly.GetName().Name}", throwOnError: false, ignoreCase: false));
        }
    }

    private static void AssertLegacyFacadeExports(
        System.Reflection.Assembly assembly,
        string namespacePrefix,
        IEnumerable<string> expectedTypeNames)
    {
        var actual = assembly.GetExportedTypes()
            .Select(static type => type.FullName)
            .Where(fullName =>
                fullName is not null &&
                fullName.StartsWith(namespacePrefix + ".", StringComparison.Ordinal))
            .Select(static fullName => fullName!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var expected = expectedTypeNames
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
    }

    private static bool IsBuildOutput(string path)
    {
        var separator = Path.DirectorySeparatorChar;
        return path.Contains($"{separator}bin{separator}", StringComparison.Ordinal) ||
               path.Contains($"{separator}obj{separator}", StringComparison.Ordinal);
    }

    private static readonly string[] LegacyContentNotionTypes =
    [
        "Bukit.Content.Notion.INotionBlockRenderer",
        "Bukit.Content.Notion.NotionApiClient",
        "Bukit.Content.Notion.NotionBlockRendererRegistry",
        "Bukit.Content.Notion.NotionBlockTransformer",
        "Bukit.Content.Notion.NotionBlocksRenderer",
        "Bukit.Content.Notion.NotionClientStats",
        "Bukit.Content.Notion.NotionContentProvider",
        "Bukit.Content.Notion.NotionPropertyParser",
        "Bukit.Content.Notion.NotionProviderOptions",
        "Bukit.Content.Notion.NotionRenderContext"
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
