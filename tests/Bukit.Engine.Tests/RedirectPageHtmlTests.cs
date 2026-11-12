using Xunit;

namespace Bukit.Engine.Tests;

public sealed class RedirectPageHtmlTests
{
    [Theory]
    [InlineData("https://example.com/en/")]
    [InlineData("/repo/zh-CN/")]
    public void Build_ContainsRedirectContract(string target)
    {
        var html = RedirectPageHtml.Build(target);

        Assert.Contains($"content=\"0; url={target}\"", html, StringComparison.Ordinal);
        Assert.Contains($"<link rel=\"canonical\" href=\"{target}\">", html, StringComparison.Ordinal);
        Assert.Contains("<meta name=\"robots\" content=\"noindex\">", html, StringComparison.Ordinal);
        Assert.Contains("<title>Redirect</title>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_EscapesAttributeAndTextContent()
    {
        var html = RedirectPageHtml.Build("/a?x=1&y=\"2\"&z='3'");

        Assert.Contains("x=1&amp;y=&quot;2&quot;&amp;z=&#39;3&#39;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("y=\"2\"", html, StringComparison.Ordinal);
        Assert.Contains("<title>Redirect</title>", html, StringComparison.Ordinal);
    }
}
