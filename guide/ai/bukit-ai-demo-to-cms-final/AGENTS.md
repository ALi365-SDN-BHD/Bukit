# AGENTS.md

## Bukit AI Demo-to-CMS Instructions

When working in this repository, treat Bukit website generation as a staged engineering workflow.

## Required Workflow

```text
User requirements
→ Generate a migratable HTML Demo
→ User confirms style and functionality
→ Convert the confirmed Demo into a Bukit theme and content project
→ Validate with Bukit
→ Push content to Notion when required
→ Build and publish
```

## Do Not

- Do not generate disposable HTML that cannot be migrated.
- Do not skip the Demo confirmation stage unless explicitly requested.
- Do not keep long-term business content inside templates.
- Do not merge list and detail pages into a single static template.
- Do not use unstable or random template names.
- Do not depend on complex runtime JavaScript for content rendering.
- Do not create a Notion-only build with non-Notion seed sources.

## Demo Requirements

Every Demo must:

- Use semantic HTML
- Use local assets under `assets/`
- Include `demo.routes.yaml`
- Separate list and detail pages
- Use stable classes:
  - `article-card`
  - `company-card`
  - `service-card`
  - `faq-item`
- Use `data-field` markers for extractable content

## Bukit Project Requirements

The converted Bukit project should include:

```text
themes/<theme-name>/
  layouts/layouts/base.html
  layouts/pages/*.html
  layouts/partials/*.html
  layouts/components/*.html
  layouts/bukit.templates.yaml
  assets/

sites/<site-name>/
  site.yaml
  content/
  notion-seed/
  import-report.md
```

## Content Rules

Default Notion push collections:

```text
pages
posts
companies
services
```

Default review-only seed collections:

```text
sections
faqs
media
components
```

## Preferred Commands

```bash
bukit import html-demo ./demo   --theme <theme-name>   --content-source notion   --build-source markdown   --route-map demo.routes.yaml   --strict warn   --force   --verify
```

```bash
bukit notion push   --input sites/<site-name>/notion-seed   --database-map sites/<site-name>/notion-seed/notion-database-map.yaml   --create-missing-databases   --parent-page-id <notion-parent-page-id>   --mode upsert   --update-content replace
```

## Validation

Before considering a task complete:

```bash
bukit doctor --config sites/<site-name>/site.yaml
bukit build --config sites/<site-name>/site.yaml
```

Review `import-report.md`, especially:

```text
Hardcoded Content Residue
Diagnostics
Link Validation
Visual Verification
Manual Review Required
```

## Configuration Generation Rules

When generating Bukit configuration files:

1. Do not invent `site.yaml` fields.
2. Select a standard Profile before generating `site.yaml`.
3. Reference `docs/ai-demo-to-bukit/config/site-yaml-spec.md`.
4. Do not generate `content.provider` and `content.sources` together.
5. `--build-source notion` requires `--content-source notion`.
6. Notion multi-database mode must use `content.sources`.
7. After generating configuration, run schema validate, `bukit doctor`, and `bukit build`.
8. If validation fails, fix the configuration. Do not ignore errors.

Required validation commands:

```bash
bukit doctor --config sites/<site-name>/site.yaml
bukit build --config sites/<site-name>/site.yaml
```

If supported:

```bash
bukit config validate --config sites/<site-name>/site.yaml
bukit doctor --config sites/<site-name>/site.yaml --strict
```

Expected future diagnostic examples:

```text
Unknown field: content.notion.database
Missing required field: content.sources[0].collection
Invalid combination: content.provider and content.sources cannot coexist
Invalid build source: notion requires content source notion
```

Required configuration references:

```text
docs/ai-demo-to-bukit/config/site-yaml-spec.md
docs/ai-demo-to-bukit/config/site-yaml-profiles.md
docs/ai-demo-to-bukit/config/seed-data-spec.md
docs/ai-demo-to-bukit/config/demo-routes-spec.md
docs/ai-demo-to-bukit/config/notion-database-map-spec.md
docs/ai-demo-to-bukit/config/template-manifest-spec.md
docs/ai-demo-to-bukit/config/environment-variables-spec.md
```
