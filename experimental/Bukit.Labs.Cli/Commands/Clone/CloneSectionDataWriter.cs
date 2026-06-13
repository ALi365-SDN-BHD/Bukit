using System.Text;

namespace Bukit.Cli.Commands;

internal static class CloneSectionDataWriter
{
    internal static IEnumerable<NormalizedSection> NormalizeSections(IReadOnlyList<CloneSectionInfo> sections)
    {
        for (var i = 0; i < sections.Count; i++)
        {
            var section = sections[i];
            var order = section.Order ?? ((i + 1) * 10);
            var type = CloneContentAssetHelpers.NormalizeType(section.Type ?? section.Semantic);
            var key = CloneContentAssetHelpers.SectionDataKey(section, i);
            var cssClass = $"clone-section-{i + 1:000}";
            yield return new NormalizedSection(section, key, type, order, cssClass);
        }
    }

    internal static string GenerateSectionData(NormalizedSection normalized, IReadOnlyDictionary<string, string> assetMap)
    {
        var section = normalized.Source;
        var title = section.DisplayTitle;
        var body = BuildSectionBody(normalized, assetMap);

        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"title: {CloneYamlWriter.YamlScalar(title)}");
        sb.AppendLine($"type: {CloneYamlWriter.YamlScalar(normalized.Type)}");
        sb.AppendLine($"order: {normalized.Order}");
        sb.AppendLine("enabled: true");
        sb.AppendLine($"clone_key: {CloneYamlWriter.YamlScalar(normalized.Key)}");
        sb.AppendLine($"clone_class: {CloneYamlWriter.YamlScalar(normalized.CssClass)}");
        if (!string.IsNullOrWhiteSpace(section.Semantic))
            sb.AppendLine($"semantic: {CloneYamlWriter.YamlScalar(section.Semantic!)}");
        if (!string.IsNullOrWhiteSpace(section.Eyebrow))
            sb.AppendLine($"eyebrow: {CloneYamlWriter.YamlScalar(section.Eyebrow!)}");
        if (!string.IsNullOrWhiteSpace(section.Subheading))
            sb.AppendLine($"subheading: {CloneYamlWriter.YamlScalar(section.Subheading!)}");
        if (section.Buttons.Count > 0)
            sb.AppendLine($"buttons_json: {CloneYamlWriter.YamlScalar(CloneJson.Serialize(section.Buttons))}");
        if (section.Items.Count > 0)
            sb.AppendLine($"items_json: {CloneYamlWriter.YamlScalar(CloneJson.Serialize(section.Items))}");
        if (section.Components.Count > 0)
            sb.AppendLine($"components_json: {CloneYamlWriter.YamlScalar(CloneJson.Serialize(section.Components))}");
        var imageUrls = CloneContentAssetHelpers.RewriteUrls(section.ImageUrls.Concat(section.Assets.Select(a => a.Src)), assetMap)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (imageUrls.Count > 0)
            sb.AppendLine($"image_urls_json: {CloneYamlWriter.YamlScalar(CloneJson.Serialize(imageUrls))}");
        if (section.Styles is { Count: > 0 })
            sb.AppendLine($"styles_json: {CloneYamlWriter.YamlScalar(CloneJson.Serialize(section.Styles))}");
        if (section.ComputedStyles is { Count: > 0 })
            sb.AppendLine($"computed_styles_json: {CloneYamlWriter.YamlScalar(CloneJson.Serialize(section.ComputedStyles))}");
        if (section.Bounds is not null)
            sb.AppendLine($"bounds_json: {CloneYamlWriter.YamlScalar(CloneJson.Serialize(section.Bounds))}");
        if (section.Interactions.Count > 0)
            sb.AppendLine($"interactions_json: {CloneYamlWriter.YamlScalar(CloneJson.Serialize(section.Interactions))}");
        if (section.HasStates)
            sb.AppendLine($"states_json: {CloneYamlWriter.YamlScalar(CloneJson.Serialize(section.States))}");
        if (section.Responsive is not null)
            sb.AppendLine($"responsive_json: {CloneYamlWriter.YamlScalar(CloneJson.Serialize(section.Responsive))}");
        CloneYamlWriter.AppendBlockScalar(sb, "content_html", body);
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine(body);
        return sb.ToString();
    }

    internal static string BuildSectionBody(NormalizedSection normalized, IReadOnlyDictionary<string, string> assetMap)
    {
        var section = normalized.Source;
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(section.ContentHtml))
            sb.AppendLine(CloneContentAssetHelpers.RewriteUrls(section.ContentHtml!, assetMap));

        if (!string.IsNullOrWhiteSpace(section.Text))
            sb.AppendLine($"<p>{CloneContentWriter.Html(section.Text!)}</p>");

        foreach (var image in CloneContentAssetHelpers.RewriteUrls(section.ImageUrls, assetMap))
            sb.AppendLine($"<img src=\"{CloneContentWriter.HtmlAttr(image)}\" alt=\"\" loading=\"lazy\" />");

        if (section.Items.Count > 0)
        {
            sb.AppendLine("<div class=\"clone-items\">");
            foreach (var item in section.Items)
            {
                sb.AppendLine("  <article class=\"clone-item\">");
                if (!string.IsNullOrWhiteSpace(item.Image))
                    sb.AppendLine($"    <img src=\"{CloneContentWriter.HtmlAttr(CloneContentAssetHelpers.RewriteUrl(item.Image!, assetMap))}\" alt=\"\" loading=\"lazy\" />");
                if (!string.IsNullOrWhiteSpace(item.Title))
                    sb.AppendLine($"    <h3>{CloneContentWriter.Html(item.Title!)}</h3>");
                var text = item.Description ?? item.Text;
                if (!string.IsNullOrWhiteSpace(text))
                    sb.AppendLine($"    <p>{CloneContentWriter.Html(text!)}</p>");
                if (!string.IsNullOrWhiteSpace(item.Url))
                    sb.AppendLine($"    <a class=\"clone-link\" href=\"{CloneContentWriter.HtmlAttr(item.Url!)}\">{CloneContentWriter.Html(item.Title ?? item.Url!)}</a>");
                sb.AppendLine("  </article>");
            }
            sb.AppendLine("</div>");
        }

        foreach (var button in section.Buttons)
        {
            if (string.IsNullOrWhiteSpace(button.Label))
                continue;
            sb.AppendLine($"<a class=\"clone-button clone-button-{CloneContentWriter.HtmlAttr(button.Variant ?? "primary")}\" href=\"{CloneContentWriter.HtmlAttr(button.Url ?? "#")}\">{CloneContentWriter.Html(button.Label!)}</a>");
        }

        if (section.States.Count > 0)
        {
            sb.AppendLine("<div class=\"state-section\">");
            sb.AppendLine("  <div class=\"state-tabs\" role=\"tablist\">");
            for (var i = 0; i < section.States.Count; i++)
            {
                var selected = i == 0 ? "true" : "false";
                sb.AppendLine($"    <button class=\"state-tab\" role=\"tab\" aria-selected=\"{selected}\" aria-controls=\"{normalized.Key}-state-{i}\">{CloneContentWriter.Html(section.States[i].Label ?? $"State {i + 1}")}</button>");
            }
            sb.AppendLine("  </div>");
            for (var i = 0; i < section.States.Count; i++)
            {
                var hidden = i == 0 ? "" : " hidden";
                sb.AppendLine($"  <div class=\"state-panel{hidden}\" role=\"tabpanel\" id=\"{normalized.Key}-state-{i}\">");
                sb.AppendLine(CloneContentAssetHelpers.RewriteUrls(section.States[i].ContentHtml ?? "", assetMap));
                sb.AppendLine("  </div>");
            }
            sb.AppendLine("</div>");
        }

        return sb.Length == 0 ? "<!-- cloned empty section -->" : sb.ToString();
    }

    internal static string GenerateStructuredIndex(IReadOnlyList<NormalizedSection> sections)
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

    internal static void AppendResponsiveCss(StringBuilder sb, NormalizedSection section)
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

    internal static string PartialFor(string type) => type switch
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

    internal static IEnumerable<string> CommonPartials()
        => ["clone-navigation", "clone-hero", "clone-feature-grid", "clone-pricing", "clone-faq", "clone-cta", "clone-footer"];
}

internal sealed record NormalizedSection(CloneSectionInfo Source, string Key, string Type, int Order, string CssClass);
