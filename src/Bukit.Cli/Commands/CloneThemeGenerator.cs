using System.Text;

namespace Bukit.Cli.Commands;

internal static class CloneThemeGenerator
{
    public static void WriteTo(string rootDir, string themeName, CloneTokens tokens, CloneLayoutInfo layout, string? brand = null)
    {
        var css = GenerateStyleCss(tokens);
        WriteFile(rootDir, $"themes/{themeName}/assets/style.css", css);

        WriteFile(rootDir, $"themes/{themeName}/layouts/layouts/base.html", GenerateBaseLayout(tokens));
        WriteFile(rootDir, $"themes/{themeName}/layouts/partials/header.html", GenerateHeader(tokens, layout, brand));
        WriteFile(rootDir, $"themes/{themeName}/layouts/partials/footer.html", GenerateFooter(layout, brand));
        WriteFile(rootDir, $"themes/{themeName}/layouts/partials/list-card.html", StarterThemeScaffold.ListCardPartial);
        WriteFile(rootDir, $"themes/{themeName}/layouts/partials/pagination-nav.html", StarterThemeScaffold.PaginationNavPartial);

        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/index.html", GenerateIndex(tokens, layout, brand));
        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/page.html", StarterThemeScaffold.PageTemplate);
        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/post.html", StarterThemeScaffold.PostTemplate);
        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/list.html", StarterThemeScaffold.ListTemplate);
        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/pagination.html", StarterThemeScaffold.PaginationTemplate);
        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/taxonomy-index.html", StarterThemeScaffold.TaxonomyIndexTemplate);
        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/taxonomy-term.html", StarterThemeScaffold.TaxonomyTermTemplate);
        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/search.html", StarterThemeScaffold.SearchTemplate);
        WriteFile(rootDir, $"themes/{themeName}/layouts/bukit.templates.yaml", StarterThemeScaffold.TemplateCapabilities);
    }

