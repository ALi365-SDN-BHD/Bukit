# Bukit Core Agent Skills

Skills that teach an AI assistant to build and maintain websites with stable Bukit
Core. They describe current Core behavior only; the source of truth is
`src/Bukit-Core` (`BukitCliSpecs.cs` for commands, `AppConfig.cs` and the config
validators for `site.yaml`). Labs workflows live in
[`guide/labs-skills`](../labs-skills/README.md) and are opt-in.

Start with [`using-bukit`](using-bukit/SKILL.md), then load the narrow skill for the
task.

| Skill | Use |
|---|---|
| [`using-bukit`](using-bukit/SKILL.md) | Gateway: ground rules, project layout, standard loop, skill map. |
| [`bukit-cli-reference`](bukit-cli-reference/SKILL.md) | Commands, options, exit codes, environment variables. |
| [`bukit-config`](bukit-config/SKILL.md) | `site.yaml` structure, defaults, validation, plugin toggles. |
| [`bukit-content`](bukit-content/SKILL.md) | Content sources, Markdown, front matter, data. |
| [`bukit-content-model`](bukit-content-model/SKILL.md) | Notion sources, remote media, model schema, route metadata. |
| [`bukit-routing`](bukit-routing/SKILL.md) | Collections, permalinks, lists, pagination, taxonomy, conflicts. |
| [`bukit-templating`](bukit-templating/SKILL.md) | Scriban templates, model, helpers, limits. |
| [`bukit-theme`](bukit-theme/SKILL.md) | Theme layout, theme.yaml, assets, SCSS, images, components. |
| [`bukit-i18n`](bukit-i18n/SKILL.md) | Language filtering, multilingual output, hreflang. |
| [`bukit-build`](bukit-build/SKILL.md) | Build pipeline, clean, incremental cache, reports, security gate. |
| [`bukit-outputs`](bukit-outputs/SKILL.md) | Sitemap, feeds, search, robots.txt, aliases, 404, projections. |
| [`bukit-seo`](bukit-seo/SKILL.md) | SEO metadata, render modes, `seo audit`, `seo diff`. |
| [`bukit-seo-insights`](bukit-seo-insights/SKILL.md) | Search Console, GA4, question, AI citation, authority joins. |
| [`bukit-geo`](bukit-geo/SKILL.md) | llms.txt, GEO front matter, AI crawler policy, `geo audit`. |
| [`bukit-analytics`](bukit-analytics/SKILL.md) | Analytics providers, consent, CSP report. |
| [`bukit-local-server`](bukit-local-server/SKILL.md) | `dev` and `preview`. |
| [`bukit-publish-deploy`](bukit-publish-deploy/SKILL.md) | `publish audit`, GitHub Pages deploy, CI. |
| [`bukit-troubleshooting`](bukit-troubleshooting/SKILL.md) | Errors, events, symptom-to-fix table. |

Maintenance rules: [`MAINTENANCE.md`](MAINTENANCE.md).
