using System.Text;

namespace Bukit.Cli.Commands;

internal static class CloneThemeGenerator
{
    public static CloneGenerationSummary WriteTo(string rootDir, string themeName, CloneTokens tokens, CloneLayoutInfo layout, string? brand = null, CloneBehaviors? behaviors = null, List<CloneIcon>? icons = null, List<CloneAsset>? assets = null)
    {
        var fileCount = 0;
        var warnings = new List<string>();

        var css = GenerateStyleCss(tokens);
        if (behaviors is not null && behaviors.HasAnyCssBehavior)
        {
            css += "\n" + GenerateBehaviorCss(behaviors, tokens);
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
            WriteFile(rootDir, $"themes/{themeName}/assets/behaviors.js", GenerateBehaviorsJs(behaviors));
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

        var behaviorCount = CountBehaviors(behaviors);
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

    private static int CountBehaviors(CloneBehaviors? b)
    {
        if (b is null) return 0;
        var count = 0;
        if (b.StickyHeader) count++;
        if (b.CardHoverLift) count++;
        if (b.AnimateOnScroll) count++;
        if (b.ScrollShrinkNav) count++;
        if (b.DarkModeToggle) count++;
        if (b.MobileHamburger) count++;
        if (b.SmoothScroll) count++;
        if (b.BackToTop) count++;
        if (b.HasModal) count++;
        if (b.HasDropdown) count++;
        if (b.HasTabs) count++;
        if (b.UseLenis) count++;
        return count;
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
        var fontSizeXs = C(t.FontSizeXs, "0.75rem");
        var fontSizeSm = C(t.FontSizeSm, "0.875rem");
        var fontSizeBase = C(t.FontSizeBase, "1rem");
        var fontSizeLg = C(t.FontSizeLg, "1.125rem");
        var fontSizeXl = C(t.FontSizeXl, "1.25rem");
        var fontSize2xl = C(t.FontSize2xl, "1.5rem");
        var fontSize3xl = C(t.FontSize3xl, "2rem");
        var fontSize4xl = C(t.FontSize4xl, "2.5rem");
        var fontSizeDisplay = C(t.FontSizeDisplay, "clamp(2rem, 5vw, 4.2rem)");
        var fontWeightNormal = C(t.FontWeightNormal, "400");
        var fontWeightBold = C(t.FontWeightBold, "700");
        var lineHeightTight = C(t.LineHeightTight, "1.2");
        var lineHeightNormal = C(t.LineHeightNormal, "1.65");
        var lineHeightRelaxed = C(t.LineHeightRelaxed, "1.8");
        var zHeader = C(t.ZHeader, "100");
        var zDropdown = C(t.ZDropdown, "200");
        var zModal = C(t.ZModal, "300");
        var zTooltip = C(t.ZTooltip, "400");

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
  --font-size-xs: {{fontSizeXs}};
  --font-size-sm: {{fontSizeSm}};
  --font-size-base: {{fontSizeBase}};
  --font-size-lg: {{fontSizeLg}};
  --font-size-xl: {{fontSizeXl}};
  --font-size-2xl: {{fontSize2xl}};
  --font-size-3xl: {{fontSize3xl}};
  --font-size-4xl: {{fontSize4xl}};
  --font-size-display: {{fontSizeDisplay}};
  --font-weight-normal: {{fontWeightNormal}};
  --font-weight-bold: {{fontWeightBold}};
  --line-height-tight: {{lineHeightTight}};
  --line-height-normal: {{lineHeightNormal}};
  --line-height-relaxed: {{lineHeightRelaxed}};
  --z-header: {{zHeader}};
  --z-dropdown: {{zDropdown}};
  --z-modal: {{zModal}};
  --z-tooltip: {{zTooltip}};
}

* { box-sizing: border-box; }

html { background: var(--bg); }

body {
  margin: 0;
  font-family: {{fontFamily}};
  color: var(--text);
  background: linear-gradient(180deg, #fff 0, var(--bg) 360px);
  line-height: var(--line-height-normal);
}

h1, h2, h3, h4, h5, h6 { font-family: {{hFontFamily}}; font-weight: var(--font-weight-bold); }

a { color: var(--primary); text-decoration: none; }
a:hover { color: var(--primary-strong); text-decoration: underline; }

img { max-width: 100%; height: auto; }

.site-header {
  border-bottom: 1px solid var(--border);
  background: rgba(255, 255, 255, 0.86);
  z-index: var(--z-header);
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

.state-tabs { display: flex; gap: 2px; border-bottom: 2px solid var(--border); margin-bottom: 18px; overflow-x: auto; }
.state-tab { padding: 10px 18px; border: none; border-bottom: 2px solid transparent; margin-bottom: -2px; background: none; color: var(--muted); font: inherit; font-weight: 600; cursor: pointer; white-space: nowrap; transition: color 0.15s ease, border-color 0.15s ease; }
.state-tab:hover { color: var(--text); }
.state-tab[aria-selected="true"] { color: var(--primary); border-bottom-color: var(--primary); }
.state-panel { padding: 4px 0; }
.state-panel.hidden { display: none; }
.cta-section { text-align: center; padding: 32px 0; }

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
            sb.AppendLine($"  <h2 class=\"section-heading\">{Esc(section.Heading)}</h2>");
        if (!string.IsNullOrWhiteSpace(section.ContentHtml))
            sb.AppendLine($"  {section.ContentHtml}");
        foreach (var imgUrl in section.ImageUrls)
        {
            sb.AppendLine($"  <img src=\"{Esc(imgUrl)}\" alt=\"\" loading=\"lazy\" />");
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
            sb.AppendLine($"  <h2 class=\"section-heading\">{Esc(section.Heading)}</h2>");

        sb.AppendLine("  <div class=\"state-tabs\" role=\"tablist\">");
        for (var i = 0; i < section.States.Count; i++)
        {
            var state = section.States[i];
            var selected = i == 0 ? "true" : "false";
            sb.AppendLine($"    <button class=\"state-tab\" role=\"tab\" aria-selected=\"{selected}\" aria-controls=\"{id}-{i}\">{Esc(state.Label ?? $"State {i + 1}")}</button>");
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
        var brandText = string.IsNullOrWhiteSpace(siteName) ? "{{ site.title }}" : Esc(siteName);
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

    internal static string GenerateBehaviorCss(CloneBehaviors b, CloneTokens t)
    {
        var sb = new StringBuilder();

        if (b.StickyHeader)
        {
            sb.AppendLine("""
.site-header { position: sticky; top: 0; z-index: 100; }

""");
        }

        if (b.ScrollShrinkNav)
        {
            sb.AppendLine("""
.site-header { transition: transform 0.3s ease; }
.nav-hidden { transform: translateY(-100%); }

""");
        }

        if (b.CardHoverLift)
        {
            var lift = C(t.HoverLift, "3px");
            var shadow = C(t.HoverShadow, "var(--modal-shadow)");
            sb.AppendLine($$"""
.card { transition: transform 0.2s ease, box-shadow 0.2s ease; }
.card:hover { transform: translateY(-{{lift}}); box-shadow: {{shadow}}; }

""");
        }

        if (b.AnimateOnScroll)
        {
            var style = b.AnimationStyle ?? "fadeInUp";
            var animName = style switch
            {
                "slideUp" => "slideUp",
                "scaleIn" => "scaleIn",
                "fadeIn" => "fadeIn",
                _ => "fadeInUp"
            };
            var translateInit = style switch
            {
                "scaleIn" => "scale(0.92)",
                "fadeIn" => "translateY(0)",
                _ => "translateY(20px)"
            };

            switch (style)
            {
                case "slideUp":
                    sb.AppendLine("""
@keyframes slideUp {
  from { opacity: 0; transform: translateY(40px); }
  to   { opacity: 1; transform: translateY(0); }
}

""");
                    break;
                case "scaleIn":
                    sb.AppendLine("""
@keyframes scaleIn {
  from { opacity: 0; transform: scale(0.92); }
  to   { opacity: 1; transform: scale(1); }
}

""");
                    break;
                case "fadeIn":
                    sb.AppendLine("""
@keyframes fadeIn {
  from { opacity: 0; }
  to   { opacity: 1; }
}

""");
                    break;
                default:
                    sb.AppendLine("""
@keyframes fadeInUp {
  from { opacity: 0; transform: translateY(20px); }
  to   { opacity: 1; transform: translateY(0); }
}

""");
                    break;
            }
            sb.AppendLine($$"""
.animate-in { opacity: 0; transform: {{translateInit}}; }
.animate-visible { animation: {{animName}} 0.55s ease forwards; }

""");
        }

        if (b.MobileHamburger)
        {
            sb.AppendLine("""
.hamburger { display: none; flex-direction: column; gap: 5px; padding: 8px; border: none; background: none; cursor: pointer; }
.hamburger-bar { display: block; width: 22px; height: 2.5px; border-radius: 2px; background: var(--text); transition: transform 0.25s ease, opacity 0.25s ease; }

@media (max-width: var(--bp-mobile)) {
  .hamburger { display: flex; }
  .nav-links { display: none; flex-direction: column; width: 100%; gap: 8px; padding-top: 12px; }
  .nav-links.open { display: flex; }
}

""");
        }

        if (b.DarkModeToggle)
        {
            sb.AppendLine("""
.dark-mode-toggle { display: inline-flex; align-items: center; gap: 6px; padding: 6px 10px; border: 1px solid var(--border); border-radius: var(--radius); background: var(--surface); color: var(--text); font: inherit; font-size: 0.88rem; cursor: pointer; }
.dark-mode-toggle:hover { background: var(--surface-muted); }

body.dark { --bg: #1a1a2e; --surface: #16213e; --surface-muted: #0f3460; --text: #eaeaea; --muted: #a0a0b0; --border: #2a2a4a; }
body.dark img { opacity: 0.9; }
body.dark .site-header { background: rgba(22, 33, 62, 0.92); }

""");
        }

        if (b.HasModal)
        {
            sb.AppendLine("""
.modal-overlay { position: fixed; inset: 0; z-index: 200; display: flex; align-items: center; justify-content: center; background: rgba(0,0,0,0.45); opacity: 0; visibility: hidden; transition: opacity 0.25s ease, visibility 0.25s ease; }
.modal-overlay.visible { opacity: 1; visibility: visible; }
.modal-container { max-width: 560px; width: 90%; max-height: 80vh; overflow-y: auto; padding: 28px 32px; border-radius: var(--radius); background: var(--surface); box-shadow: var(--modal-shadow); }
.modal-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 20px; }
.modal-title { font-family: var(--heading-font, inherit); font-size: 1.3rem; font-weight: 700; margin: 0; }
.modal-close { padding: 6px 10px; border: none; background: none; font-size: 1.4rem; cursor: pointer; color: var(--muted); line-height: 1; }
.modal-close:hover { color: var(--text); }
.modal-body p { margin: 0.6em 0; color: var(--muted); }

""");
        }

        if (b.HasDropdown)
        {
            sb.AppendLine("""
.dropdown { position: relative; display: inline-block; }
.dropdown-trigger { display: inline-flex; align-items: center; gap: 6px; padding: 8px 14px; border: 1px solid var(--border); border-radius: var(--radius); background: var(--surface); color: var(--text); font: inherit; cursor: pointer; }
.dropdown-trigger:hover { background: var(--surface-muted); }
.dropdown-caret { font-size: 0.75rem; transition: transform 0.2s ease; }
.dropdown.open .dropdown-caret { transform: rotate(180deg); }
.dropdown-menu { position: absolute; top: calc(100% + 6px); left: 0; min-width: 180px; padding: 6px 0; border: 1px solid var(--border); border-radius: var(--radius); background: var(--surface); box-shadow: var(--dropdown-shadow); z-index: 150; }
.dropdown-item { display: block; padding: 8px 14px; color: var(--text); font-size: 0.92rem; }
.dropdown-item:hover { background: var(--surface-muted); color: var(--primary); }

""");
        }

        if (b.HasTabs)
        {
            sb.AppendLine("""
.tabs { margin: 20px 0; }
.tab-nav { display: flex; gap: 2px; border-bottom: 2px solid var(--border); margin-bottom: 18px; overflow-x: auto; }
.tab-btn { padding: 10px 18px; border: none; border-bottom: 2px solid transparent; margin-bottom: -2px; background: none; color: var(--muted); font: inherit; font-weight: 600; cursor: pointer; white-space: nowrap; transition: color 0.15s ease, border-color 0.15s ease; }
.tab-btn:hover { color: var(--text); }
.tab-btn[aria-selected="true"] { color: var(--primary); border-bottom-color: var(--primary); }
.tab-panel { padding: 4px 0; }
.tab-panel:not(.hidden) { display: block; }

""");
        }

        return sb.ToString().TrimEnd('\r', '\n');
    }

    internal static string GenerateBehaviorsJs(CloneBehaviors b)
    {
        var sb = new StringBuilder();
        sb.AppendLine("(function(){'use strict';");
        sb.AppendLine();

        if (b.ScrollShrinkNav)
        {
            var threshold = b.ScrollThreshold > 0 ? b.ScrollThreshold : 60;
            var reveal = Math.Max(10, threshold / 6);
            sb.AppendLine($"var h=document.querySelector('.site-header');\nvar s=0;\nwindow.addEventListener('scroll',function(){{var n=window.scrollY;if(n>{threshold}&&n>s)h.classList.add('nav-hidden');else if(n<{reveal}||n<s)h.classList.remove('nav-hidden');s=n}},{{passive:true}});\n");
        }

        if (b.MobileHamburger)
        {
            sb.AppendLine("""
var btn=document.querySelector('.hamburger');
var nav=document.querySelector('.nav-links');
if(btn&&nav){btn.addEventListener('click',function(){var o=nav.classList.toggle('open');btn.setAttribute('aria-expanded',String(o));});}

""");
        }

        if (b.DarkModeToggle)
        {
            sb.AppendLine("""
var t=document.createElement('button');
t.className='dark-mode-toggle';
t.textContent='☀️';
t.title='Toggle dark mode';
var hh=document.querySelector('.site-header');
if(hh)hh.appendChild(t);
var stored=localStorage.getItem('theme');
if(stored==='dark')document.body.classList.add('dark');
t.addEventListener('click',function(){var d=document.body.classList.toggle('dark');localStorage.setItem('theme',d?'dark':'light');t.textContent=d?'🌙':'☀️';});

""");
        }

        if (b.SmoothScroll)
        {
            sb.AppendLine("""
document.querySelectorAll('a[href^=\"#\"]').forEach(function(a){a.addEventListener('click',function(e){var id=this.getAttribute('href').slice(1);var el=document.getElementById(id);if(el){e.preventDefault();el.scrollIntoView({behavior:'smooth',block:'start'});}});});

""");
        }

        if (b.BackToTop)
        {
            sb.AppendLine("""
var btt=document.createElement('button');
btt.textContent='↑';
btt.className='back-to-top';
btt.setAttribute('aria-label','Back to top');
btt.style.cssText='position:fixed;bottom:24px;right:24px;width:44px;height:44px;border-radius:50%;border:1px solid var(--border);background:var(--surface);color:var(--text);font-size:1.2rem;cursor:pointer;opacity:0;transition:opacity 0.3s;z-index:90;';
document.body.appendChild(btt);
window.addEventListener('scroll',function(){btt.style.opacity=window.scrollY>400?'1':'0';},{passive:true});
btt.addEventListener('click',function(){window.scrollTo({top:0,behavior:'smooth'});});

""");
        }

        if (b.AnimateOnScroll)
        {
            sb.AppendLine("""
var observer=new IntersectionObserver(function(entries){entries.forEach(function(e){if(e.isIntersecting)e.target.classList.add('animate-visible');});},{threshold:0.15});
document.querySelectorAll('.animate-in').forEach(function(el){observer.observe(el);});

""");
        }

        if (b.HasModal)
        {
            sb.AppendLine("""
var mo=document.getElementById('site-modal');
if(mo){var mc=mo.querySelector('.modal-close');if(mc)mc.addEventListener('click',function(){mo.classList.add('hidden');mo.classList.remove('visible');mo.setAttribute('aria-hidden','true');});mo.addEventListener('click',function(e){if(e.target===mo){mo.classList.add('hidden');mo.classList.remove('visible');mo.setAttribute('aria-hidden','true');}});document.addEventListener('keydown',function(e){if(e.key==='Escape'&&mo.classList.contains('visible')){mo.classList.add('hidden');mo.classList.remove('visible');mo.setAttribute('aria-hidden','true');}});var triggers=document.querySelectorAll('[data-modal-trigger]');triggers.forEach(function(btn){btn.addEventListener('click',function(){mo.classList.remove('hidden');mo.classList.add('visible');mo.setAttribute('aria-hidden','false');});});}

""");
        }

        if (b.HasDropdown)
        {
            sb.AppendLine("""
document.querySelectorAll('.dropdown-trigger').forEach(function(btn){btn.addEventListener('click',function(e){e.stopPropagation();var dd=btn.closest('.dropdown');var menu=dd.querySelector('.dropdown-menu');var open=dd.classList.toggle('open');btn.setAttribute('aria-expanded',String(open));if(menu)menu.hidden=!open;});});
document.addEventListener('click',function(e){document.querySelectorAll('.dropdown.open').forEach(function(dd){if(!dd.contains(e.target)){dd.classList.remove('open');dd.querySelector('.dropdown-trigger').setAttribute('aria-expanded','false');var menu=dd.querySelector('.dropdown-menu');if(menu)menu.hidden=true;}});});

""");
        }

        if (b.HasTabs)
        {
            sb.AppendLine("""
document.querySelectorAll('.tab-nav').forEach(function(nav){var btns=nav.querySelectorAll('.tab-btn');btns.forEach(function(btn){btn.addEventListener('click',function(){var panelId=btn.getAttribute('aria-controls');btns.forEach(function(b){b.setAttribute('aria-selected','false');});btn.setAttribute('aria-selected','true');var parent=nav.closest('.tabs');if(parent){parent.querySelectorAll('.tab-panel').forEach(function(p){p.classList.add('hidden');});var panel=document.getElementById(panelId);if(panel)panel.classList.remove('hidden');}});});if(btns.length>0){btns[0].click();}});

""");
        }

        if (b.UseLenis)
        {
            sb.AppendLine("""
var lenis=new Lenis({duration:1.2,easing:function(t){return Math.min(1,1.001-Math.pow(2,-10*t))},smoothWheel:true});
function raf(time){lenis.raf(time);requestAnimationFrame(raf);}
requestAnimationFrame(raf);

""");
        }

        sb.AppendLine("})();");
        return sb.ToString();
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
            var label = Esc(link.Label ?? "Link");
            var url = Esc(link.Url ?? "#");
            var href = url.StartsWith("/", StringComparison.Ordinal) ? "{{ base_url }}" + url : url;
            sb.AppendLine($"        <a href=\"{href}\">{label}</a>");
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
