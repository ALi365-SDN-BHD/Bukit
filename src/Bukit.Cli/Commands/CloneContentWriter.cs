using System.Text;
using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;

namespace Bukit.Cli.Commands;

internal static partial class CloneContentWriter
{
    public static CloneContentWriteResult WriteTo(
        string rootDir,
        string themeName,
        CloneTokens tokens,
        ClonePageInfo page,
        IReadOnlyList<CloneSectionInfo> sections,
        IReadOnlyList<CloneAsset> assets,
        CloneBehaviors? behaviors,
        string? brand)
    {
        var warnings = new List<string>();
        var normalizedSections = NormalizeSections(sections).ToList();
        var assetMap = BuildAssetMap(assets);

        var contentFiles = 0;
        var dataFiles = 0;
        var themeFiles = 0;

        WriteFile(rootDir, "content/index.md", GenerateIndexContent(page, brand));
        contentFiles++;

        foreach (var section in normalizedSections)
        {
            var markdown = GenerateSectionData(section, assetMap);
            WriteFile(rootDir, $"data/{section.Key}.md", markdown);
            dataFiles++;
        }

        WriteFile(rootDir, "data/clone-assets.md", GenerateAssetManifest(assets, assetMap));
        dataFiles++;

        CloneResearchWriter.WriteTo(rootDir, tokens, page, normalizedSections.Select(s => s.Source).ToList(), assets, behaviors, assetMap);

        var css = CloneThemeGenerator.GenerateStyleCss(tokens) + "\n\n" + GenerateCloneCss(normalizedSections);
        if (behaviors is not null && behaviors.HasAnyCssBehavior)
        {
            css += "\n" + CloneThemeGenerator.GenerateBehaviorCss(behaviors, tokens);
        }

        WriteFile(rootDir, $"themes/{themeName}/assets/style.css", css);
        themeFiles++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/layouts/base.html", CloneThemeGenerator.GenerateBaseLayout(tokens, behaviors));
        themeFiles++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/partials/header.html", CloneThemeGenerator.GenerateHeader(tokens, CloneLayoutInfo.Default, brand, behaviors));
        themeFiles++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/partials/footer.html", CloneThemeGenerator.GenerateFooter(CloneLayoutInfo.Default, brand));
        themeFiles++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/partials/clone-section.html", CloneSectionPartial);
        themeFiles++;

        foreach (var partial in CommonPartials())
        {
            WriteFile(rootDir, $"themes/{themeName}/layouts/partials/{partial}.html", CloneSectionPartial);
            themeFiles++;
        }

        WriteFile(rootDir, $"themes/{themeName}/layouts/partials/list-card.html", StarterThemeScaffold.ListCardPartial);
        themeFiles++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/partials/pagination-nav.html", StarterThemeScaffold.PaginationNavPartial);
        themeFiles++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/index.html", GenerateStructuredIndex(normalizedSections));
        themeFiles++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/page.html", StarterThemeScaffold.PageTemplate);
        themeFiles++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/post.html", StarterThemeScaffold.PostTemplate);
        themeFiles++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/list.html", StarterThemeScaffold.ListTemplate);
        themeFiles++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/pagination.html", StarterThemeScaffold.PaginationTemplate);
        themeFiles++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/taxonomy-index.html", StarterThemeScaffold.TaxonomyIndexTemplate);
        themeFiles++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/taxonomy-term.html", StarterThemeScaffold.TaxonomyTermTemplate);
        themeFiles++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/search.html", StarterThemeScaffold.SearchTemplate);
        themeFiles++;
        WriteFile(rootDir, $"themes/{themeName}/layouts/bukit.templates.yaml", StarterThemeScaffold.TemplateCapabilities);
        themeFiles++;
        WriteFile(rootDir, $"themes/{themeName}/theme.yaml", GenerateThemeYaml(themeName, tokens, brand, behaviors));
        themeFiles++;

        if (behaviors is not null && behaviors.HasAnyJsBehavior)
        {
            WriteFile(rootDir, $"themes/{themeName}/assets/behaviors.js", CloneThemeGenerator.GenerateBehaviorsJs(behaviors));
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

        var configUpdated = EnsureSourcesConfig(rootDir, themeName, brand, tokens, warnings);

        return new CloneContentWriteResult(
            ThemeFileCount: themeFiles,
            ContentFileCount: contentFiles,
            DataFileCount: dataFiles,
            SectionCount: normalizedSections.Count,
            ConfigUpdated: configUpdated,
            Warnings: warnings);
    }

    private static IEnumerable<NormalizedSection> NormalizeSections(IReadOnlyList<CloneSectionInfo> sections)
    {
        for (var i = 0; i < sections.Count; i++)
        {
            var section = sections[i];
            var order = section.Order ?? ((i + 1) * 10);
            var type = NormalizeType(section.Type ?? section.Semantic);
            var key = SectionDataKey(section, i);
            var cssClass = $"clone-section-{i + 1:000}";
            yield return new NormalizedSection(section, key, type, order, cssClass);
        }
    }

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
        sb.AppendLine($"title: {YamlScalar(title)}");
        sb.AppendLine("type: page");
        sb.AppendLine("slug: index");
        sb.AppendLine("template: pages/index.html");
        if (!string.IsNullOrWhiteSpace(page.Url))
            sb.AppendLine($"source_url: {YamlScalar(page.Url!)}");
        if (!string.IsNullOrWhiteSpace(summary))
            sb.AppendLine($"summary: {YamlScalar(summary!)}");
        if (!string.IsNullOrWhiteSpace(page.Seo?.Image))
            sb.AppendLine($"og_image: {YamlScalar(page.Seo!.Image!)}");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine(body);
        return sb.ToString();
    }

