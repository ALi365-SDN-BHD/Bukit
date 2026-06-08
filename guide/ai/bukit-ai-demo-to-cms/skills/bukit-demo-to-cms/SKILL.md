---
name: bukit-demo-to-cms
description: Generate a migratable HTML Demo, wait for user confirmation, then convert it into a Bukit theme, content seed, Notion configuration, and buildable site.
---

# Bukit Demo-to-CMS Skill

## Purpose

Use this skill when the user wants to design, generate, convert, validate, or publish a website that will ultimately run on Bukit.

The workflow is:

```text
User requirements
-> AI-generated visual HTML Demo
-> User confirms style and functionality
-> AI / Bukit converts Demo into Bukit theme, content data, Notion seed, and configuration
-> Bukit validates, builds, and publishes
```

The goal is not to generate disposable HTML. The goal is to produce a maintainable Bukit site that can use Notion as a CMS.

## Core Rules

1. Generate a visual HTML Demo first unless the user explicitly requests a direct Bukit project.
2. Do not generate the final Bukit project before the user confirms the Demo.
3. The Demo must be designed for migration.
4. Every Demo page must be represented in `demo.routes.yaml`.
5. Repeated UI structures must use stable class names.
6. Business content must be extractable into content data or Notion seed.
7. Theme templates must contain structure and presentation, not long-lived business copy.
8. After conversion, always provide validation and build commands.
9. Default workflow uses Notion seed with Markdown local preview.
10. Use Notion-only build only after the CMS content source is ready.

## Stage 1: Requirements Analysis

Before generating the Demo, identify:

```text
Site name
Theme name
Site purpose
Target audience
Core sections
Page list
Visual style
Languages
Content collections
Notion CMS requirement
Multi-database Notion requirement
Local preview requirement
```

## Stage 2: Generate a Migratable HTML Demo

Expected structure:

```text
demo/
  index.html
  insights.html
  article-detail.html
  companies.html
  company-detail.html
  about.html
  contact.html
  assets/
    css/style.css
    js/main.js
    images/
demo.routes.yaml
```

## Stage 3: Wait for User Confirmation

Do not continue to final Bukit engineering until the user confirms the Demo.

## Stage 4: Convert the Final Demo into a Bukit Project

Expected result:

```text
themes/<theme-name>/
sites/<site-name>/
notion-seed/
site.yaml
notion-database-map.yaml
import-report.md
```

## Stage 5: Validate

Always run:

```bash
bukit doctor --config sites/<site-name>/site.yaml
bukit build --config sites/<site-name>/site.yaml
```


## Configuration Generation Rules

When generating Bukit configuration files, the AI must follow these rules:

1. Do not invent `site.yaml` fields.
2. Select a standard Profile before generating `site.yaml`.
3. Reference `docs/ai-demo-to-bukit/config/site-yaml-spec.md`.
4. Generate only `content.sources[]`; never generate `content.provider`.
5. `--build-source notion` requires `--content-source notion`.
6. Notion multi-database mode must use `content.sources`.
7. After generating configuration, run schema validation, `bukit doctor`, and `bukit build`.
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

Expected future diagnostics from Bukit doctor:

```text
Unknown field: content.notion.database
Missing required field: content.sources[0].collection
Removed field: content.provider
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
schemas/
```
