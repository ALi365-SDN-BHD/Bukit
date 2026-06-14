using System.Text;

namespace Bukit.Labs.Cli.Commands;

internal static class CloneIndexPageGenerator
{
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

        if (layout.ExtraSections.Any(s => s.States.Count >= 2))
        {
            sb.AppendLine();
            sb.AppendLine("<script>(function(){document.querySelectorAll('.state-section').forEach(function(sec){var tabs=sec.querySelectorAll('.state-tab');tabs.forEach(function(tab){tab.addEventListener('click',function(){var panelId=tab.getAttribute('aria-controls');tabs.forEach(function(t){t.setAttribute('aria-selected','false');});tab.setAttribute('aria-selected','true');sec.querySelectorAll('.state-panel').forEach(function(p){p.classList.add('hidden');});var panel=document.getElementById(panelId);if(panel)panel.classList.remove('hidden');});});});})();</script>");
        }

        return sb.ToString();
    }

    internal static void GenerateStaticSection(StringBuilder sb, SectionInfo section)
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

    internal static string GenerateResponsiveCss(SectionInfo section)
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

    internal static void GenerateStateSection(StringBuilder sb, SectionInfo section, List<string>? warnings)
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
}