    internal static string GenerateStyleCss(CloneTokens t)
    {
        var bg = C(t.Bg, "#fbfaf8");
        var surface = C(t.Surface, "#ffffff");
        var surfaceMuted = C(t.SurfaceMuted, "#f3f1ed");
        var text = C(t.Text, "#202124");
        var muted = C(t.Muted, "#66615b");
        var border = C(t.Border, "#ded9d0");
        var primary = C(t.Primary, "#0b5fff");
        var primaryStrong = C(t.PrimaryStrong, "#0846b8");
        var accent = C(t.Accent, "#0f7b6c");
        var radius = C(t.Radius, "8px");
        var contentMax = C(t.ContentMax, "760px");
        var wideMax = C(t.WideMax, "1080px");
        var shadow = C(t.Shadow, "0 16px 40px rgba(32, 33, 36, 0.08)");
        var cardShadow = C(t.CardShadow, shadow);
        var modalShadow = C(t.ModalShadow, "0 24px 80px rgba(32, 33, 36, 0.18)");
        var dropdownShadow = C(t.DropdownShadow, "0 8px 24px rgba(32, 33, 36, 0.12)");
        var navPad = C(t.NavPadding, "18px 24px");
        var containerPad = C(t.ContainerPadding, "42px 24px 64px");
        var sectionGap = C(t.SectionGap, "34px");
        var spacingScale = t.SpacingScale;
        var fontFamily = C(t.FontFamily, "system-ui, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, Helvetica, Arial, \"Noto Sans\", sans-serif");
        var hFontFamily = C(t.HeadingFontFamily, fontFamily);
        var codeFontFamily = C(t.CodeFontFamily, "\"SFMono-Regular\", Consolas, \"Liberation Mono\", monospace");
        var bpMobile = t.ResponsiveBreakpoints?.Mobile ?? "680px";
        var bpTablet = t.ResponsiveBreakpoints?.Tablet ?? "1024px";
        var bpDesktop = t.ResponsiveBreakpoints?.Desktop ?? "1440px";

        var spacingVars = new StringBuilder();
        if (spacingScale is not null)
        {
            AddVar(spacingVars, "--space-xs", spacingScale.Xs);
            AddVar(spacingVars, "--space-sm", spacingScale.Sm);
            AddVar(spacingVars, "--space-md", spacingScale.Md);
            AddVar(spacingVars, "--space-lg", spacingScale.Lg);
            AddVar(spacingVars, "--space-xl", spacingScale.Xl);
        }

        return $$"""
:root {
  color-scheme: light;
  --bg: {{bg}};
  --surface: {{surface}};
  --surface-muted: {{surfaceMuted}};
  --text: {{text}};
  --muted: {{muted}};
  --border: {{border}};
  --primary: {{primary}};
  --primary-strong: {{primaryStrong}};
  --accent: {{accent}};
  --shadow: {{shadow}};
  --card-shadow: {{cardShadow}};
  --modal-shadow: {{modalShadow}};
  --dropdown-shadow: {{dropdownShadow}};
  --radius: {{radius}};
  --content: {{contentMax}};
  --wide: {{wideMax}};
  --nav-padding: {{navPad}};
  --container-padding: {{containerPad}};
  --section-gap: {{sectionGap}};
  --bp-mobile: {{bpMobile}};
  --bp-tablet: {{bpTablet}};
  --bp-desktop: {{bpDesktop}};
{{spacingVars}}
}

* { box-sizing: border-box; }

html { background: var(--bg); }

body {
  margin: 0;
  font-family: {{fontFamily}};
  color: var(--text);
  background: linear-gradient(180deg, #fff 0, var(--bg) 360px);
  line-height: 1.65;
}

h1, h2, h3, h4, h5, h6 { font-family: {{hFontFamily}}; }

a { color: var(--primary); text-decoration: none; }
a:hover { color: var(--primary-strong); text-decoration: underline; }

img { max-width: 100%; height: auto; }

.site-header {
  border-bottom: 1px solid var(--border);
  background: rgba(255, 255, 255, 0.86);
}

.nav {
  max-width: var(--wide);
  margin: 0 auto;
  padding: var(--nav-padding);
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 20px;
}

.brand { color: var(--text); font-weight: 750; letter-spacing: 0; }

.nav-links { display: flex; flex-wrap: wrap; justify-content: flex-end; gap: 14px; }
.nav-links a { color: var(--muted); font-size: 0.95rem; }

.container { max-width: var(--wide); margin: 0 auto; padding: var(--container-padding); }

.hero { max-width: 860px; padding: 28px 0 34px; }

.hero-cta {
  display: inline-flex; align-items: center; min-height: 42px;
  margin-top: 20px; padding: 0 24px; border: none;
  border-radius: var(--radius); background: var(--primary); color: #fff;
  font: inherit; font-weight: 700; text-decoration: none; cursor: pointer;
}
.hero-cta:hover { background: var(--primary-strong); color: #fff; text-decoration: none; }

.eyebrow {
  margin: 0 0 10px; color: var(--accent);
  font-size: 0.82rem; font-weight: 700;
  letter-spacing: 0.08em; text-transform: uppercase;
}

.hero h1, .page-header h1, .article-header h1 {
  margin: 0; color: var(--text);
  font-size: clamp(2rem, 5vw, 4.2rem); line-height: 1.05; letter-spacing: 0;
}

.hero p, .page-header p, .article-summary {
  max-width: 720px; color: var(--muted); font-size: 1.08rem;
}

.section-heading {
  margin: var(--section-gap) 0 16px; font-size: 0.88rem; font-weight: 750;
  letter-spacing: 0.08em; text-transform: uppercase; color: var(--muted);
}

.card-list { display: grid; gap: 14px; margin: 0; padding: 0; list-style: none; }

.card {
  display: block; padding: 20px; border: 1px solid var(--border);
  border-radius: var(--radius); background: var(--surface); box-shadow: var(--card-shadow);
}

.card-title { margin: 0 0 6px; font-size: 1.18rem; line-height: 1.3; }
.card-title a { color: var(--text); }

.meta { display: flex; flex-wrap: wrap; gap: 8px; margin: 0 0 10px; color: var(--muted); font-size: 0.9rem; }
.summary { margin: 0; color: var(--muted); }

.article { max-width: var(--content); margin: 0 auto; }
.article-header, .page-header { margin-bottom: 30px; }
.content { font-size: 1.02rem; }

.content h1, .content h2, .content h3 { margin-top: 1.7em; line-height: 1.2; }
.content p, .content ul, .content ol { margin: 1em 0; }

.content pre, pre {
  overflow-x: auto; padding: 16px; border-radius: var(--radius);
  background: #1f2937; color: #f8fafc; font-size: 0.92rem;
}

pre code, code { font-family: {{codeFontFamily}}; }

:not(pre) > code {
  padding: 0.12em 0.35em; border-radius: 4px; background: var(--surface-muted);
}

blockquote {
  margin: 1.2em 0; padding: 0.1em 0 0.1em 18px;
  border-left: 4px solid var(--primary); color: var(--muted);
}

table { width: 100%; border-collapse: collapse; margin: 18px 0; background: var(--surface); }
th, td { padding: 10px 12px; border: 1px solid var(--border); text-align: left; }
th { background: var(--surface-muted); }

figure { margin: 20px 0; }
figcaption { margin-top: 8px; color: var(--muted); font-size: 0.9rem; text-align: center; }

.callout {
  display: flex; gap: 12px; margin: 16px 0; padding: 16px;
  border: 1px solid var(--border); border-radius: var(--radius); background: var(--surface-muted);
}
.callout-icon { flex: 0 0 auto; font-size: 1.25rem; }
.callout-content { min-width: 0; }

.to-do { display: flex; align-items: flex-start; gap: 8px; padding: 4px 0; }
.to-do input[type="checkbox"] { margin-top: 6px; }

a.bookmark {
  display: block; margin: 12px 0; padding: 14px 16px;
  border: 1px solid var(--border); border-radius: var(--radius);
  background: var(--surface); color: inherit;
}

.video-embed { position: relative; height: 0; margin: 18px 0; overflow: hidden; padding-bottom: 56.25%; }
.video-embed iframe { position: absolute; inset: 0; width: 100%; height: 100%; border: 0; }

.math-block { overflow-x: auto; padding: 16px 0; text-align: center; }

.notion-gray { color: #787774; }
.notion-brown { color: #64473a; }
.notion-orange { color: #d9730d; }
.notion-yellow { color: #b38700; }
.notion-green { color: #0f7b6c; }
.notion-blue { color: #0b6e99; }
.notion-purple { color: #6940a5; }
.notion-pink { color: #ad1a72; }
.notion-red { color: #d92d20; }
.notion-gray_background { background-color: #f1f1ef; }
.notion-brown_background { background-color: #f4eeee; }
.notion-orange_background { background-color: #fbecdd; }
.notion-yellow_background { background-color: #fbf3db; }
.notion-green_background { background-color: #edf3ec; }
.notion-blue_background { background-color: #e7f3f8; }
.notion-purple_background { background-color: #f6f3f9; }
.notion-pink_background { background-color: #f9f0f5; }
.notion-red_background { background-color: #fdebec; }

.notion-columns { display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 18px; margin: 16px 0; }
.notion-column, .callout-children, .to-do-children { min-width: 0; }

.pagination {
  display: flex; align-items: center; justify-content: space-between; gap: 12px;
  margin-top: 28px; padding-top: 18px; border-top: 1px solid var(--border);
}

.search-form { display: flex; gap: 10px; margin: 24px 0; }
.search-form input {
  flex: 1; min-width: 0; padding: 10px 12px;
  border: 1px solid var(--border); border-radius: var(--radius); font: inherit;
}

button, .button {
  display: inline-flex; align-items: center; justify-content: center;
  min-height: 42px; padding: 0 16px; border: 1px solid var(--primary);
  border-radius: var(--radius); background: var(--primary); color: #fff;
  font: inherit; font-weight: 700; cursor: pointer;
}
button:hover, .button:hover {
  background: var(--primary-strong); color: #fff; text-decoration: none;
}

.term-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 12px; margin: 0; padding: 0; list-style: none; }
.term-card { padding: 16px; border: 1px solid var(--border); border-radius: var(--radius); background: var(--surface); box-shadow: var(--card-shadow); }

.site-footer { border-top: 1px solid var(--border); color: var(--muted); background: var(--surface); }
.footer-inner {
  max-width: var(--wide); margin: 0 auto; padding: 24px;
  display: flex; flex-wrap: wrap; justify-content: space-between; gap: 12px;
}
.footer-links { display: flex; flex-wrap: wrap; gap: 16px; }
.footer-links a { color: var(--muted); }
.footer-links a:hover { color: var(--primary); }

@media (max-width: {{bpMobile}}) {
  .nav, .footer-inner, .pagination, .search-form { align-items: stretch; flex-direction: column; }
  .nav-links { justify-content: flex-start; }
  .container { padding: 30px 18px 48px; }
  .card { padding: 16px; }
}

@media (min-width: {{bpTablet}}) and (max-width: calc({{bpDesktop}} - 1px)) {
  .hero { max-width: 720px; }
}
""";
    }

