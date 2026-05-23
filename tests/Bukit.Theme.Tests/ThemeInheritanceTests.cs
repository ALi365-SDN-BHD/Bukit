using Xunit;
using Bukit.Theme;

namespace Bukit.Theme.Tests;

public sealed class ThemeInheritanceTests : IDisposable
{
    private readonly string _testDir;

    public ThemeInheritanceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "bukit-theme-inheritance-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    private string CreateThemeDir(string name)
    {
        var dir = Path.Combine(_testDir, name);
        Directory.CreateDirectory(dir);
        var layoutsDir = Path.Combine(dir, "layouts");
        Directory.CreateDirectory(layoutsDir);
        return dir;
    }

    [Fact]
    public void ResolveSection_ChildOverridesParent()
    {
        var parentDir = CreateThemeDir("parent");
        File.WriteAllText(Path.Combine(parentDir, "theme.yaml"), """
            name: parent
            version: 1.0.0
            sections:
              hero:
                template: sections/hero.html
                description: Parent hero
            """);

        var childDir = CreateThemeDir("child");
        File.WriteAllText(Path.Combine(childDir, "theme.yaml"), """
            name: child
            version: 1.0.0
            extends: parent
            sections:
              hero:
                template: sections/hero-child.html
                description: Child hero
            """);

        var parentManifest = ThemeManifestLoader.Load(parentDir);
        var childManifest = ThemeManifestLoader.Load(childDir);
        Assert.NotNull(parentManifest);
        Assert.NotNull(childManifest);

        var parentRegistry = new ThemeComponentRegistry(parentDir, parentManifest);
        var childRegistry = new ThemeComponentRegistry(childDir, childManifest, parentRegistry);

        var resolved = childRegistry.ResolveSection("hero");
        Assert.NotNull(resolved);
        Assert.Equal("Child hero", resolved.Description);
        Assert.Equal("sections/hero-child.html", resolved.Template);
    }

    [Fact]
    public void ResolveComponent_ChildInheritsFromParent()
    {
        var parentDir = CreateThemeDir("parent");
        File.WriteAllText(Path.Combine(parentDir, "theme.yaml"), """
            name: parent
            version: 1.0.0
            components:
              card:
                template: components/card.html
                props:
                  title: string
            """);

        var childDir = CreateThemeDir("child");
        File.WriteAllText(Path.Combine(childDir, "theme.yaml"), """
            name: child
            version: 1.0.0
            extends: parent
            """);

        var parentManifest = ThemeManifestLoader.Load(parentDir);
        var childManifest = ThemeManifestLoader.Load(childDir);
        Assert.NotNull(parentManifest);
        Assert.NotNull(childManifest);

        var parentRegistry = new ThemeComponentRegistry(parentDir, parentManifest);
        var childRegistry = new ThemeComponentRegistry(childDir, childManifest, parentRegistry);

        var resolved = childRegistry.ResolveComponent("card");
        Assert.NotNull(resolved);
        Assert.Equal("components/card.html", resolved.Template);
        Assert.NotNull(resolved.Props);
        Assert.True(resolved.Props.ContainsKey("title"));
    }

    [Fact]
    public void Tokens_MergeCorrectly()
    {
        var parentDir = CreateThemeDir("parent");
        File.WriteAllText(Path.Combine(parentDir, "tokens.yaml"), """
            colors:
              primary: "#000000"
              background: "#ffffff"
              accent: "#888888"
            """);

        var childDir = CreateThemeDir("child");
        File.WriteAllText(Path.Combine(childDir, "tokens.yaml"), """
            colors:
              primary: "#ff0000"
              text: "#333333"
            """);

        var loader = new ThemeTokensLoader();
        var merged = loader.LoadWithInheritance(childDir, parentDir);
        Assert.NotNull(merged);
        Assert.NotNull(merged.Colors);

        Assert.Equal("#ff0000", merged.Colors["primary"]);
        Assert.Equal("#ffffff", merged.Colors["background"]);
        Assert.Equal("#888888", merged.Colors["accent"]);
        Assert.Equal("#333333", merged.Colors["text"]);
    }

