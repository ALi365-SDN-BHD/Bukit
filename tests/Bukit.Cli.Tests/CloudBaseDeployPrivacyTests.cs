using Bukit.Cli.Deploy;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class CloudBaseDeployPrivacyTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bukit-cloudbase-deploy-privacy", Guid.NewGuid().ToString("N"));
    private readonly string _output;
    private readonly string _staged;

    public CloudBaseDeployPrivacyTests()
    {
        _output = Path.Combine(_root, "output");
        _staged = Path.Combine(_root, "staged");
        Directory.CreateDirectory(Path.Combine(_output, ".bukit"));
        Directory.CreateDirectory(_staged);
        File.WriteAllText(
            Path.Combine(_output, ".bukit", "publish-audit-report.json"),
            """{"schema":"https://bukit.dev/schemas/publish-audit-report.v1.json","documents":[]}""");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Theory]
    [InlineData("index.html", """<img src="cloud://env-abc.media/a.png">""")]
    [InlineData("index.html", """<img src=cloud://env-abc.media/a.png>""")]
    [InlineData("index.html", """<a href="cloud://env-abc.media/a.pdf">x</a>""")]
    [InlineData("index.html", """<img srcset="a.png 1x, cloud://env-abc.media/b.png 2x">""")]
    [InlineData("index.html", """<link href="cloud://env-abc.media/a.css">""")]
    [InlineData("index.html", """<meta property="og:image" content="cloud://env-abc.media/a.png">""")]
    [InlineData("index.html", """<div style="background:url(cloud://env-abc.media/a.png)"></div>""")]
    [InlineData("index.html", """<div style=background:url(cloud://env-abc.media/a.png)></div>""")]
    [InlineData("index.html", """<a title="a > b" href="cloud://env-abc.media/a.pdf">x</a>""")]
    [InlineData("feed.rss", """<enclosure url="cloud://env-abc.media/a.mp3"/>""")]
    [InlineData("site.css", """a{background:url("cloud://env-abc.media/a.png")}""")]
    [InlineData("site.css", "a{background:url(cloud://env-abc.media/a.png)}")]
    [InlineData("data.json", """{"image":"cloud://env-abc.media/a.png"}""")]
    public void Validate_CloudReferenceInUrlPosition_Fails(string file, string content)
    {
        Write(file, content);

        Assert.Contains(Validate(), error => error.StartsWith("BKT-DEPLOY-PRIVACY-0001", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("guide.html", "<p>Use cloud://env-abc.media/a.png to upload.</p>")]
    [InlineData("guide.md", "Call `cloud://env-abc.media/a.png` at runtime.")]
    [InlineData("app.js", "const x = 'cloud://env-abc.media/a.png';")]
    [InlineData("notes.txt", "cloud://env-abc.media/a.png")]
    [InlineData("guide.md", "Use `/v1/storages/object/sign/media/a.png` without the token parameter.")]
    [InlineData("data.json", """{"note":"mentions cloud://env-abc.media/a.png inline"}""")]
    public void Validate_CloudReferenceInText_DoesNotFail(string file, string content)
    {
        Write(file, content);

        Assert.Empty(Validate());
    }

    [Theory]
    [InlineData("""<a href="https://env-abc.api.tcloudbasegateway.com/v1/storages/object/sign/media/a.png?token=x">x</a>""")]
    [InlineData("""<img src="https://env-abc.storage.tcloudbase.com/media/a.png?token=x">""")]
    [InlineData("""<a href="http://env-abc.api.intl.tcloudbasegateway.com/v1/storages/object/sign/media/a.png?token=x">x</a>""")]
    [InlineData("""<img src="//env-abc.storage.tcloudbase.com/media/a.png?x=1&token=x">""")]
    public void Validate_SignedCloudBaseLink_FailsAnywhere(string content)
    {
        Write("guide.md", content);
        Write("code.html", $"<code>{content}</code>");

        Assert.NotEmpty(Validate());
    }

    [Theory]
    [InlineData("""<a href="https://env-abc.api.tcloudbasegateway.com/v1/storages/object/sign/media/a.png">x</a>""")]
    [InlineData("""<img src="https://env-abc.storage.tcloudbase.com/media/a.png">""")]
    public void Validate_UnsignedCloudBaseLink_Passes(string content)
    {
        Write("index.html", content);

        Assert.Empty(Validate());
    }

    [Fact]
    public void Validate_ConfiguredEnvId_Fails()
    {
        Write("index.html", "<p>https://env-abc.api.tcloudbasegateway.com/x</p>");

        Assert.NotEmpty(DeploymentPrivacyValidator.Validate(_output, _staged, ["env-abc"]));
        Assert.Empty(DeploymentPrivacyValidator.Validate(_output, _staged));
    }

    [Fact]
    public void Validate_BareEnvIdInProseAndPath_Passes()
    {
        Write("blog/index.html", "<p>blog test results</p>");

        Assert.Empty(DeploymentPrivacyValidator.Validate(_output, _staged, ["blog"]));
    }

    [Fact]
    public void Validate_EnvIdPrefixDoesNotMatchLongerHost()
    {
        Write("index.html", """<a href="https://prod-10.api.tcloudbasegateway.com/x">x</a>""");

        Assert.Empty(DeploymentPrivacyValidator.Validate(_output, _staged, ["prod-1"]));
    }

    [Fact]
    public void Validate_OtherEnvironmentHostSharingSuffix_Passes()
    {
        Write("index.html", """<a href="https://xprod-1.api.tcloudbasegateway.com/x">x</a>""");

        Assert.Empty(DeploymentPrivacyValidator.Validate(_output, _staged, ["prod-1"]));
    }

    [Theory]
    [InlineData("""<img src="/img?u=https%3A%2F%2Fprod-1.storage.tcloudbase.com%2Fa.png">""")]
    [InlineData("""<img src="/img?u=https%3A%2F%2Fprod-1.api.tcloudbasegateway.com%2Fx">""")]
    public void Validate_PercentEncodedConfiguredHost_Fails(string content)
    {
        Write("index.html", content);

        Assert.NotEmpty(DeploymentPrivacyValidator.Validate(_output, _staged, ["prod-1"]));
    }

    [Fact]
    public void Validate_SecondCloudBaseEnvId_Fails()
    {
        Write("index.html", """<a href="https://env-b.api.tcloudbasegateway.com/x">x</a>""");

        Assert.NotEmpty(DeploymentPrivacyValidator.Validate(_output, _staged, ["env-a", "env-b"]));
    }

    [Fact]
    public void Validate_CloudBaseSignedLink_NamesCloudBase()
    {
        Write("guide.md", """https://env-abc.api.tcloudbasegateway.com/v1/storages/object/sign/media/a.png?token=x""");

        var errors = DeploymentPrivacyValidator.Validate(_output, _staged);

        Assert.Contains(errors, error => error.Contains("CloudBase provenance", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_MalformedJson_WordingFollowsCloudBaseSource()
    {
        Write("data.json", """{"title":""");

        Assert.Contains(Validate(), error => error.Contains("unable to complete the Notion provenance privacy scan", StringComparison.Ordinal));
        Assert.Contains(
            DeploymentPrivacyValidator.Validate(_output, _staged, ["env-abc"]),
            error => error.Contains("unable to complete the content provenance privacy scan", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_MalformedJsonWithCloudReference_Fails()
    {
        Write("data.json", """{"image":"cloud://env-abc.media/a.png""");

        Assert.NotEmpty(Validate());
    }

    private IReadOnlyList<string> Validate() => DeploymentPrivacyValidator.Validate(_output, _staged);

    private void Write(string relative, string content)
    {
        var path = Path.Combine(_staged, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