    private static string GenerateSectionData(NormalizedSection normalized, IReadOnlyDictionary<string, string> assetMap)
    {
        var section = normalized.Source;
        var title = section.DisplayTitle;
        var body = BuildSectionBody(normalized, assetMap);

        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"title: {YamlScalar(title)}");
        sb.AppendLine($"type: {YamlScalar(normalized.Type)}");
        sb.AppendLine($"order: {normalized.Order}");
        sb.AppendLine("enabled: true");
        sb.AppendLine($"clone_key: {YamlScalar(normalized.Key)}");
        sb.AppendLine($"clone_class: {YamlScalar(normalized.CssClass)}");
        if (!string.IsNullOrWhiteSpace(section.Semantic))
            sb.AppendLine($"semantic: {YamlScalar(section.Semantic!)}");
        if (!string.IsNullOrWhiteSpace(section.Eyebrow))
            sb.AppendLine($"eyebrow: {YamlScalar(section.Eyebrow!)}");
        if (!string.IsNullOrWhiteSpace(section.Subheading))
            sb.AppendLine($"subheading: {YamlScalar(section.Subheading!)}");
        if (section.Buttons.Count > 0)
            sb.AppendLine($"buttons_json: {YamlScalar(CloneJson.Serialize(section.Buttons))}");
        if (section.Items.Count > 0)
            sb.AppendLine($"items_json: {YamlScalar(CloneJson.Serialize(section.Items))}");
        if (section.Components.Count > 0)
            sb.AppendLine($"components_json: {YamlScalar(CloneJson.Serialize(section.Components))}");
        var imageUrls = RewriteUrls(section.ImageUrls.Concat(section.Assets.Select(a => a.Src)), assetMap)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (imageUrls.Count > 0)
            sb.AppendLine($"image_urls_json: {YamlScalar(CloneJson.Serialize(imageUrls))}");
        if (section.Styles is { Count: > 0 })
            sb.AppendLine($"styles_json: {YamlScalar(CloneJson.Serialize(section.Styles))}");
        if (section.ComputedStyles is { Count: > 0 })
            sb.AppendLine($"computed_styles_json: {YamlScalar(CloneJson.Serialize(section.ComputedStyles))}");
        if (section.Bounds is not null)
            sb.AppendLine($"bounds_json: {YamlScalar(CloneJson.Serialize(section.Bounds))}");
        if (section.Interactions.Count > 0)
            sb.AppendLine($"interactions_json: {YamlScalar(CloneJson.Serialize(section.Interactions))}");
        if (section.HasStates)
            sb.AppendLine($"states_json: {YamlScalar(CloneJson.Serialize(section.States))}");
        if (section.Responsive is not null)
            sb.AppendLine($"responsive_json: {YamlScalar(CloneJson.Serialize(section.Responsive))}");
        AppendBlockScalar(sb, "content_html", body);
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine(body);
        return sb.ToString();
    }

    private static string BuildSectionBody(NormalizedSection normalized, IReadOnlyDictionary<string, string> assetMap)
    {
        var section = normalized.Source;
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(section.ContentHtml))
            sb.AppendLine(RewriteUrls(section.ContentHtml!, assetMap));

        if (!string.IsNullOrWhiteSpace(section.Text))
            sb.AppendLine($"<p>{Html(section.Text!)}</p>");

        foreach (var image in RewriteUrls(section.ImageUrls, assetMap))
            sb.AppendLine($"<img src=\"{HtmlAttr(image)}\" alt=\"\" loading=\"lazy\" />");

        if (section.Items.Count > 0)
        {
            sb.AppendLine("<div class=\"clone-items\">");
            foreach (var item in section.Items)
            {
                sb.AppendLine("  <article class=\"clone-item\">");
                if (!string.IsNullOrWhiteSpace(item.Image))
                    sb.AppendLine($"    <img src=\"{HtmlAttr(RewriteUrl(item.Image!, assetMap))}\" alt=\"\" loading=\"lazy\" />");
                if (!string.IsNullOrWhiteSpace(item.Title))
                    sb.AppendLine($"    <h3>{Html(item.Title!)}</h3>");
                var text = item.Description ?? item.Text;
                if (!string.IsNullOrWhiteSpace(text))
                    sb.AppendLine($"    <p>{Html(text!)}</p>");
                if (!string.IsNullOrWhiteSpace(item.Url))
                    sb.AppendLine($"    <a class=\"clone-link\" href=\"{HtmlAttr(item.Url!)}\">{Html(item.Title ?? item.Url!)}</a>");
                sb.AppendLine("  </article>");
            }
            sb.AppendLine("</div>");
        }

        foreach (var button in section.Buttons)
        {
            if (string.IsNullOrWhiteSpace(button.Label))
                continue;
            sb.AppendLine($"<a class=\"clone-button clone-button-{HtmlAttr(button.Variant ?? "primary")}\" href=\"{HtmlAttr(button.Url ?? "#")}\">{Html(button.Label!)}</a>");
        }

        if (section.States.Count > 0)
        {
            sb.AppendLine("<div class=\"state-section\">");
            sb.AppendLine("  <div class=\"state-tabs\" role=\"tablist\">");
            for (var i = 0; i < section.States.Count; i++)
            {
                var selected = i == 0 ? "true" : "false";
                sb.AppendLine($"    <button class=\"state-tab\" role=\"tab\" aria-selected=\"{selected}\" aria-controls=\"{normalized.Key}-state-{i}\">{Html(section.States[i].Label ?? $"State {i + 1}")}</button>");
            }
            sb.AppendLine("  </div>");
            for (var i = 0; i < section.States.Count; i++)
            {
                var hidden = i == 0 ? "" : " hidden";
                sb.AppendLine($"  <div class=\"state-panel{hidden}\" role=\"tabpanel\" id=\"{normalized.Key}-state-{i}\">");
                sb.AppendLine(RewriteUrls(section.States[i].ContentHtml ?? "", assetMap));
                sb.AppendLine("  </div>");
            }
            sb.AppendLine("</div>");
        }

        return sb.Length == 0 ? "<!-- cloned empty section -->" : sb.ToString();
    }

    private static string GenerateStructuredIndex(IReadOnlyList<NormalizedSection> sections)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{% layout \"layouts/base.html\" %}");
        sb.AppendLine();
        foreach (var section in sections)
        {
            var partial = PartialFor(section.Type);
            sb.AppendLine($"{{{{ if site.modules && site.modules.{section.Type} }}}}");
            sb.AppendLine($"{{{{ for section in site.modules.{section.Type} }}}}");
            sb.AppendLine($"  {{{{ if section.fields && section.fields.clone_key && section.fields.clone_key.value == \"{section.Key}\" }}}}");
            sb.AppendLine($"    {{{{ include \"partials/{partial}.html\" }}}}");
            sb.AppendLine("  {{ end }}");
            sb.AppendLine("{{ end }}");
            sb.AppendLine("{{ end }}");
        }
        return sb.ToString();
    }

    private static string GenerateCloneCss(IReadOnlyList<NormalizedSection> sections)
    {
        var sb = new StringBuilder();
        sb.AppendLine(".clone-section { margin: var(--section-gap) 0; }");
        sb.AppendLine(".clone-section-body > :first-child { margin-top: 0; }");
        sb.AppendLine(".clone-section-body > :last-child { margin-bottom: 0; }");
        sb.AppendLine(".clone-items { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 18px; }");
        sb.AppendLine(".clone-item { min-width: 0; padding: 18px; border: 1px solid var(--border); border-radius: var(--radius); background: var(--surface); box-shadow: var(--card-shadow); }");
        sb.AppendLine(".clone-button { display: inline-flex; align-items: center; justify-content: center; min-height: 42px; margin: 12px 10px 0 0; padding: 0 18px; border-radius: var(--radius); background: var(--primary); color: #fff; font-weight: 700; text-decoration: none; }");
        sb.AppendLine(".clone-button:hover { background: var(--primary-strong); color: #fff; text-decoration: none; }");
        sb.AppendLine(".clone-hero { padding: 44px 0; }");
        sb.AppendLine(".clone-hero .clone-section-title { font-size: clamp(2rem, 5vw, 4.2rem); line-height: 1.05; }");
        foreach (var section in sections)
        {
            if (section.Source.Styles is { Count: > 0 })
            {
                var declarations = section.Source.Styles
                    .Where(kv => IsSafeCssName(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value))
                    .Select(kv => $"  {kv.Key}: {kv.Value.Trim()};")
                    .ToList();
                if (declarations.Count > 0)
                {
                    sb.AppendLine($".{section.CssClass} {{");
                    foreach (var declaration in declarations)
                        sb.AppendLine(declaration);
                    sb.AppendLine("}");
                }
            }

            if (section.Source.Responsive is not null)
            {
                AppendResponsiveCss(sb, section);
            }
        }
        return sb.ToString();
    }

    private static string GenerateAssetManifest(IReadOnlyList<CloneAsset> assets, IReadOnlyDictionary<string, string> assetMap)
    {
        var manifest = assets.Select(asset => new CloneAssetManifestEntry(
            asset.Type,
            asset.Src,
            asset.Alt,
            asset.Media,
            asset.Width,
            asset.Height,
            assetMap.TryGetValue(asset.Src, out var local) ? local : asset.LocalPath,
            asset.Integrity,
            asset.Failure)).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine("title: 'Clone assets'");
        sb.AppendLine("type: 'assets'");
        sb.AppendLine("order: 0");
        sb.AppendLine("enabled: true");
        sb.AppendLine($"assets_json: {YamlScalar(CloneJson.Serialize(manifest))}");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("Clone asset manifest generated from assets.json.");
        return sb.ToString();
    }

    private static void AppendResponsiveCss(StringBuilder sb, NormalizedSection section)
    {
        var r = section.Source.Responsive!;
        if (r.ColumnsDesktop is not null)
            sb.AppendLine($".{section.CssClass} .clone-items {{ grid-template-columns: {r.ColumnsDesktop}; }}");
        if (r.MaxWidthDesktop is not null)
            sb.AppendLine($".{section.CssClass} {{ max-width: {r.MaxWidthDesktop}; }}");
        if (r.ColumnsTablet is not null || r.MaxWidthTablet is not null)
        {
            sb.AppendLine("@media (max-width: var(--bp-tablet)) {");
            if (r.ColumnsTablet is not null)
                sb.AppendLine($"  .{section.CssClass} .clone-items {{ grid-template-columns: {r.ColumnsTablet}; }}");
            if (r.MaxWidthTablet is not null)
                sb.AppendLine($"  .{section.CssClass} {{ max-width: {r.MaxWidthTablet}; }}");
            sb.AppendLine("}");
        }
        if (r.ColumnsMobile is not null || r.MaxWidthMobile is not null)
        {
            sb.AppendLine("@media (max-width: var(--bp-mobile)) {");
            if (r.ColumnsMobile is not null)
                sb.AppendLine($"  .{section.CssClass} .clone-items {{ grid-template-columns: {r.ColumnsMobile}; }}");
            if (r.MaxWidthMobile is not null)
                sb.AppendLine($"  .{section.CssClass} {{ max-width: {r.MaxWidthMobile}; }}");
            sb.AppendLine("}");
        }
    }

    private static bool EnsureSourcesConfig(string rootDir, string themeName, string? brand, CloneTokens tokens, List<string> warnings)
    {
        var path = Path.Combine(rootDir, "site.yaml");
        if (!File.Exists(path))
        {
            warnings.Add("site.yaml not found; skipped content source configuration.");
            return false;
        }

        try
        {
            var stream = new YamlStream();
            using (var reader = File.OpenText(path))
                stream.Load(reader);

            if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root)
            {
                warnings.Add("site.yaml root is not a mapping; skipped content source configuration.");
                return false;
            }

            var content = GetOrCreateMapping(root, "content");
            content.Children[new YamlScalarNode("provider")] = new YamlScalarNode("sources");
            var sources = GetOrCreateSequence(content, "sources");
            EnsureMarkdownSource(sources, "content", "content", "content", "page");
            EnsureMarkdownSource(sources, "modules", "data", "data", "module");

            var theme = GetOrCreateMapping(root, "theme");
            theme.Children[new YamlScalarNode("name")] = new YamlScalarNode(themeName);
            var parameters = GetOrCreateMapping(theme, "params");
            if (!string.IsNullOrWhiteSpace(brand))
            {
                parameters.Children[new YamlScalarNode("brand")] = new YamlScalarNode(brand);
                parameters.Children[new YamlScalarNode("footer_text")] = new YamlScalarNode(brand);
            }
            if (!string.IsNullOrWhiteSpace(tokens.Primary))
                parameters.Children[new YamlScalarNode("primary_color")] = new YamlScalarNode(tokens.Primary);
            if (!string.IsNullOrWhiteSpace(tokens.Accent))
                parameters.Children[new YamlScalarNode("accent_color")] = new YamlScalarNode(tokens.Accent);

            using var writer = new StringWriter();
            stream.Save(writer, assignAnchors: false);
            File.WriteAllText(path, writer.ToString());
            return true;
        }
        catch (Exception ex)
        {
            warnings.Add($"Failed to update site.yaml: {ex.Message}");
            return false;
        }
    }

    private static void EnsureMarkdownSource(YamlSequenceNode sources, string name, string mode, string dir, string defaultType)
    {
        foreach (var child in sources.Children.OfType<YamlMappingNode>())
        {
            if (GetScalar(child, "name")?.Equals(name, StringComparison.OrdinalIgnoreCase) == true)
            {
                child.Children[new YamlScalarNode("type")] = new YamlScalarNode("markdown");
                child.Children[new YamlScalarNode("mode")] = new YamlScalarNode(mode);
                if (mode.Equals("content", StringComparison.OrdinalIgnoreCase) && defaultType.Equals("page", StringComparison.OrdinalIgnoreCase))
                    child.Children[new YamlScalarNode("collection")] = new YamlScalarNode("page");
                var markdown = GetOrCreateMapping(child, "markdown");
                markdown.Children[new YamlScalarNode("dir")] = new YamlScalarNode(dir);
                markdown.Children[new YamlScalarNode("defaultType")] = new YamlScalarNode(defaultType);
                return;
            }
        }

        var newNode = new YamlMappingNode
        {
            { "type", "markdown" },
            { "name", name },
            { "mode", mode },
        };
        if (mode.Equals("content", StringComparison.OrdinalIgnoreCase) && defaultType.Equals("page", StringComparison.OrdinalIgnoreCase))
            newNode.Children[new YamlScalarNode("collection")] = new YamlScalarNode("page");
        newNode.Children[new YamlScalarNode("markdown")] = new YamlMappingNode { { "dir", dir }, { "defaultType", defaultType } };
        sources.Add(newNode);
    }

    internal static IReadOnlyDictionary<string, string> BuildAssetMap(IReadOnlyList<CloneAsset> assets)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var asset in assets.Where(a => !string.IsNullOrWhiteSpace(a.Src)))
        {
            index++;
            var local = asset.LocalPath;
            if (string.IsNullOrWhiteSpace(local) && asset.Src.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                local = LocalAssetPath(asset, index);
            }

            if (!string.IsNullOrWhiteSpace(local))
                map[asset.Src] = local!;
        }
        return map;
    }

    internal static string AssetFileName(CloneAsset asset, int index)
    {
        try
        {
            var uri = new Uri(asset.Src);
            var fileName = Path.GetFileName(uri.AbsolutePath);
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = $"{asset.Type}-{index}.img";
            if (string.IsNullOrWhiteSpace(Path.GetExtension(fileName)))
                fileName += ".img";
            return SanitizeFileName(fileName);
        }
        catch
        {
            var ext = Path.GetExtension(asset.Src);
            return SanitizeFileName($"{asset.Type}-{index}{(string.IsNullOrWhiteSpace(ext) ? ".img" : ext)}");
        }
    }

    internal static string LocalAssetPath(CloneAsset asset, int index)
        => $"/assets/{AssetSubdir(asset.Type)}/{AssetFileName(asset, index)}";

    internal static string AssetSubdir(string? type)
    {
        var normalized = (type ?? "").Trim().ToLowerInvariant();
        if (normalized is "video" or "videos" or "movie" or "lottie")
            return "videos";
        if (normalized is "font" or "fonts" or "typeface")
            return "fonts";
        if (normalized is "favicon" or "og" or "open_graph" or "seo" or "manifest")
            return "seo";
        if (normalized is "svg" or "icon" or "icons" or "sprite")
            return "icons";
        return "images";
    }

    internal static string SectionDataKey(CloneSectionInfo section, int index)
    {
        var type = NormalizeType(section.Type ?? section.Semantic);
        return string.IsNullOrWhiteSpace(section.Id)
            ? $"clone-{index + 1:000}-{type}"
            : SanitizeSlug(section.Id!);
    }

    internal static string SectionSpecFileName(CloneSectionInfo section, int index)
    {
        var name = SanitizeSlug(section.Id ?? section.Type ?? section.Semantic ?? $"section-{index + 1:000}");
        return $"{index + 1:000}-{name}.spec.md";
    }

    private static IEnumerable<string> RewriteUrls(IEnumerable<string> urls, IReadOnlyDictionary<string, string> assetMap)
        => urls.Select(x => RewriteUrl(x, assetMap));

    private static string RewriteUrls(string html, IReadOnlyDictionary<string, string> assetMap)
    {
        var result = html;
        foreach (var kv in assetMap)
            result = result.Replace(kv.Key, kv.Value, StringComparison.OrdinalIgnoreCase);
        return result;
    }

    private static string RewriteUrl(string url, IReadOnlyDictionary<string, string> assetMap)
        => assetMap.TryGetValue(url, out var local) ? local : url;

    private static string NormalizeType(string? type)
    {
        var text = (type ?? "rich_section").Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
        if (text is "nav" or "navbar" or "header")
            return "navigation";
        if (text is "feature" or "features_grid" or "feature_grid")
            return "features";
        if (text is "call_to_action" or "call-to-action")
            return "cta";
        return SafeIdentifierRegex().Replace(text, "_").Trim('_') switch
        {
            "" => "rich_section",
            var normalized => normalized
        };
    }

    private static string PartialFor(string type) => type switch
    {
        "navigation" => "clone-navigation",
        "hero" => "clone-hero",
        "features" => "clone-feature-grid",
        "pricing" => "clone-pricing",
        "faq" => "clone-faq",
        "cta" => "clone-cta",
        "footer" => "clone-footer",
        _ => "clone-section"
    };

    private static IEnumerable<string> CommonPartials()
        => ["clone-navigation", "clone-hero", "clone-feature-grid", "clone-pricing", "clone-faq", "clone-cta", "clone-footer"];

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
        sb.AppendLine($"author: {YamlScalar(author)}");
        sb.AppendLine("license: MIT");
        sb.AppendLine($"tags: [{string.Join(", ", tags)}]");
        sb.AppendLine("params:");
        sb.AppendLine("  - key: brand");
        sb.AppendLine("    label: Site Brand");
        sb.AppendLine("    type: string");
        sb.AppendLine($"    default: {YamlScalar(author)}");
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

    private static YamlMappingNode GetOrCreateMapping(YamlMappingNode parent, string key)
    {
        var k = new YamlScalarNode(key);
        if (parent.Children.TryGetValue(k, out var existing) && existing is YamlMappingNode map)
            return map;

        var created = new YamlMappingNode();
        parent.Children[k] = created;
        return created;
    }

    private static YamlSequenceNode GetOrCreateSequence(YamlMappingNode parent, string key)
    {
        var k = new YamlScalarNode(key);
        if (parent.Children.TryGetValue(k, out var existing) && existing is YamlSequenceNode seq)
            return seq;

        var created = new YamlSequenceNode();
        parent.Children[k] = created;
        return created;
    }

    private static string? GetScalar(YamlMappingNode map, string key)
        => map.Children.TryGetValue(new YamlScalarNode(key), out var node) && node is YamlScalarNode scalar ? scalar.Value : null;

    private static string SanitizeSlug(string value)
    {
        var slug = SafeSlugRegex().Replace(value.Trim().ToLowerInvariant(), "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "clone-section" : slug;
    }

    private static string SanitizeFileName(string value)
    {
        var sb = new StringBuilder();
        foreach (var c in value)
            sb.Append(char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '_');
        return sb.Length == 0 ? "asset.img" : sb.ToString();
    }

    private static bool IsSafeCssName(string key)
        => CssNameRegex().IsMatch(key);

    private static string YamlScalar(string value)
        => "'" + value.Replace("'", "''") + "'";

    private static void AppendBlockScalar(StringBuilder sb, string key, string value)
    {
        sb.AppendLine($"{key}: |-");
        if (string.IsNullOrEmpty(value))
        {
            sb.AppendLine("  ");
            return;
        }

        foreach (var line in value.ReplaceLineEndings("\n").Split('\n'))
            sb.AppendLine("  " + line);
    }

    private static string Html(string value)
        => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static string HtmlAttr(string value)
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

    [GeneratedRegex("[^a-z0-9_-]+", RegexOptions.IgnoreCase)]
    private static partial Regex SafeIdentifierRegex();

    [GeneratedRegex("[^a-z0-9-]+", RegexOptions.IgnoreCase)]
    private static partial Regex SafeSlugRegex();

    [GeneratedRegex("^[a-zA-Z-][a-zA-Z0-9-]*$")]
    private static partial Regex CssNameRegex();

    private sealed record NormalizedSection(CloneSectionInfo Source, string Key, string Type, int Order, string CssClass);
}

internal sealed record CloneContentWriteResult(
    int ThemeFileCount,
    int ContentFileCount,
    int DataFileCount,
    int SectionCount,
    bool ConfigUpdated,
    List<string> Warnings);
