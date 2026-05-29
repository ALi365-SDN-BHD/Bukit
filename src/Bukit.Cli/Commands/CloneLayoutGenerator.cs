using System.Text;

namespace Bukit.Cli.Commands;

internal static class CloneLayoutGenerator
{
    internal static string GenerateBaseLayout(CloneTokens t, CloneBehaviors? behaviors = null)
    {
        var fontBlock = string.IsNullOrWhiteSpace(t.GoogleFontsUrl)
            ? ""
            : $"  <link rel=\"preconnect\" href=\"https://fonts.googleapis.com\">\n  <link rel=\"preconnect\" href=\"https://fonts.gstatic.com\" crossorigin>\n  <link href=\"{t.GoogleFontsUrl}\" rel=\"stylesheet\">\n";

        var externalCssBlock = new StringBuilder();
        if (t.ExternalCssUrls is { Count: > 0 })
        {
            foreach (var url in t.ExternalCssUrls)
            {
                if (!string.IsNullOrWhiteSpace(url))
                    externalCssBlock.AppendLine($"  <link rel=\"stylesheet\" href=\"{url.Trim()}\" />");
            }
        }

        var themeAssets = fontBlock +
            externalCssBlock +
            "  <link rel=\"stylesheet\" href=\"{{ base_url }}/assets/style.css\" />\n";

        var externalJsBlock = new StringBuilder();
        if (t.ExternalJsUrls is { Count: > 0 })
        {
            foreach (var url in t.ExternalJsUrls)
            {
                if (!string.IsNullOrWhiteSpace(url))
                    externalJsBlock.AppendLine($"  <script src=\"{url.Trim()}\" defer></script>");
            }
        }

        var jsBlock = (behaviors is not null && behaviors.HasAnyJsBehavior)
            ? "  <script src=\"{{ base_url }}/assets/behaviors.js\" defer></script>\n"
            : "";

        var lenisTag = (behaviors?.UseLenis == true)
            ? "  <script src=\"https://cdn.jsdelivr.net/npm/lenis@1.1/dist/lenis.min.js\"></script>\n"
            : "";

        var template = """
{{ base_url = site.base_url }}
{{ if base_url == "/" }}{{ base_url = "" }}{{ end }}
<!DOCTYPE html>
<html lang="{{ site.language }}">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>{{ if page.seo }}{{ page.seo.title }}{{ else }}{{ page.title }}{{ end }}</title>
  <link rel="alternate" type="application/rss+xml" href="{{ base_url }}/rss.xml" />
  <link rel="sitemap" type="application/xml" href="{{ base_url }}/sitemap.xml" />
__ASSETS__</head>
<body>
  {{ include "partials/header.html" }}
  <main class="container">
    {{ content }}
  </main>
  {{ include "partials/footer.html" }}
__LENIS____EXTERNAL_JS____BEHAVIORS_JS__</body>
</html>
""";

        return template
            .Replace("__ASSETS__", themeAssets)
            .Replace("__EXTERNAL_JS__", externalJsBlock.ToString())
            .Replace("__BEHAVIORS_JS__", jsBlock)
            .Replace("__LENIS__", lenisTag);
    }

    internal static string GenerateHeader(CloneTokens t, CloneLayoutInfo layout, string? siteName, CloneBehaviors? behaviors = null)
    {
        var brandText = string.IsNullOrWhiteSpace(siteName) ? "{{ site.title }}" : CloneStyleSheetGenerator.Esc(siteName);
        var navLinksHtml = layout.NavLinks.Count > 0
            ? GenerateNavLinks(layout.NavLinks)
            : """
        <a href="{{ base_url }}/">Home</a>
        <a href="{{ base_url }}/blog/">Blog</a>
        <a href="{{ base_url }}/pages/">Pages</a>
""";

        var hamburgerBlock = (behaviors?.MobileHamburger == true)
            ? """
    <button class="hamburger" aria-label="Toggle menu" aria-expanded="false">
      <span class="hamburger-bar"></span>
      <span class="hamburger-bar"></span>
      <span class="hamburger-bar"></span>
    </button>
"""
            : "";

        var template = """
{{ base_url = site.base_url }}
{{ if base_url == "/" }}{{ base_url = "" }}{{ end }}
<header class="site-header">
  <nav class="nav" aria-label="Primary navigation">
    <a class="brand" href="{{ base_url }}/">
      {{ if site.params && site.params.brand }}{{ site.params.brand }}{{ else }}__BRAND__{{ end }}
    </a>
__HAMBURGER__
    <div class="nav-links">
      {{ if site.modules && site.modules.navigation }}
        {{ for item in site.modules.navigation }}
          {{ nav_url = "/" }}
          {{ if item.fields && item.fields.link }}{{ nav_url = item.fields.link.value }}{{ end }}
          <a href="{{ nav_url }}">{{ item.title }}</a>
        {{ end }}
      {{ else }}
__NAV_LINKS__
      {{ end }}
    </div>
  </nav>
</header>
""";

        return template
            .Replace("__BRAND__", brandText)
            .Replace("__NAV_LINKS__", navLinksHtml)
            .Replace("__HAMBURGER__", hamburgerBlock);
    }

    internal static string GenerateFooter(CloneLayoutInfo layout, string? brand)
    {
        var footerText = string.IsNullOrWhiteSpace(brand)
            ? "{{ site.params.footer_text ?? site.title }}"
            : CloneStyleSheetGenerator.Esc(brand);

        var linksHtml = layout.FooterLinks.Count > 0
            ? "  <div class=\"footer-links\">\n" +
              string.Join("\n", layout.FooterLinks.Select(l =>
                  $"    <a href=\"{CloneStyleSheetGenerator.Esc(l.Url ?? "#")}\" target=\"_blank\" rel=\"noopener\">{CloneStyleSheetGenerator.Esc(l.Label ?? l.Url ?? "Link")}</a>")) +
              "\n  </div>"
            : "";

        var template = """
<footer class="site-footer">
  <div class="footer-inner">
    <span>__FOOTER_TEXT__</span>
__LINKS__
    <small>Powered by <a href="https://github.com/ALi365-SDN-BHD/Bukit" target="_blank" rel="noopener">bukit</a></small>
  </div>
</footer>
""";

        return template
            .Replace("__FOOTER_TEXT__", footerText)
            .Replace("__LINKS__", linksHtml);
    }

    internal static string GenerateNavLinks(List<NavLinkInfo> links)
    {
        if (links.Count == 0)
        {
            return """
        <a href="{{ base_url }}/">Home</a>
        <a href="{{ base_url }}/blog/">Blog</a>
        <a href="{{ base_url }}/pages/">Pages</a>
""";
        }

        var sb = new StringBuilder();
        foreach (var link in links.Take(8))
        {
            var label = CloneStyleSheetGenerator.Esc(link.Label ?? "Link");
            var url = CloneStyleSheetGenerator.Esc(link.Url ?? "#");
            var href = url.StartsWith("/", StringComparison.Ordinal) ? "{{ base_url }}" + url : url;
            sb.AppendLine($"        <a href=\"{href}\">{label}</a>");
        }
        return sb.ToString().TrimEnd('\r', '\n');
    }
}
