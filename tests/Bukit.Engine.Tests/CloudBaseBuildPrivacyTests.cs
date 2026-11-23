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
    [InlineData("env-abc")]
    public void Evaluate_CloudBaseIdentifierInPublicOutput_Fails(string text)
    {
        File.WriteAllText(Path.Combine(_root, "index.html"), $"<p>{text}</p>");

        var result = PublicOutputPrivacyCheck.Evaluate(Config(), _root, []);

        Assert.Equal("failed", result.Status);
        Assert.NotEmpty(result.Errors);
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
}