    internal static string GenerateBaseLayout(CloneTokens t)
    {
        var fontBlock = string.IsNullOrWhiteSpace(t.GoogleFontsUrl)
            ? ""
            : $"  <link rel=\"preconnect\" href=\"https://fonts.googleapis.com\">\n  <link rel=\"preconnect\" href=\"https://fonts.gstatic.com\" crossorigin>\n  <link href=\"{t.GoogleFontsUrl}\" rel=\"stylesheet\">\n";

        var themeAssets = fontBlock +
            "  <link rel=\"stylesheet\" href=\"{{ site.base_url }}/assets/style.css\" />\n";

        var template = """
<!DOCTYPE html>
<html lang="{{ site.language }}">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>{{ if page.seo }}{{ page.seo.title }}{{ else }}{{ page.title }}{{ end }}</title>
  <link rel="alternate" type="application/rss+xml" href="{{ site.base_url }}/rss.xml" />
  <link rel="sitemap" type="application/xml" href="{{ site.base_url }}/sitemap.xml" />
__ASSETS__</head>
<body>
  {{ include "partials/header.html" }}
  <main class="container">
    {{ content }}
  </main>
  {{ include "partials/footer.html" }}
</body>
</html>
""";

        return template.Replace("__ASSETS__", themeAssets);
    }

