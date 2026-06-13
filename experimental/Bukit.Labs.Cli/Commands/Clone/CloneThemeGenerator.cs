using System.Text;

namespace Bukit.Cli.Commands;

internal static class CloneThemeGenerator
{
    public static CloneGenerationSummary WriteTo(string rootDir, string themeName, CloneTokens tokens, CloneLayoutInfo layout, string? brand = null, CloneBehaviors? behaviors = null, List<CloneIcon>? icons = null, List<CloneAsset>? assets = null, TemplateScope templateScope = TemplateScope.Full, bool includePageTemplate = true)
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

        var baseLayout = CloneLayoutGenerator.GenerateBaseLayout(tokens, behaviors);
        WriteFile(rootDir, $"themes/{themeName}/layouts/layouts/base.html", baseLayout);
        fileCount++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/partials/header.html", CloneLayoutGenerator.GenerateHeader(tokens, layout, brand, behaviors));
        fileCount++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/partials/footer.html", CloneLayoutGenerator.GenerateFooter(layout, brand));
        fileCount++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/partials/list-card.html", ThemeTemplateResource.Get("ListCardPartial"));
        fileCount++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/partials/pagination-nav.html", ThemeTemplateResource.Get("PaginationNavPartial"));
        fileCount++;

        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/index.html", CloneIndexPageGenerator.GenerateIndex(tokens, layout, brand, warnings));
        fileCount++;
        if (includePageTemplate)
        {
            WriteFile(rootDir, $"themes/{themeName}/layouts/pages/page.html", ThemeTemplateResource.Get("PageTemplate"));
            fileCount++;
        }
        if (templateScope.ShouldWritePageTemplates())
        {
            WriteFile(rootDir, $"themes/{themeName}/layouts/pages/post.html", ThemeTemplateResource.Get("PostTemplate"));
            fileCount++;
            WriteFile(rootDir, $"themes/{themeName}/layouts/pages/list.html", ThemeTemplateResource.Get("ListTemplate"));
            fileCount++;
        }
        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/pagination.html", ThemeTemplateResource.Get("PaginationTemplate"));
        fileCount++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/taxonomy-index.html", ThemeTemplateResource.Get("TaxonomyIndexTemplate"));
        fileCount++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/taxonomy-term.html", ThemeTemplateResource.Get("TaxonomyTermTemplate"));
        fileCount++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/search.html", ThemeTemplateResource.Get("SearchTemplate"));
        fileCount++;
        if (includePageTemplate)
        {
            WriteFile(rootDir, $"themes/{themeName}/layouts/bukit.templates.yaml",
                templateScope == TemplateScope.Full ? ThemeTemplateResource.Get("TemplateCapabilities") : BareTemplateCapabilities);
            fileCount++;
        }

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
        sb.AppendLine("templates:");
        sb.AppendLine("  home:");
        sb.AppendLine("    template: pages/index.html");
        sb.AppendLine("    required: true");
        sb.AppendLine("  page:");
        sb.AppendLine("    template: pages/page.html");
        sb.AppendLine("    accepts:");
        sb.AppendLine("      collection: page");
        sb.AppendLine("  post:");
        sb.AppendLine("    template: pages/post.html");
        sb.AppendLine("    accepts:");
        sb.AppendLine("      collection: post");
        sb.AppendLine("  detail:");
        sb.AppendLine("    template: pages/page.html");
        sb.AppendLine("    accepts:");
        sb.AppendLine("      kind: detail");
        sb.AppendLine("  list:");
        sb.AppendLine("    template: pages/list.html");
        sb.AppendLine("    accepts:");
        sb.AppendLine("      kind: list");
        sb.AppendLine("  pagination:");
        sb.AppendLine("    template: pages/pagination.html");
        sb.AppendLine("    accepts:");
        sb.AppendLine("      kind: pagination");
        sb.AppendLine("  archive:");
        sb.AppendLine("    template: pages/page.html");
        sb.AppendLine("    accepts:");
        sb.AppendLine("      kind: archive");
        sb.AppendLine("  taxonomy_index:");
        sb.AppendLine("    template: pages/taxonomy-index.html");
        sb.AppendLine("    accepts:");
        sb.AppendLine("      kind: taxonomy_index");
        sb.AppendLine("  taxonomy_term:");
        sb.AppendLine("    template: pages/taxonomy-term.html");
        sb.AppendLine("    accepts:");
        sb.AppendLine("      kind: taxonomy_term");
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

    internal const string BareTemplateCapabilities = """
templates:
  pages/index.html:
    capabilities:
      needs_page_content: false
      supports_pagination: false
      supports_taxonomy: false
      supports_search_snippets: false
  pages/page.html:
    capabilities:
      needs_page_content: false
      supports_pagination: false
      supports_taxonomy: false
      supports_search_snippets: false
  pages/pagination.html:
    capabilities:
      supports_pagination: true
  pages/taxonomy-index.html:
    capabilities:
      supports_taxonomy: true
  pages/taxonomy-term.html:
    capabilities:
      supports_taxonomy: true
  pages/search.html:
    capabilities:
      supports_search_snippets: true
""";

    private static void WriteFile(string rootDir, string relativePath, string content)
    {
        var path = Path.Combine(rootDir, relativePath);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
