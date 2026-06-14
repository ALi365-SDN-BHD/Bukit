using Bukit.Labs.Cli.Commands;
using Xunit;

namespace Bukit.Labs.Cli.Tests;

public sealed class CloneLayoutGeneratorTests
{
    [Fact]
    public void GenerateBaseLayout_WithOptionalAssetsAndBehaviors_RendersAllBlocks()
    {
        var tokens = new CloneTokens
        {
            GoogleFontsUrl = "https://fonts.googleapis.com/css2?family=Inter:wght@400;700&display=swap",
            ExternalCssUrls = [" https://cdn.example.com/a.css ", "   ", "https://cdn.example.com/b.css"],
            ExternalJsUrls = [" https://cdn.example.com/a.js ", "", "https://cdn.example.com/b.js"]
        };

        var html = CloneLayoutGenerator.GenerateBaseLayout(tokens, new CloneBehaviors
        {
            MobileHamburger = true,
            UseLenis = true
        });

        Assert.Contains("fonts.googleapis.com", html, StringComparison.Ordinal);
        Assert.Contains("<link rel=\"stylesheet\" href=\"https://cdn.example.com/a.css\" />", html, StringComparison.Ordinal);
        Assert.Contains("<link rel=\"stylesheet\" href=\"https://cdn.example.com/b.css\" />", html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"   \"", html, StringComparison.Ordinal);
        Assert.Contains("<script src=\"https://cdn.example.com/a.js\" defer></script>", html, StringComparison.Ordinal);
        Assert.Contains("<script src=\"https://cdn.example.com/b.js\" defer></script>", html, StringComparison.Ordinal);
        Assert.Contains("assets/behaviors.js", html, StringComparison.Ordinal);
        Assert.Contains("lenis.min.js", html, StringComparison.Ordinal);
        Assert.Contains("assets/style.css", html, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateHeader_WithCustomLinksAndHamburger_RendersEscapedBrandAndPrefixedUrls()
    {
        var layout = new CloneLayoutInfo
        {
            NavLinks =
            [
                new NavLinkInfo { Label = "Pricing <Now>", Url = "/pricing" },
                new NavLinkInfo { Label = "Docs", Url = "https://docs.example.com" }
            ]
        };

        var html = CloneLayoutGenerator.GenerateHeader(
            CloneTokens.Default,
            layout,
            "Acme <Site>",
            new CloneBehaviors { MobileHamburger = true });

        Assert.Contains("Acme &lt;Site&gt;", html, StringComparison.Ordinal);
        Assert.Contains("class=\"hamburger\"", html, StringComparison.Ordinal);
        Assert.Contains("<a href=\"{{ base_url }}/pricing\">Pricing &lt;Now&gt;</a>", html, StringComparison.Ordinal);
        Assert.Contains("<a href=\"https://docs.example.com\">Docs</a>", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Home</a>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateHeader_WithoutCustomLinks_UsesFallbackNavigationAndSiteTitle()
    {
        var html = CloneLayoutGenerator.GenerateHeader(CloneTokens.Default, CloneLayoutInfo.Default, siteName: null);

        Assert.Contains("{{ site.title }}", html, StringComparison.Ordinal);
        Assert.Contains("<a href=\"{{ base_url }}/\">Home</a>", html, StringComparison.Ordinal);
        Assert.Contains("<a href=\"{{ base_url }}/blog/\">Blog</a>", html, StringComparison.Ordinal);
        Assert.Contains("<a href=\"{{ base_url }}/pages/\">Pages</a>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"hamburger\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateFooter_WithFooterLinks_UsesFallbackTextAndLinkLabels()
    {
        var layout = new CloneLayoutInfo
        {
            FooterLinks =
            [
                new FooterLinkInfo { Label = null, Url = "https://status.example.com" },
                new FooterLinkInfo { Label = null, Url = null }
            ]
        };

        var html = CloneLayoutGenerator.GenerateFooter(layout, brand: null);

        Assert.Contains("{{ site.params.footer_text ?? site.title }}", html, StringComparison.Ordinal);
        Assert.Contains("<a href=\"https://status.example.com\" target=\"_blank\" rel=\"noopener\">https://status.example.com</a>", html, StringComparison.Ordinal);
        Assert.Contains("<a href=\"#\" target=\"_blank\" rel=\"noopener\">Link</a>", html, StringComparison.Ordinal);
        Assert.Contains("Powered by", html, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateNavLinks_TruncatesAfterEightEntries()
    {
        var links = Enumerable.Range(1, 9)
            .Select(i => new NavLinkInfo { Label = $"Link {i}", Url = $"/item-{i}" })
            .ToList();

        var html = CloneLayoutGenerator.GenerateNavLinks(links);

        Assert.Contains("Link 1", html, StringComparison.Ordinal);
        Assert.Contains("Link 8", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Link 9", html, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateNavLinks_WhenEmpty_UsesFallbackLinks()
    {
        var html = CloneLayoutGenerator.GenerateNavLinks([]);

        Assert.Contains("<a href=\"{{ base_url }}/\">Home</a>", html, StringComparison.Ordinal);
        Assert.Contains("<a href=\"{{ base_url }}/blog/\">Blog</a>", html, StringComparison.Ordinal);
        Assert.Contains("<a href=\"{{ base_url }}/pages/\">Pages</a>", html, StringComparison.Ordinal);
    }
}