    [Fact]
    public void GetAllSectionNames_ReturnsMergedNames()
    {
        var parentDir = CreateThemeDir("parent");
        File.WriteAllText(Path.Combine(parentDir, "theme.yaml"), """
            name: parent
            version: 1.0.0
            sections:
              cta:
                template: sections/cta.html
            """);

        var childDir = CreateThemeDir("child");
        File.WriteAllText(Path.Combine(childDir, "theme.yaml"), """
            name: child
            version: 1.0.0
            extends: parent
            sections:
              hero:
                template: sections/hero.html
            """);

        var parentManifest = ThemeManifestLoader.Load(parentDir);
        var childManifest = ThemeManifestLoader.Load(childDir);
        Assert.NotNull(parentManifest);
        Assert.NotNull(childManifest);

        var parentRegistry = new ThemeComponentRegistry(parentDir, parentManifest);
        var childRegistry = new ThemeComponentRegistry(childDir, childManifest, parentRegistry);

        var names = childRegistry.GetAllSectionNames().OrderBy(n => n).ToList();
        Assert.Equal(2, names.Count);
        Assert.Contains("hero", names);
        Assert.Contains("cta", names);
    }

    [Fact]
    public void GetAllComponentNames_ReturnsMergedNames()
    {
        var parentDir = CreateThemeDir("parent");
        File.WriteAllText(Path.Combine(parentDir, "theme.yaml"), """
            name: parent
            version: 1.0.0
            components:
              button:
                template: components/button.html
            """);

        var childDir = CreateThemeDir("child");
        File.WriteAllText(Path.Combine(childDir, "theme.yaml"), """
            name: child
            version: 1.0.0
            extends: parent
            components:
              card:
                template: components/card.html
            """);

        var parentManifest = ThemeManifestLoader.Load(parentDir);
        var childManifest = ThemeManifestLoader.Load(childDir);
        Assert.NotNull(parentManifest);
        Assert.NotNull(childManifest);

        var parentRegistry = new ThemeComponentRegistry(parentDir, parentManifest);
        var childRegistry = new ThemeComponentRegistry(childDir, childManifest, parentRegistry);

        var names = childRegistry.GetAllComponentNames().OrderBy(n => n).ToList();
        Assert.Equal(2, names.Count);
        Assert.Contains("button", names);
        Assert.Contains("card", names);
    }

    [Fact]
    public void ResolveSectionTemplate_FromChildTheme()
    {
        var childDir = CreateThemeDir("child");
        var sectionsDir = Path.Combine(childDir, "layouts", "sections");
        Directory.CreateDirectory(sectionsDir);
        File.WriteAllText(Path.Combine(sectionsDir, "hero.html"), "<section>Child Hero</section>");

        File.WriteAllText(Path.Combine(childDir, "theme.yaml"), """
            name: child
            version: 1.0.0
            sections:
              hero:
                template: sections/hero.html
            """);

        var childManifest = ThemeManifestLoader.Load(childDir);
        Assert.NotNull(childManifest);

        var childRegistry = new ThemeComponentRegistry(childDir, childManifest);

        var templatePath = childRegistry.ResolveSectionTemplate("hero");
        Assert.NotNull(templatePath);
        Assert.EndsWith("sections/hero.html", templatePath);
    }

    [Fact]
    public void ResolveSectionTemplate_FromParentWhenChildDoesNotHave()
    {
        var parentDir = CreateThemeDir("parent");
        var parentSectionsDir = Path.Combine(parentDir, "layouts", "sections");
        Directory.CreateDirectory(parentSectionsDir);
        File.WriteAllText(Path.Combine(parentSectionsDir, "cta.html"), "<section>Parent CTA</section>");

        File.WriteAllText(Path.Combine(parentDir, "theme.yaml"), """
            name: parent
            version: 1.0.0
            sections:
              cta:
                template: sections/cta.html
            """);

        var childDir = CreateThemeDir("child");
        File.WriteAllText(Path.Combine(childDir, "theme.yaml"), """
            name: child
            version: 1.0.0
            extends: parent
            sections:
              hero:
                template: sections/hero.html
            """);

        var parentManifest = ThemeManifestLoader.Load(parentDir);
        var childManifest = ThemeManifestLoader.Load(childDir);
        Assert.NotNull(parentManifest);
        Assert.NotNull(childManifest);

        var parentRegistry = new ThemeComponentRegistry(parentDir, parentManifest);
        var childRegistry = new ThemeComponentRegistry(childDir, childManifest, parentRegistry);

        var templatePath = childRegistry.ResolveSectionTemplate("cta");
        Assert.NotNull(templatePath);
        Assert.EndsWith("sections/cta.html", templatePath);
    }
}
