using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;
using Bukit.Theme;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ThemeTemplateResolverTests
{
    [Fact]
    public void ResolveHomeTemplate_WithoutThemeDeclaration_DefaultsToPagesIndexHtml()
    {
        var resolver = new ThemeTemplateResolver(null);

        Assert.Equal("pages/index.html", resolver.ResolveHomeTemplate());
    }

    [Fact]
    public void ResolveRequiredTemplates_HomeRequiredFalse_Throws()
    {
        var manifest = new ThemeManifestV2
        {
            Templates = new Dictionary<string, ThemeTemplateDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["home"] = new() { Template = "screens/home.html", Required = false }
            }
        };

        var ex = Assert.Throws<ConfigException>(() => new ThemeTemplateResolver(manifest).ValidateRequiredTemplates());

        Assert.Contains("home.required", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveContentTemplate_MatchesAcceptsWithoutHardcodedRole()
    {
        var manifest = new ThemeManifestV2
        {
            Templates = new Dictionary<string, ThemeTemplateDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["article"] = new()
                {
                    Template = "content/article.html",
                    Accepts = new ThemeTemplateAccept { Type = "post", Collection = "articles" }
                }
            }
        };
        var item = ContentDocument.Create(
            "1",
            "Hello",
            "hello",
            DateTimeOffset.UnixEpoch,
            "<p>Hello</p>",
            ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = "post",
                ["collection"] = "articles"
            }));

        var template = new ThemeTemplateResolver(manifest).ResolveContentTemplate(item);

        Assert.Equal("content/article.html", template);
    }

    [Fact]
    public void ResolveContentTemplate_WhenNoMatchingTemplate_Throws()
    {
        var manifest = new ThemeManifestV2
        {
            Templates = new Dictionary<string, ThemeTemplateDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["home"] = new() { Template = "index.html", Required = true }
            }
        };
        var item = ContentDocument.Create(
            "1",
            "Hello",
            "hello",
            DateTimeOffset.UnixEpoch,
            "<p>Hello</p>",
            ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = "post",
                ["collection"] = "articles"
            }));

        var ex = Assert.Throws<ConfigException>(() => new ThemeTemplateResolver(manifest).ResolveContentTemplate(item));

        Assert.Contains("No theme template matches", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("articles", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveContentTemplate_MatchesStructuredTypeAndCollection()
    {
        var manifest = new ThemeManifestV2
        {
            Templates = new Dictionary<string, ThemeTemplateDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["article"] = new()
                {
                    Template = "content/article.html",
                    Accepts = new ThemeTemplateAccept { Type = "post", Collection = "articles" }
                }
            }
        };
        var item = ContentDocument.Create(
            "1",
            "Hello",
            "hello",
            DateTimeOffset.UnixEpoch,
            "<p>Hello</p>",
            new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = new("text", "post"),
                ["collection"] = new("text", "articles")
            });

        var template = new ThemeTemplateResolver(manifest).ResolveContentTemplate(item);

        Assert.Equal("content/article.html", template);
    }
}
