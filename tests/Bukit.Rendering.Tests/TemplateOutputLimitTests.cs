using Xunit;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Rendering.Scriban;

namespace Bukit.Rendering.Tests;

public sealed class TemplateOutputLimitTests : IDisposable
{
    private readonly string _layoutsDir;

    public TemplateOutputLimitTests()
    {
        _layoutsDir = Path.Combine(Path.GetTempPath(), "bukit-output-limit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_layoutsDir, "partials"));
    }

    public void Dispose() => TestCleanup.DeleteDirectory(_layoutsDir, recursive: true);

    [Fact]
    public void RenderPage_LargeStringInsideInclude_IsNotTruncated()
    {
        // Scriban's default LimitToString (1 MiB) counts include output twice and cut it near 524k chars.
        File.WriteAllText(Path.Combine(_layoutsDir, "partials", "big.html"), "{{ page.content }}");
        File.WriteAllText(Path.Combine(_layoutsDir, "page.html"), "<html>{{ include 'partials/big.html' }}</html>");
        var renderer = new ScribanTemplateRenderer(_layoutsDir, null, null, null, null, null, null, null, "off");
        var content = new string('x', 1_200_000);
        var model = new PageModel
        {
            Site = new SiteModel { Name = "t", Title = "T", Url = "https://example.com", BaseUrl = "/", Language = "en" },
            Page = new PageInfo { Title = "T", Content = content, Url = "/" }
        };

        var result = renderer.RenderPage("page.html", model);

        Assert.Equal("<html>" + content + "</html>", result);
    }
}
