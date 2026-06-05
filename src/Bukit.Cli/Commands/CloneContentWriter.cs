using System.Text;

namespace Bukit.Cli.Commands;

internal static class CloneContentWriter
{
    public static CloneContentWriteResult WriteTo(
        string rootDir,
        string themeName,
        CloneTokens tokens,
        ClonePageInfo page,
        IReadOnlyList<CloneSectionInfo> sections,
        IReadOnlyList<CloneAsset> assets,
        CloneBehaviors? behaviors,
        string? brand,
        TemplateScope templateScope = TemplateScope.Full,
        bool includePageTemplate = true)
    {
        var warnings = new List<string>();
        var normalizedSections = CloneSectionDataWriter.NormalizeSections(sections).ToList();
        var assetMap = CloneContentAssetHelpers.BuildAssetMap(assets);

        var contentFiles = 0;
        var dataFiles = 0;
        var themeFiles = 0;

        WriteFile(rootDir, "content/index.md", GenerateIndexContent(page, brand));
        contentFiles++;

        foreach (var section in normalizedSections)
        {
            var markdown = CloneSectionDataWriter.GenerateSectionData(section, assetMap);
            WriteFile(rootDir, $"data/{section.Key}.md", markdown);
            dataFiles++;
        }

        WriteFile(rootDir, "data/clone-assets.md", CloneContentAssetHelpers.GenerateAssetManifest(assets, assetMap));
        dataFiles++;

        CloneResearchWriter.WriteTo(rootDir, tokens, page, normalizedSections.Select(s => s.Source).ToList(), assets, behaviors, assetMap);

        var css = CloneStyleSheetGenerator.GenerateStyleCss(tokens) + "\n\n" + CloneContentCssWriter.GenerateCloneCss(normalizedSections);
        if (behaviors is not null && behaviors.HasAnyCssBehavior)
        {
            css += "\n" + CloneBehaviorGenerator.GenerateBehaviorCss(behaviors, tokens);
        }

        WriteFile(rootDir, $"themes/{themeName}/assets/style.css", css);
        themeFiles++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/layouts/base.html", CloneLayoutGenerator.GenerateBaseLayout(tokens, behaviors));
        themeFiles++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/partials/header.html", CloneLayoutGenerator.GenerateHeader(tokens, CloneLayoutInfo.Default, brand, behaviors));
        themeFiles++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/partials/footer.html", CloneLayoutGenerator.GenerateFooter(CloneLayoutInfo.Default, brand));
        themeFiles++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/partials/clone-section.html", CloneSectionPartial);
        themeFiles++;

        foreach (var partial in CloneSectionDataWriter.CommonPartials())
        {
            WriteFile(rootDir, $"themes/{themeName}/layouts/partials/{partial}.html", CloneSectionPartial);
            themeFiles++;
        }

        WriteFile(rootDir, $"themes/{themeName}/layouts/partials/list-card.html", ThemeTemplateResource.Get("ListCardPartial"));
        themeFiles++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/partials/pagination-nav.html", ThemeTemplateResource.Get("PaginationNavPartial"));
        themeFiles++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/index.html", CloneSectionDataWriter.GenerateStructuredIndex(normalizedSections));
        themeFiles++;
        if (includePageTemplate)
        {
            WriteFile(rootDir, $"themes/{themeName}/layouts/pages/page.html", ThemeTemplateResource.Get("PageTemplate"));
            themeFiles++;
        }
        if (templateScope.ShouldWritePageTemplates())
        {
            WriteFile(rootDir, $"themes/{themeName}/layouts/pages/post.html", ThemeTemplateResource.Get("PostTemplate"));
            themeFiles++;
            WriteFile(rootDir, $"themes/{themeName}/layouts/pages/list.html", ThemeTemplateResource.Get("ListTemplate"));
            themeFiles++;
        }
        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/pagination.html", ThemeTemplateResource.Get("PaginationTemplate"));
        themeFiles++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/taxonomy-index.html", ThemeTemplateResource.Get("TaxonomyIndexTemplate"));
        themeFiles++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/taxonomy-term.html", ThemeTemplateResource.Get("TaxonomyTermTemplate"));
        themeFiles++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/search.html", ThemeTemplateResource.Get("SearchTemplate"));
        themeFiles++;
        if (includePageTemplate)
        {
            WriteFile(rootDir, $"themes/{themeName}/layouts/bukit.templates.yaml",
                templateScope == TemplateScope.Full ? ThemeTemplateResource.Get("TemplateCapabilities") : CloneThemeGenerator.BareTemplateCapabilities);
            themeFiles++;
        }
        WriteFile(rootDir, $"themes/{themeName}/theme.yaml", GenerateThemeYaml(themeName, tokens, brand, behaviors));
        themeFiles++;

        if (behaviors is not null && behaviors.HasAnyJsBehavior)
        {
            WriteFile(rootDir, $"themes/{themeName}/assets/behaviors.js", CloneBehaviorGenerator.GenerateBehaviorsJs(behaviors));
            themeFiles++;
        }

        if (behaviors?.HasModal == true)
        {
            WriteFile(rootDir, $"themes/{themeName}/layouts/partials/modal.html", CloneThemeGenerator.ModalPartial);
            themeFiles++;
        }

        if (behaviors?.HasDropdown == true)
        {
            WriteFile(rootDir, $"themes/{themeName}/layouts/partials/dropdown.html", CloneThemeGenerator.DropdownPartial);
            themeFiles++;
        }

        if (behaviors?.HasTabs == true)
        {
            WriteFile(rootDir, $"themes/{themeName}/layouts/partials/tabs.html", CloneThemeGenerator.TabsPartial);
            themeFiles++;
        }

        var configUpdated = CloneYamlWriter.EnsureSourcesConfig(rootDir, themeName, brand, tokens, warnings);

        return new CloneContentWriteResult(
            ThemeFileCount: themeFiles,
            ContentFileCount: contentFiles,
            DataFileCount: dataFiles,
            SectionCount: normalizedSections.Count,
            ConfigUpdated: configUpdated,
            Warnings: warnings);
    }

