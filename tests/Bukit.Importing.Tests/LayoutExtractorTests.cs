using Bukit.Importing;
using Xunit;

namespace Bukit.Importing.Tests;

public sealed class LayoutExtractorTests
{
    private static DiscoveredPage MakePage(string bodyOpening, string uniqueBody, string bodyClosing, string slug = "page", string headContent = "  <link rel=\"stylesheet\" href=\"style.css\" />")
    {
        return new DiscoveredPage
        {
            FilePath = $"/test/{slug}.html",
            RelativePath = $"{slug}.html",
            Slug = slug,
            Type = PageType.Page,
            Title = "Test",
            FullHtml = $"<html><head>{headContent}</head><body>{bodyOpening}{uniqueBody}{bodyClosing}</body></html>",
            HeadContent = headContent,
            BodyOpening = bodyOpening,
            UniqueBody = uniqueBody,
            BodyClosing = bodyClosing
        };
    }

    [Fact]
    public void Extract_SinglePage_HeaderIsBodyOpening()
    {
        var pages = new List<DiscoveredPage>
        {
            MakePage("<header>Nav</header>\n", "<main>Content</main>\n", "<footer>End</footer>\n")
        };

        var result = LayoutExtractor.Extract(pages, []);

        Assert.Contains("Nav", result.Header);
        Assert.Contains("End", result.Footer);
        Assert.Contains("style.css", result.HeadExtras);
    }

    [Fact]
    public void Extract_TwoPages_ExtractsCommonPrefix()
    {
        var pages = new List<DiscoveredPage>
        {
            MakePage("<header>Nav</header>\n", "<main>Page 1</main>\n", "<footer>End</footer>\n", "page1"),
            MakePage("<header>Nav</header>\n", "<main>Page 2</main>\n", "<footer>End</footer>\n", "page2"),
        };

        var result = LayoutExtractor.Extract(pages, []);

        Assert.Contains("<header>Nav</header>", result.Header);
        Assert.Contains("<footer>End</footer>", result.Footer);
        Assert.DoesNotContain("Page 1", result.Header);
    }

    [Fact]
    public void Extract_MultiplePages_DifferentContentNotInCommon()
    {
        var pages = new List<DiscoveredPage>
        {
            MakePage("<header>Shared</header>\n", "<main>Unique A</main>\n", "<footer>Shared</footer>\n", "a"),
            MakePage("<header>Shared</header>\n", "<main>Unique B</main>\n", "<footer>Shared</footer>\n", "b"),
            MakePage("<header>Shared</header>\n", "<main>Unique C</main>\n", "<footer>Shared</footer>\n", "c"),
        };

        var result = LayoutExtractor.Extract(pages, []);

        Assert.Contains("Shared", result.Header);
        Assert.Contains("Shared", result.Footer);
        Assert.DoesNotContain("Unique", result.Header);
    }

    [Fact]
    public void Extract_NavBlockDetected()
    {
        var pages = new List<DiscoveredPage>
        {
            MakePage("<header><nav><a href=\"/\">Home</a></nav></header>\n", "<main>A</main>\n", "<footer>F</footer>\n", "a"),
            MakePage("<header><nav><a href=\"/\">Home</a></nav></header>\n", "<main>B</main>\n", "<footer>F</footer>\n", "b"),
        };

        var result = LayoutExtractor.Extract(pages, []);

        Assert.Contains("<nav>", result.Nav);
        Assert.Contains("Home", result.Nav);
    }

    [Fact]
    public void Extract_MenuClassBlockDetectedWithoutNavTag()
    {
        var pages = new List<DiscoveredPage>
        {
            MakePage("<header><div class=\"main-menu\"><a href=\"/\">Home</a><a href=\"/about/\">About</a></div></header>\n", "<main>A</main>\n", "<footer>F</footer>\n", "a"),
            MakePage("<header><div class=\"main-menu\"><a href=\"/\">Home</a><a href=\"/about/\">About</a></div></header>\n", "<main>B</main>\n", "<footer>F</footer>\n", "b"),
        };

        var result = LayoutExtractor.Extract(pages, []);

        Assert.Contains("main-menu", result.Nav);
        Assert.Contains("About", result.Nav);
        Assert.True(result.HeaderContainsNav);
    }

    [Fact]
    public void Extract_ShortHeader_AddsWarning()
    {
        var warnings = new List<string>();
        var pages = new List<DiscoveredPage>
        {
            MakePage("<h1>X</h1>\n", "<main>A</main>\n", "<p>Y</p>\n", "a"),
            MakePage("<h1>X</h1>\n", "<main>B</main>\n", "<p>Y</p>\n", "b"),
        };

        LayoutExtractor.Extract(pages, warnings);

        Assert.Contains(warnings, w => w.Contains("header", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NormalizeBlock_RemovesCommonIndent()
    {
        var content = "    <div>\n        <p>text</p>\n    </div>";
        var result = LayoutExtractor.NormalizeBlock(content);
        Assert.DoesNotContain("        ", result);
        Assert.Contains("<div>", result);
    }

    [Fact]
    public void Extract_SinglePage_WithHeaderTag_UsesSemanticTag()
    {
        var pages = new List<DiscoveredPage>
        {
            MakePage(
                "<header>MyHeader</header>\n<section>Hero</section>\n",
                "<main>Content</main>\n",
                "<section>CTA</section>\n<footer>MyFooter</footer>\n")
        };

        var result = LayoutExtractor.Extract(pages, []);

        Assert.Contains("MyHeader", result.Header);
        Assert.DoesNotContain("Hero", result.Header);
        Assert.Contains("MyFooter", result.Footer);
        Assert.DoesNotContain("CTA", result.Footer);
    }

    [Fact]
    public void Extract_MultiPage_WithDifferentBodyClass_StillMatches()
    {
        var pages = new List<DiscoveredPage>
        {
            MakePage("<header class=\"site-header\">Shared</header>\n", "<main>A</main>\n", "<footer class=\"site-footer\">End</footer>\n", "a"),
            MakePage("<header class=\"alt-header\">Shared</header>\n", "<main>B</main>\n", "<footer class=\"alt-footer\">End</footer>\n", "b"),
        };

        var result = LayoutExtractor.Extract(pages, []);

        Assert.Contains("Shared", result.Header);
        Assert.Contains("End", result.Footer);
    }
}
