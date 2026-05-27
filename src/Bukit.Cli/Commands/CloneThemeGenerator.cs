using System.Text;

namespace Bukit.Cli.Commands;

internal static class CloneThemeGenerator
{
    public static CloneGenerationSummary WriteTo(string rootDir, string themeName, CloneTokens tokens, CloneLayoutInfo layout, string? brand = null, CloneBehaviors? behaviors = null, List<CloneIcon>? icons = null, List<CloneAsset>? assets = null)
    {
        var fileCount = 0;
        var warnings = new List<string>();

        var css = CloneStyleSheetGenerator.GenerateStyleCss(tokens);
        if (behaviors is not null && behaviors.HasAnyCssBehavior)
        {
            css += "\n" + CloneBehaviorGenerator.GenerateBehaviorCss(behaviors, tokens);
        }
        WriteFile(rootDir, $"themes/{themeName}/assets/style.css", css);
        fileCount++;

        var baseLayout = GenerateBaseLayout(tokens, behaviors);
        WriteFile(rootDir, $"themes/{themeName}/layouts/layouts/base.html", baseLayout);
        fileCount++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/partials/header.html", GenerateHeader(tokens, layout, brand, behaviors));
        fileCount++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/partials/footer.html", GenerateFooter(layout, brand));
        fileCount++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/partials/list-card.html", ThemeTemplateResource.Get("ListCardPartial"));
        fileCount++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/partials/pagination-nav.html", ThemeTemplateResource.Get("PaginationNavPartial"));
        fileCount++;

        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/index.html", GenerateIndex(tokens, layout, brand, warnings));
        fileCount++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/page.html", ThemeTemplateResource.Get("PageTemplate"));
        fileCount++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/post.html", ThemeTemplateResource.Get("PostTemplate"));
        fileCount++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/list.html", ThemeTemplateResource.Get("ListTemplate"));
        fileCount++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/pagination.html", ThemeTemplateResource.Get("PaginationTemplate"));
        fileCount++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/taxonomy-index.html", ThemeTemplateResource.Get("TaxonomyIndexTemplate"));
        fileCount++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/taxonomy-term.html", ThemeTemplateResource.Get("TaxonomyTermTemplate"));
        fileCount++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/search.html", ThemeTemplateResource.Get("SearchTemplate"));
        fileCount++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/bukit.templates.yaml", ThemeTemplateResource.Get("TemplateCapabilities"));
        fileCount++;

        var themeYaml = GenerateThemeYaml(themeName, tokens, layout, brand, behaviors);
        WriteFile(rootDir, $"themes/{themeName}/theme.yaml", themeYaml);
        fileCount++;

        if (behaviors is not null && behaviors.HasAnyJsBehavior)
        {
            WriteFile(rootDir, $"themes/{themeName}/assets/behaviors.js", CloneBehaviorGenerator.GenerateBehaviorsJs(behaviors));
            fileCount++;
        }

        if (behaviors?.HasModal == true)
        {
            WriteFile(rootDir, $"themes/{themeName}/layouts/partials/modal.html", ModalPartial);
            fileCount++;
        }
        if (behaviors?.HasDropdown == true)
        {
            WriteFile(rootDir, $"themes/{themeName}/layouts/partials/dropdown.html", DropdownPartial);
            fileCount++;
        }
        if (behaviors?.HasTabs == true)
        {
            WriteFile(rootDir, $"themes/{themeName}/layouts/partials/tabs.html", TabsPartial);
            fileCount++;
        }

        var iconCount = 0;
        if (icons is { Count: > 0 })
        {
            var iconsDir = Path.Combine(rootDir, $"themes/{themeName}/assets/icons");
            Directory.CreateDirectory(iconsDir);
            foreach (var icon in icons)
            {
                if (string.IsNullOrWhiteSpace(icon.Svg)) continue;
                var safeName = SanitizeFileName(icon.Name);
                var filePath = Path.Combine(iconsDir, $"{safeName}.svg");
                File.WriteAllText(filePath, icon.Svg, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                iconCount++;
            }
        }

        var assetCount = 0;
        if (assets is { Count: > 0 })
        {
            var assetsDir = Path.Combine(rootDir, $"themes/{themeName}/assets/images");
            Directory.CreateDirectory(assetsDir);
            assetCount = assets.Count;
        }

        var behaviorCount = CloneBehaviorGenerator.CountBehaviors(behaviors);
        var sectionCount = layout.ExtraSections.Count;

        return new CloneGenerationSummary
        {
            FileCount = fileCount,
            BehaviorCount = behaviorCount,
            IconCount = iconCount,
            AssetCount = assetCount,
            SectionCount = sectionCount,
            Warnings = warnings
        };
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "icon";
        var sb = new StringBuilder();
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c) || c is '-' or '_' or '.')
                sb.Append(c);
            else
                sb.Append('_');
        }
        return sb.Length > 0 ? sb.ToString() : "icon";
    }

    private static string GenerateThemeYaml(string themeName, CloneTokens tokens, CloneLayoutInfo layout, string? brand, CloneBehaviors? behaviors)
    {
        var author = brand ?? "Bukit";
        var tags = new List<string> { "cloned" };
        if (behaviors?.DarkModeToggle == true) tags.Add("dark-mode");
        if (behaviors?.StickyHeader == true) tags.Add("sticky-header");
        if (behaviors?.MobileHamburger == true) tags.Add("responsive");

        var tagsYaml = "[" + string.Join(", ", tags) + "]";

        var sb = new StringBuilder();
        sb.AppendLine($"name: {themeName}");
        sb.AppendLine("version: 1.0.0");
        sb.AppendLine($"description: Custom theme generated by bukit clone");
        sb.AppendLine($"author: {author}");
        sb.AppendLine("license: MIT");
        sb.AppendLine($"tags: {tagsYaml}");
        sb.AppendLine("params:");
        sb.AppendLine("  - key: brand");
        sb.AppendLine("    label: Site Brand");
        sb.AppendLine("    type: string");
        sb.AppendLine($"    default: {author}");
        sb.AppendLine("  - key: primary_color");
        sb.AppendLine("    label: Primary Color");
        sb.AppendLine("    type: color");
        sb.AppendLine($"    default: \"{tokens.Primary ?? "#0b5fff"}\"");
        sb.AppendLine("  - key: accent_color");
        sb.AppendLine("    label: Accent Color");
        sb.AppendLine("    type: color");
        sb.AppendLine($"    default: \"{tokens.Accent ?? "#0f7b6c"}\"");
        sb.AppendLine("  - key: footer_text");
        sb.AppendLine("    label: Footer Text");
        sb.AppendLine("    type: string");
        sb.AppendLine($"    default: {author}");
        return sb.ToString();
    }

    internal const string ModalPartial = """
{{ if site.modules && site.modules.modal }}
<div class="modal-overlay hidden" id="site-modal" role="dialog" aria-modal="true">
  <div class="modal-container">
    <div class="modal-header">
      <span class="modal-title">{{ site.modules.modal.title }}</span>
      <button class="modal-close" aria-label="Close modal">&times;</button>
    </div>
    <div class="modal-body">
      {{ for item in site.modules.modal.items }}
        {{ if item.fields && item.fields.desc }}
          <p>{{ item.fields.desc.value }}</p>
        {{ else }}
          <p>{{ item.title }}</p>
        {{ end }}
      {{ end }}
    </div>
  </div>
</div>
{{ end }}
""";

    internal const string DropdownPartial = """
<div class="dropdown">
  <button class="dropdown-trigger" aria-haspopup="true" aria-expanded="false">
    <span class="dropdown-label">Menu</span>
    <span class="dropdown-caret">▾</span>
  </button>
  <div class="dropdown-menu" role="menu" hidden>
    {{ for item in dropdown_items }}
      <a href="{{ item.url }}" class="dropdown-item" role="menuitem">{{ item.label }}</a>
    {{ end }}
  </div>
</div>
""";

    internal const string TabsPartial = """
{{ if site.modules && site.modules.tabs }}
<div class="tabs">
  <div class="tab-nav" role="tablist">
    {{ for tab in site.modules.tabs }}
      <button class="tab-btn" role="tab" aria-selected="false" aria-controls="tab-panel-{{ for.rindex }}">
        {{ tab.title }}
      </button>
    {{ end }}
  </div>
  {{ for tab in site.modules.tabs }}
    <div class="tab-panel hidden" role="tabpanel" id="tab-panel-{{ for.rindex }}">
      {{ if tab.fields && tab.fields.desc }}
        {{ tab.fields.desc.value }}
      {{ end }}
    </div>
  {{ end }}
</div>
{{ end }}
""";

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

    internal static string GenerateIndex(CloneTokens t, CloneLayoutInfo layout, string? brand, List<string>? warnings = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{% layout \"layouts/base.html\" %}");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(layout.HeroHeading))
        {
            sb.AppendLine("<section class=\"hero\">");
            sb.AppendLine($"  <p class=\"eyebrow\">{CloneStyleSheetGenerator.Esc(layout.SiteTitle ?? brand ?? "Site")}</p>");
            sb.AppendLine($"  <h1>{CloneStyleSheetGenerator.Esc(layout.HeroHeading)}</h1>");
            if (!string.IsNullOrWhiteSpace(layout.HeroSubtext))
                sb.AppendLine($"  <p>{CloneStyleSheetGenerator.Esc(layout.HeroSubtext)}</p>");

            if (layout.HasHeroCta && !string.IsNullOrWhiteSpace(layout.HeroCtaText))
            {
                var ctaUrl = CloneStyleSheetGenerator.Esc(layout.HeroCtaUrl ?? "#");
                sb.AppendLine($"  <a class=\"hero-cta\" href=\"{ctaUrl}\">{CloneStyleSheetGenerator.Esc(layout.HeroCtaText)}</a>");
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

        if (layout.HasCTASection)
        {
            sb.AppendLine("{{ if site.modules && site.modules.call_to_action }}");
            sb.AppendLine("{{ cta = site.modules.call_to_action[0] }}");
            sb.AppendLine("<section class=\"cta-section\">");
            sb.AppendLine("  <h2 class=\"section-heading\">{{ cta.title }}</h2>");
            sb.AppendLine("  {{ if cta.fields && cta.fields.desc }}");
            sb.AppendLine("  <p>{{ cta.fields.desc.value }}</p>");
            sb.AppendLine("  {{ end }}");
            sb.AppendLine("</section>");
            sb.AppendLine("{{ end }}");
            sb.AppendLine();
        }

        foreach (var section in layout.ExtraSections)
        {
            if (section.HasStates)
            {
                GenerateStateSection(sb, section, warnings);
            }
            else
            {
                GenerateStaticSection(sb, section);
            }
        }

        sb.AppendLine("<section>");
        sb.AppendLine("  <h2 class=\"section-heading\">{{ if site.params && site.params.latest_heading }}{{ site.params.latest_heading }}{{ else }}Latest content{{ end }}</h2>");
        sb.AppendLine("  <ul class=\"card-list\">");
        sb.AppendLine("  {{ for p in pages }}");
        sb.AppendLine("    {{ item = p }}");
        sb.AppendLine("    {{ include \"partials/list-card.html\" }}");
        sb.AppendLine("  {{ end }}");
        sb.AppendLine("  </ul>");
        sb.AppendLine("</section>");

        if (layout.ExtraSections.Any(s => s.HasStates))
        {
            sb.AppendLine();
            sb.AppendLine("<script>(function(){document.querySelectorAll('.state-section').forEach(function(sec){var tabs=sec.querySelectorAll('.state-tab');tabs.forEach(function(tab){tab.addEventListener('click',function(){var panelId=tab.getAttribute('aria-controls');tabs.forEach(function(t){t.setAttribute('aria-selected','false');});tab.setAttribute('aria-selected','true');sec.querySelectorAll('.state-panel').forEach(function(p){p.classList.add('hidden');});var panel=document.getElementById(panelId);if(panel)panel.classList.remove('hidden');});});});})();</script>");
        }

        return sb.ToString();
    }

    private static void GenerateStaticSection(StringBuilder sb, SectionInfo section)
    {
        var responsive = section.HasResponsive ? section.Responsive! : null;
        var cls = responsive is not null ? " class=\"sec-r-" + Math.Abs(section.Heading?.GetHashCode() ?? section.GetHashCode()) + "\"" : "";
        if (responsive is not null)
            sb.Append(GenerateResponsiveCss(section));
        sb.AppendLine($"<section{cls}>");
        if (!string.IsNullOrWhiteSpace(section.Heading))
            sb.AppendLine($"  <h2 class=\"section-heading\">{CloneStyleSheetGenerator.Esc(section.Heading)}</h2>");
        if (!string.IsNullOrWhiteSpace(section.ContentHtml))
            sb.AppendLine($"  {section.ContentHtml}");
        foreach (var imgUrl in section.ImageUrls)
        {
            sb.AppendLine($"  <img src=\"{CloneStyleSheetGenerator.Esc(imgUrl)}\" alt=\"\" loading=\"lazy\" />");
        }
        sb.AppendLine("</section>");
        sb.AppendLine();
    }

    private static string GenerateResponsiveCss(SectionInfo section)
    {
        var r = section.Responsive!;
        var className = "sec-r-" + Math.Abs(section.Heading?.GetHashCode() ?? section.GetHashCode());
        var sb = new StringBuilder();
        sb.AppendLine($"<style>");
        if (r.MaxWidthDesktop is not null)
            sb.AppendLine($"  .{className} {{ max-width: {r.MaxWidthDesktop}; }}");
        if (r.ColumnsDesktop is not null)
            sb.AppendLine($"  .{className} {{ display: grid; grid-template-columns: {r.ColumnsDesktop}; gap: 16px; }}");
        if (r.MaxWidthTablet is not null || r.ColumnsTablet is not null)
        {
            sb.AppendLine("  @media (max-width: var(--bp-tablet)) {");
            if (r.MaxWidthTablet is not null)
                sb.AppendLine($"    .{className} {{ max-width: {r.MaxWidthTablet}; }}");
            if (r.ColumnsTablet is not null)
                sb.AppendLine($"    .{className} {{ grid-template-columns: {r.ColumnsTablet}; }}");
            sb.AppendLine("  }");
        }
        if (r.MaxWidthMobile is not null || r.ColumnsMobile is not null)
        {
            sb.AppendLine("  @media (max-width: var(--bp-mobile)) {");
            if (r.MaxWidthMobile is not null)
                sb.AppendLine($"    .{className} {{ max-width: {r.MaxWidthMobile}; }}");
            if (r.ColumnsMobile is not null)
                sb.AppendLine($"    .{className} {{ grid-template-columns: {r.ColumnsMobile}; }}");
            sb.AppendLine("  }");
        }
        sb.AppendLine("</style>");
        return sb.ToString();
    }

    private static void GenerateStateSection(StringBuilder sb, SectionInfo section, List<string>? warnings)
    {
        if (section.States.Count < 2)
        {
            warnings?.Add($"Skipped multi-state section \"{section.Heading}\": needs at least 2 states.");
            GenerateStaticSection(sb, section);
            return;
        }

        var id = "state-section-" + Math.Abs(section.Heading?.GetHashCode() ?? section.GetHashCode());
        sb.AppendLine("<section class=\"state-section\" data-section-id=\"" + id + "\">");
        if (!string.IsNullOrWhiteSpace(section.Heading))
            sb.AppendLine($"  <h2 class=\"section-heading\">{CloneStyleSheetGenerator.Esc(section.Heading)}</h2>");

        sb.AppendLine("  <div class=\"state-tabs\" role=\"tablist\">");
        for (var i = 0; i < section.States.Count; i++)
        {
            var state = section.States[i];
            var selected = i == 0 ? "true" : "false";
            sb.AppendLine($"    <button class=\"state-tab\" role=\"tab\" aria-selected=\"{selected}\" aria-controls=\"{id}-{i}\">{CloneStyleSheetGenerator.Esc(state.Label ?? $"State {i + 1}")}</button>");
        }
        sb.AppendLine("  </div>");

        for (var i = 0; i < section.States.Count; i++)
        {
            var state = section.States[i];
            var hidden = i == 0 ? "" : " hidden";
            sb.AppendLine($"  <div class=\"state-panel{hidden}\" role=\"tabpanel\" id=\"{id}-{i}\">");
            if (!string.IsNullOrWhiteSpace(state.ContentHtml))
                sb.AppendLine($"    {state.ContentHtml}");
            sb.AppendLine("  </div>");
        }

        sb.AppendLine("</section>");
        sb.AppendLine();
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

    private static string GenerateNavLinks(List<NavLinkInfo> links)
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

    private static void WriteFile(string rootDir, string relativePath, string content)
    {
        var path = Path.Combine(rootDir, relativePath);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