    internal static string SectionDataKey(CloneSectionInfo section, int index) => CloneContentAssetHelpers.SectionDataKey(section, index);

    internal static string SectionSpecFileName(CloneSectionInfo section, int index) => CloneContentAssetHelpers.SectionSpecFileName(section, index);

    internal static IReadOnlyDictionary<string, string> BuildAssetMap(IReadOnlyList<CloneAsset> assets) => CloneContentAssetHelpers.BuildAssetMap(assets);

    internal static string AssetFileName(CloneAsset asset, int index) => CloneContentAssetHelpers.AssetFileName(asset, index);

    internal static string AssetSubdir(string? type) => CloneContentAssetHelpers.AssetSubdir(type);

    internal static string LocalAssetPath(CloneAsset asset, int index) => CloneContentAssetHelpers.LocalAssetPath(asset, index);

    private static string SanitizeFileName(string value)
    {
        var sb = new StringBuilder();
        foreach (var c in value)
            sb.Append(char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '_');
        return sb.Length == 0 ? "asset.img" : sb.ToString();
    }

    private static string SanitizeSlug(string value)
    {
        var slug = System.Text.RegularExpressions.Regex.Replace(value.Trim().ToLowerInvariant(), "[^a-z0-9-]+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "clone-section" : slug;
    }

    private static string NormalizeType(string? type) => CloneContentAssetHelpers.NormalizeType(type);

    private static string PartialFor(string type) => CloneSectionDataWriter.PartialFor(type);

    private static string GenerateIndexContent(ClonePageInfo page, string? brand)
    {
        var title = page.Title ?? page.Seo?.Title ?? brand ?? "Cloned page";
        var summary = page.Summary ?? page.Description ?? page.Seo?.Description;
        var body = !string.IsNullOrWhiteSpace(page.BodyMarkdown)
            ? page.BodyMarkdown!.Trim()
            : !string.IsNullOrWhiteSpace(page.ContentHtml)
                ? page.ContentHtml!.Trim()
                : $"# {title}";

        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"title: {CloneYamlWriter.YamlScalar(title)}");
        sb.AppendLine("type: page");
        sb.AppendLine("slug: index");
        sb.AppendLine("template: pages/index.html");
        if (!string.IsNullOrWhiteSpace(page.Url))
            sb.AppendLine($"source_url: {CloneYamlWriter.YamlScalar(page.Url!)}");
        if (!string.IsNullOrWhiteSpace(summary))
            sb.AppendLine($"summary: {CloneYamlWriter.YamlScalar(summary!)}");
        if (!string.IsNullOrWhiteSpace(page.Seo?.Image))
            sb.AppendLine($"og_image: {CloneYamlWriter.YamlScalar(page.Seo!.Image!)}");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine(body);
        return sb.ToString();
    }