    internal static string GenerateIndex(CloneTokens t, CloneLayoutInfo layout, string? brand)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{% layout \"layouts/base.html\" %}");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(layout.HeroHeading))
        {
            sb.AppendLine("<section class=\"hero\">");
            sb.AppendLine($"  <p class=\"eyebrow\">{Esc(layout.SiteTitle ?? brand ?? "Site")}</p>");
            sb.AppendLine($"  <h1>{Esc(layout.HeroHeading)}</h1>");
            if (!string.IsNullOrWhiteSpace(layout.HeroSubtext))
                sb.AppendLine($"  <p>{Esc(layout.HeroSubtext)}</p>");

            if (layout.HasHeroCta && !string.IsNullOrWhiteSpace(layout.HeroCtaText))
            {
                var ctaUrl = Esc(layout.HeroCtaUrl ?? "#");
                sb.AppendLine($"  <a class=\"hero-cta\" href=\"{ctaUrl}\">{Esc(layout.HeroCtaText)}</a>");
            }

            sb.AppendLine("</section>");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("<section class=\"hero\">");
            sb.AppendLine("  <h1>{{ site.title }}</h1>");
            sb.AppendLine("  {{ if site.description }}<p>{{ site.description }}</p>{{ end }}");
            sb.AppendLine("</section>");
            sb.AppendLine();
        }

        if (layout.HasFeaturesSection)
        {
            sb.AppendLine("{{ if site.modules && site.modules.features }}");
            sb.AppendLine("<section>");
            sb.AppendLine("  <h2 class=\"section-heading\">Featured</h2>");
            sb.AppendLine("  <ul class=\"card-list\">");
            sb.AppendLine("  {{ for feature in site.modules.features }}");
            sb.AppendLine("    <li class=\"card\">");
            sb.AppendLine("      <h2 class=\"card-title\">{{ feature.title }}</h2>");
            sb.AppendLine("      {{ if feature.fields && feature.fields.desc }}<p class=\"summary\">{{ feature.fields.desc.value }}</p>{{ end }}");
            sb.AppendLine("    </li>");
            sb.AppendLine("  {{ end }}");
            sb.AppendLine("  </ul>");
            sb.AppendLine("</section>");
            sb.AppendLine("{{ end }}");
            sb.AppendLine();
        }

        sb.AppendLine("<section>");
        sb.AppendLine("  <h2 class=\"section-heading\">Latest content</h2>");
        sb.AppendLine("  <ul class=\"card-list\">");
        sb.AppendLine("  {{ for p in pages }}");
        sb.AppendLine("    {{ item = p }}");
        sb.AppendLine("    {{ include \"partials/list-card.html\" }}");
        sb.AppendLine("  {{ end }}");
        sb.AppendLine("  </ul>");
        sb.AppendLine("</section>");

        return sb.ToString();
    }

    internal static string GenerateHeader(CloneTokens t, CloneLayoutInfo layout, string? siteName)
    {
        var brandText = string.IsNullOrWhiteSpace(siteName) ? "{{ site.title }}" : Esc(siteName);
        var navLinksHtml = layout.NavLinks.Count > 0
            ? GenerateNavLinks(layout.NavLinks)
            : """
        <a href="{{ site.base_url }}/">Home</a>
        <a href="{{ site.base_url }}/blog/">Blog</a>
        <a href="{{ site.base_url }}/pages/">Pages</a>
""";

        var template = """
<header class="site-header">
  <nav class="nav" aria-label="Primary navigation">
    <a class="brand" href="{{ site.base_url }}/">
      {{ if site.params && site.params.brand }}{{ site.params.brand }}{{ else }}__BRAND__{{ end }}
    </a>
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
            .Replace("__NAV_LINKS__", navLinksHtml);
    }

    internal static string GenerateFooter(CloneLayoutInfo layout, string? brand)
    {
        var footerText = string.IsNullOrWhiteSpace(brand)
            ? "{{ site.params.footer_text ?? site.title }}"
            : Esc(brand);

        var linksHtml = layout.FooterLinks.Count > 0
            ? "  <div class=\"footer-links\">\n" +
              string.Join("\n", layout.FooterLinks.Select(l =>
                  $"    <a href=\"{Esc(l.Url ?? "#")}\" target=\"_blank\" rel=\"noopener\">{Esc(l.Label ?? l.Url ?? "Link")}</a>")) +
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

    private static string GenerateNavLinks(List<NavLinkInfo> links)
    {
        if (links.Count == 0)
        {
            return """
        <a href="{{ site.base_url }}/">Home</a>
        <a href="{{ site.base_url }}/blog/">Blog</a>
        <a href="{{ site.base_url }}/pages/">Pages</a>
""";
        }

        var sb = new StringBuilder();
        foreach (var link in links.Take(8))
        {
            var label = Esc(link.Label ?? "Link");
            var url = Esc(link.Url ?? "#");
            sb.AppendLine($"        <a href=\"{url}\">{label}</a>");
        }
        return sb.ToString().TrimEnd('\r', '\n');
    }

    private static string C(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static void AddVar(StringBuilder sb, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            sb.AppendLine($"  {name}: {value};");
    }

    private static string Esc(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private static void WriteFile(string rootDir, string relativePath, string content)
    {
        var path = Path.Combine(rootDir, relativePath);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
