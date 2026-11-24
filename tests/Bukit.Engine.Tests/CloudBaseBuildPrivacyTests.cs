using Bukit.Config;
using Bukit.Engine;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class CloudBaseBuildPrivacyTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bukit-cloudbase-build-privacy", Guid.NewGuid().ToString("N"));

    public CloudBaseBuildPrivacyTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Theory]
    [InlineData("cloud://env-abc.media/a.png")]
    [InlineData("https://env-abc.api.tcloudbasegateway.com/v1/storages/object/sign/media/a.png?token=x")]
    [InlineData("https://env-abc.storage.tcloudbase.com/media/a.png?token=x")]
    [InlineData("https://other-env.storage.tcloudbase.com/b/a.pdf")]
    public void Evaluate_CloudBaseIdentifierInPublicOutput_Fails(string text)
    {
        File.WriteAllText(Path.Combine(_root, "index.html"), $"<p>{text}</p>");

        var result = PublicOutputPrivacyCheck.Evaluate(Config(), _root, []);

        Assert.Equal("failed", result.Status);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Evaluate_CloudBaseLeak_NamesCloudBase()
    {
        File.WriteAllText(Path.Combine(_root, "index.html"), "<p>cloud://env-abc.media/a.png</p>");

        var result = PublicOutputPrivacyCheck.Evaluate(Config(), _root, []);

        Assert.Contains(result.Errors, error => error.Contains("CloudBase provenance", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_BareEnvIdInTextAndPath_Passes()
    {
        var config = ConfigFor("blog");
        var output = Path.Combine(_root, "blog");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "index.html"), "<p>blog test results</p>");

        var result = PublicOutputPrivacyCheck.Evaluate(config, _root, []);

        Assert.Equal("passed", result.Status);
    }

    [Fact]
    public void Evaluate_OtherEnvironmentGatewayHost_Passes()
    {
        File.WriteAllText(
            Path.Combine(_root, "index.html"),
            """<a href="https://xprod-1.api.tcloudbasegateway.com/x">x</a>""");

        Assert.Equal("passed", PublicOutputPrivacyCheck.Evaluate(ConfigFor("prod-1"), _root, []).Status);
    }

    [Fact]
    public void Evaluate_OwnEnvironmentGatewayHost_Fails()
    {
        File.WriteAllText(
            Path.Combine(_root, "index.html"),
            """<a href="https://prod-1.api.tcloudbasegateway.com/x">x</a>""");

        Assert.Equal("failed", PublicOutputPrivacyCheck.Evaluate(ConfigFor("prod-1"), _root, []).Status);
    }

    [Theory]
    [InlineData(@"<script type=""application/ld+json"">{""articleBody"":""para\ncloud://env-abc.media/a.png""}</script>")]
    [InlineData("""<img src="/img?url=https%3A%2F%2Fprod-1.api.tcloudbasegateway.com%2Fx">""")]
    [InlineData(@"<script type=""application/ld+json"">{""articleBody"":""See\nprod-1.api.tcloudbasegateway.com""}</script>")]
    public void Evaluate_EscapedCloudBaseReferenceInHtml_Fails(string html)
    {
        File.WriteAllText(Path.Combine(_root, "index.html"), html);

        Assert.Equal("failed", PublicOutputPrivacyCheck.Evaluate(ConfigFor("prod-1"), _root, []).Status);
    }

    [Theory]
    [InlineData("prod-1.storage.tcloudbase.com", "prod-1.storage.tcloudbase.com", true)]
    [InlineData("see https://prod-1.storage.tcloudbase.com/a", "prod-1.storage.tcloudbase.com", true)]
    [InlineData("xprod-1.storage.tcloudbase.com", "prod-1.storage.tcloudbase.com", false)]
    [InlineData("a-prod-1.storage.tcloudbase.com", "prod-1.storage.tcloudbase.com", false)]
    [InlineData("a.prod-1.storage.tcloudbase.com", "prod-1.storage.tcloudbase.com", true)]
    [InlineData("xprod-1.storage.tcloudbase.com prod-1.storage.tcloudbase.com", "prod-1.storage.tcloudbase.com", true)]
    [InlineData("https%3A%2F%2Fprod-1.storage.tcloudbase.com%2Fa", "prod-1.storage.tcloudbase.com", true)]
    [InlineData(@"https:\x2F\x2Fprod-1.storage.tcloudbase.com", "prod-1.storage.tcloudbase.com", true)]
    [InlineData(@"https:\u002F\u002Fprod-1.storage.tcloudbase.com", "prod-1.storage.tcloudbase.com", true)]
    [InlineData(@"para\ncloud://env.media/a.png", "cloud://", true)]
    [InlineData(@"See\nprod-1.storage.tcloudbase.com", "prod-1.storage.tcloudbase.com", true)]
    [InlineData(@"See\tprod-1.storage.tcloudbase.com", "prod-1.storage.tcloudbase.com", true)]
    public void ContainsHostToken_MatchesAtLabelBoundary(string text, string token, bool expected)
    {
        Assert.Equal(expected, PublicOutputPrivacyCheck.ContainsHostToken(text, token));
    }

    [Fact]
    public void Evaluate_BareSignPathWithoutToken_Passes()
    {
        File.WriteAllText(
            Path.Combine(_root, "index.html"),
            "<p>Signed links look like /v1/storages/object/sign/media/a.png without the token parameter</p>");

        var result = PublicOutputPrivacyCheck.Evaluate(Config(), _root, []);

        Assert.Equal("passed", result.Status);
    }

    [Fact]
    public void Evaluate_RelativeSignedPathWithToken_Fails()
    {
        File.WriteAllText(
            Path.Combine(_root, "index.html"),
            """<img src="/v1/storages/object/sign/media/a.png?token=x">""");

        var result = PublicOutputPrivacyCheck.Evaluate(Config(), _root, []);

        Assert.Equal("failed", result.Status);
    }

    [Theory]
    [InlineData("data.json", """{"u":"https:\/\/cdn.example.com\/v1\/storages\/object\/sign\/a.png?x=1\u0026token=x"}""")]
    [InlineData("index.html", """<img src="/v1/storages/object/sign/a.png?x=1&amp;token=x">""")]
    [InlineData("index.html", """<img src="https://env-x.storage.tcloudbase.com:443/a.png?token=x">""")]
    [InlineData("index.html", """<img src="/v1/storages/object/sign/a.png?x=1&#x26;token=x">""")]
    [InlineData("feed.json", """{"content_html":"\u003Cimg src=\u0022/v1/storages/object/sign/a.png?x=1\u0026amp;token=x\u0022\u003E"}""")]
    public void Evaluate_EscapedSignedLink_Fails(string file, string content)
    {
        File.WriteAllText(Path.Combine(_root, file), content);

        var result = PublicOutputPrivacyCheck.Evaluate(NotionOnlyConfig(), _root, []);

        Assert.Equal("failed", result.Status);
    }

    [Theory]
    [InlineData("""{"h":"\u003Cimg src=\u0022/v1/storages/object/sign/a.png\u0022\u003E\u0026token=x"}""")]
    [InlineData("""{"h":"<img src=\"/v1/storages/object/sign/a.png\">&token=x"}""")]
    [InlineData("""{"h":"/v1/storages/object/sign/a.png\n?token=x"}""")]
    public void Evaluate_TokenOutsideEscapedSignedPath_Passes(string json)
    {
        File.WriteAllText(Path.Combine(_root, "data.json"), json);

        var result = PublicOutputPrivacyCheck.Evaluate(NotionOnlyConfig(), _root, []);

        Assert.Equal("passed", result.Status);
    }

    [Fact]
    public void Evaluate_NotionOnlyEscapedMarker_KeepsNotionWording()
    {
        File.WriteAllText(Path.Combine(_root, "data.json"), """{"source":"not\u0069on"}""");

        var result = PublicOutputPrivacyCheck.Evaluate(NotionOnlyConfig(), _root, []);

        var error = Assert.Single(result.Errors);
        Assert.Contains("contains internal Notion provenance", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Evaluate_NotionOnlyMalformedJson_KeepsNotionWording()
    {
        File.WriteAllText(Path.Combine(_root, "data.json"), """{"source":""");

        var result = PublicOutputPrivacyCheck.Evaluate(NotionOnlyConfig(), _root, []);

        var error = Assert.Single(result.Errors);
        Assert.Contains("unable to complete the Notion provenance privacy scan", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Evaluate_CloudBaseSiteMalformedJson_UsesContentWording()
    {
        File.WriteAllText(Path.Combine(_root, "data.json"), """{"source":""");

        var result = PublicOutputPrivacyCheck.Evaluate(Config(), _root, []);

        Assert.Contains(result.Errors, error => error.Contains("unable to complete the content provenance privacy scan", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_CloudBaseSiteEscapedJsonLeak_NamesCloudBase()
    {
        File.WriteAllText(Path.Combine(_root, "data.json"), """{"image":"\u0063loud://env-abc.media/a.png"}""");

        var result = PublicOutputPrivacyCheck.Evaluate(Config(), _root, []);

        Assert.Contains(result.Errors, error => error.Contains("internal CloudBase provenance", StringComparison.Ordinal));
    }

    private static AppConfig NotionOnlyConfig()
        => new()
        {
            Site = new SiteConfig { Name = "site", Title = "Site" },
            Content = ContentConfigFactory.FromSources(
            [
                new ContentSourceConfig
                {
                    Type = "notion",
                    Name = "page",
                    Collection = "page",
                    Notion = new NotionConfig { DatabaseId = "0123456789abcdef0123456789abcdef" }
                }
            ])
        };

    [Fact]
    public void Evaluate_NoCloudBaseSource_IsNotApplicable()
    {
        var config = Config() with
        {
            Content = ContentConfigFactory.FromSources(
            [
                new ContentSourceConfig { Type = "markdown", Name = "page", Collection = "page" }
            ])
        };

        Assert.Equal("not_applicable", PublicOutputPrivacyCheck.Evaluate(config, _root, []).Status);
    }

    private static AppConfig ConfigFor(string envId)
        => new()
        {
            Site = new SiteConfig { Name = "site", Title = "Site" },
            Content = ContentConfigFactory.FromSources(
            [
                new ContentSourceConfig
                {
                    Type = "cloudbase",
                    Name = "page",
                    Collection = "page",
                    CloudBase = new CloudBaseConfig
                    {
                        EnvId = envId,
                        Table = "articles",
                        BodyField = "content"
                    }
                }
            ])
        };

    private static AppConfig Config()
        => new()
        {
            Site = new SiteConfig { Name = "site", Title = "Site" },
            Content = ContentConfigFactory.FromSources(
            [
                new ContentSourceConfig
                {
                    Type = "cloudbase",
                    Name = "page",
                    Collection = "page",
                    CloudBase = new CloudBaseConfig
                    {
                        EnvId = "env-abc",
                        Table = "articles",
                        BodyField = "content"
                    }
                }
            ])
        };

    [Theory]
    [InlineData("""{"summary":"\ud83d","note":"cloud://docs"}""", true)]
    [InlineData("""{"image":"cloud://env-abc.media/a.png""", true)]
    [InlineData("""{"summary":"\ud83d","note":"see cloud://docs"}""", true)]
    [InlineData("""{"summary":"ok","note":"see cloud://docs"}""", false)]
    public void DeployUrlScanner_UndecodableJson_IsNotReportedClean(string json, bool expected)
    {
        Assert.Equal(expected, CloudBaseDeployUrlScanner.ContainsCloudReference(json, ".json"));
    }
}