    private static string GenerateThemeYaml(string themeName, CloneTokens tokens, string? brand, CloneBehaviors? behaviors)
    {
        var author = brand ?? "Bukit";
        var tags = new List<string> { "cloned", "content-data" };
        if (behaviors?.DarkModeToggle == true) tags.Add("dark-mode");
        if (behaviors?.MobileHamburger == true) tags.Add("responsive");

        var sb = new StringBuilder();
        sb.AppendLine($"name: {themeName}");
        sb.AppendLine("version: 1.0.0");
        sb.AppendLine("description: High-fidelity clone theme generated from target website sections and data");
        sb.AppendLine($"author: {CloneYamlWriter.YamlScalar(author)}");
        sb.AppendLine("license: MIT");
        sb.AppendLine($"tags: [{string.Join(", ", tags)}]");
        sb.AppendLine("params:");
        sb.AppendLine("  - key: brand");
        sb.AppendLine("    label: Site Brand");
        sb.AppendLine("    type: string");
        sb.AppendLine($"    default: {CloneYamlWriter.YamlScalar(author)}");
        sb.AppendLine("  - key: primary_color");
        sb.AppendLine("    label: Primary Color");
        sb.AppendLine("    type: color");
        sb.AppendLine($"    default: \"{tokens.Primary ?? "#0b5fff"}\"");
        sb.AppendLine("  - key: accent_color");
        sb.AppendLine("    label: Accent Color");
        sb.AppendLine("    type: color");
        sb.AppendLine($"    default: \"{tokens.Accent ?? "#0f7b6c"}\"");
        return sb.ToString();
    }

    internal static string Html(string value)
        => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    internal static string HtmlAttr(string value)
        => Html(value).Replace("\"", "&quot;");

    private static void WriteFile(string rootDir, string relativePath, string content)
    {
        var path = Path.Combine(rootDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private const string CloneSectionPartial = """
<section class="clone-section {{ if section.fields && section.fields.clone_class }}{{ section.fields.clone_class.value }}{{ end }} {{ if section.fields && section.fields.semantic }}clone-{{ section.fields.semantic.value }}{{ end }}">
  {{ if section.fields && section.fields.eyebrow }}<p class="eyebrow">{{ section.fields.eyebrow.value }}</p>{{ end }}
  <h2 class="clone-section-title">{{ section.title }}</h2>
  {{ if section.fields && section.fields.subheading }}<p class="clone-section-subheading">{{ section.fields.subheading.value }}</p>{{ end }}
  <div class="clone-section-body">
    {{ if section.fields && section.fields.content_html }}{{ section.fields.content_html.value }}{{ else }}{{ section.content }}{{ end }}
  </div>
</section>
""";
}

internal sealed record CloneContentWriteResult(
    int ThemeFileCount,
    int ContentFileCount,
    int DataFileCount,
    int SectionCount,
    bool ConfigUpdated,
    List<string> Warnings);
