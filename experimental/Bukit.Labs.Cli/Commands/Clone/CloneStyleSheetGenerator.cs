using System.Text;

namespace Bukit.Cli.Commands;

internal static class CloneStyleSheetGenerator
{
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

    internal static string C(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    internal static void AddVar(StringBuilder sb, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            sb.AppendLine($"  {name}: {value};");
    }

    internal static string Esc(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
