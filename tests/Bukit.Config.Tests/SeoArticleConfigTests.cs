using Bukit.Shared;
using Xunit;

namespace Bukit.Config.Tests;

public sealed class SeoArticleConfigTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    public void Dispose()
    {
        foreach (var path in _tempFiles)
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_ReadsArticleTypesAndAuthors()
    {
        var config = Load("""
            seo:
              schema:
                articleType: Article
              authors:
                Aisha Tan:
                  type: Person
                  url: /about/aisha/
                  image: /images/aisha.jpg
                  sameAs: [https://social.example/aisha]
                author-2:
                  name: Newsroom
                  type: Organization
            collections:
              news:
                permalink: /news/{slug}/
                seo:
                  articleType: NewsArticle
              posts:
                permalink: /posts/{slug}/
            """);
        SeoArticleConfig.Validate(config.Site);

        Assert.Equal("Article", config.Site.Seo.Schema.ArticleType);
        Assert.Equal("NewsArticle", config.Site.Collections!["news"].ArticleType);
        Assert.Null(config.Site.Collections["posts"].ArticleType);
        var aisha = config.Site.Seo.Authors!["aisha tan"];
        Assert.Equal("/about/aisha/", aisha.Url);
        Assert.Equal("/images/aisha.jpg", aisha.Image);
        Assert.Equal(["https://social.example/aisha"], aisha.SameAs);
        Assert.Equal("Newsroom", config.Site.Seo.Authors["AUTHOR-2"].Name);

        Assert.Equal("NewsArticle", SeoArticleConfig.ResolveArticleType(config.Site, "news"));
        Assert.Equal("Article", SeoArticleConfig.ResolveArticleType(config.Site, "posts"));
        Assert.Equal("Article", SeoArticleConfig.ResolveArticleType(config.Site, null));
    }

    [Fact]
    public void ResolveArticleType_DefaultsToBlogPosting()
    {
        var config = Load(string.Empty);

        Assert.Equal("BlogPosting", SeoArticleConfig.ResolveArticleType(config.Site, "posts"));
    }

    [Theory]
    [InlineData("seo:\n  authors:\n    Aisha:\n      email: a@example.com", "site.seo.authors.Aisha")]
    [InlineData("collections:\n  posts:\n    permalink: /p/{slug}/\n    seo:\n      ogType: article", "site.collections.posts.seo")]
    public void Load_RejectsUnknownKeys(string fragment, string path)
    {
        var ex = Assert.Throws<ConfigException>(() => Load(fragment));

        Assert.Contains(path, ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("seo:\n  schema:\n    articleType: FAQPage", "site.seo.schema.articleType must be BlogPosting")]
    [InlineData("seo:\n  schema:\n    articleType: \"News Article\"", "site.seo.schema.articleType must be BlogPosting")]
    [InlineData("seo:\n  schema:\n    articleType: newsarticle", "site.seo.schema.articleType must be BlogPosting")]
    [InlineData("seo:\n  schema:\n    articleType: blogposting", "site.seo.schema.articleType must be BlogPosting")]
    [InlineData("collections:\n  posts:\n    permalink: /p/{slug}/\n    seo:\n      articleType: WebPage", "site.collections.posts.seo.articleType must be BlogPosting")]
    [InlineData("seo:\n  authors:\n    Aisha:\n      type: Company", "site.seo.authors.Aisha.type must be Person|Organization")]
    [InlineData("seo:\n  authors:\n    Aisha:\n      url: \"  \"", "site.seo.authors.Aisha.url must not be blank")]
    public void Validate_RejectsInvalidValues(string fragment, string message)
    {
        var config = Load(fragment);

        var ex = Assert.Throws<ConfigException>(() => SeoArticleConfig.Validate(config.Site));

        Assert.Contains(message, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigValidator_RunsTheArticleChecks()
    {
        var config = Load("seo:\n  schema:\n    articleType: FAQPage");

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));

        Assert.Contains("site.seo.schema.articleType", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_RejectsAuthorKeysThatDifferOnlyInCase()
    {
        var ex = Assert.Throws<ConfigException>(() => Load("seo:\n  authors:\n    Aisha:\n      url: /a/\n    aisha:\n      url: /b/"));

        Assert.Contains("twice", ex.Message, StringComparison.Ordinal);
    }

    private AppConfig Load(string siteFragment)
    {
        var indented = string.Join(
            Environment.NewLine,
            siteFragment.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Select(line => $"  {line}"));
        var yaml = $$"""
            site:
              name: article-test
              title: Article Test
            {{indented}}
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            """;
        var path = Path.Combine(Path.GetTempPath(), $"bukit-seo-article-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, yaml);
        _tempFiles.Add(path);
        return ConfigLoader.Load(path);
    }
}
