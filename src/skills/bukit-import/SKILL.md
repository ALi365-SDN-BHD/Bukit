---
name: bukit-import
description: Use when converting an offline HTML demo into a Bukit theme/site draft with `bukit import html-demo`, importing generated seed files with `bukit import seed`, reviewing import reports, or pushing generated Notion seed records.

status: beta
since: "v3.0.0"
verified_by:
  - "src/Bukit.Cli/Commands/ImportCommand.cs"
  - "src/Bukit.Importing/HtmlDemoImporter.cs"
source_anchors:
  - "src/Bukit.Cli/Commands/ImportCommand.cs"
  - "src/Bukit.Importing/HtmlDemoImporter.cs"
  - "src/Bukit.Importing/ImportReportWriter.cs"
guide_chapters:
  - "guide/user/21-import-html-demo.md"
  - "guide/user/12-cli-reference.md"
---

# Import

## Overview

Use this skill for offline HTML demo conversion, seed review, and optional Notion write-through. This is different from `bukit-clone`: `bukit-clone` starts from a live URL and browser extraction, while `bukit-import` starts from a local directory of existing `.html`, CSS, image, and asset files.

The import workflow has three separable layers:

1. `bukit import html-demo` scans a local demo and generates a Bukit theme/site draft.
2. `bukit import seed` converts generated JSON/YAML seed files into local Markdown content.
3. `bukit notion push` or `import html-demo --push-notion` writes generated Notion seed records to Notion when explicitly requested.

## Multilingual Triggers

| Language | Trigger Phrases |
|---|---|
| English | "import html demo", "HTML demo import", "convert local demo", "import seed", "push generated seed to Notion" |
| 中文 | "导入 HTML demo"、"转换本地 demo"、"import html-demo"、"导入 seed"、"把 seed 推到 Notion" |
| Bahasa Melayu | "import demo HTML", "tukar demo setempat", "import seed", "tolak seed ke Notion" |

## Load Order

1. Load `using-bukit`.
2. Load `bukit-cli-reference` before executing or suggesting commands.
3. Load this skill for import-specific workflow, output, warning, and report behavior.
4. Add `bukit-theme` or `bukit-templating` only when editing generated templates.
5. Add `bukit-notion` only when the generated seed will be pushed to Notion or the generated site will build directly from Notion.

## Command Model

```bash
# Analyze only; writes no files but still reports real counts
bukit import html-demo ./demo --theme mysite --dry-run

# Generate a local-buildable site draft and verify it
bukit import html-demo ./demo --theme mysite --force --verify

# Generate Notion seed files but keep build local via Markdown
bukit import html-demo ./demo --theme mysite --content-source notion --build-source markdown

# Build directly from Notion after seed push setup
bukit import html-demo ./demo --theme mysite --content-source notion --build-source notion

# Convert generated JSON/YAML seed into Markdown content
bukit import seed sites/mysite/data --output sites/mysite/content --force
```

## Output Contract

By default, `import html-demo` generates:

- `themes/<theme>/` with layouts, partials, assets, and synced `bukit.templates.yaml`
- sites/&lt;theme&gt;/site.yaml
- `sites/<theme>/content/` Markdown drafts when `--build-source markdown` is active
- seed review files under `sites/<theme>/notion-seed/` for Notion source or `sites/<theme>/data/` for JSON/YAML source
- `sites/<theme>/original-demo/` unless `--no-preserve-html` is passed
- sites/&lt;theme&gt;/import-report.md unless `--no-report` is passed

Treat `import-report.md` as the handoff checklist. It includes scanned pages, generated components, seed files, diagnostics, hardcoded-content residue, build/data source relationship, and visual verification notes.

## Content Source vs Build Source

`--content-source` controls seed output format and review/push artifacts. It accepts `notion`, `json`, or `yaml`.

`--build-source` controls how the generated site builds. It accepts `markdown` or `notion`.

Important rules:

- Default is `--content-source notion --build-source markdown`.
- Markdown build source keeps `bukit build` and `--verify` offline.
- Notion build source is only valid with `--content-source notion`.
- `--build-source notion` skips local Markdown drafts and requires Notion credentials when the generated site is built.

## Notion Push Rules

`import html-demo` does not write to Notion by default. Use one of these explicit paths:

```bash
# Review first
bukit notion push --input sites/mysite/notion-seed --dry-run

# Push all supported seed records to one database
bukit notion push --input sites/mysite/notion-seed --database-id <id>

# Push with generated multi-database map
bukit notion push --input sites/mysite/notion-seed --database-map sites/mysite/notion-seed/notion-database-map.yaml

# Direct push after import
bukit import html-demo ./demo --theme mysite --push-notion --notion-database-id <id>
```

Default Notion push scope is `pages`, `navigation`, `posts`, `companies`, and `services`. Generated `sections`, `faqs`, `media`, and `components` are review-only until collection-specific Notion schemas are added. Imported demo menus are modeled as `navigation` data modules; with `--build-source notion`, the generated `site.yaml` maps them as `mode: data` so templates can read `site.modules.navigation`.

## Verification Workflow

For a serious import task:

1. Run `--dry-run` and inspect page/template/component/record counts.
2. Run the real import with `--force --verify`.
3. Read `sites/<theme>/import-report.md`.
4. Run `bukit doctor --config sites/<theme>/site.yaml` if verification was not included.
5. Run `bukit build --config sites/<theme>/site.yaml`.
6. Preview and compare desktop/tablet/mobile against the original demo.

## Common Errors

| Symptom | Likely Cause | Fix |
|---|---|---|
| `缺少必填选项: --theme <名称>` | `import html-demo` always requires a target theme name | Add `--theme <safe-name>` |
| `主题已存在` | The target theme directory already exists | Add `--force` only after confirming overwrite is intended |
| `--build-source notion requires --content-source notion` | Build provider and seed format conflict | Use `--content-source notion --build-source notion`, or keep default Markdown build |
| `--push-notion 不能与 --dry-run 同时使用` | Direct push has an external side effect | Run import first, then `bukit notion push --dry-run`, then push without dry run |
| Missing Notion database IDs | Generated database map has empty `databaseId` values | Fill the IDs or pass `--create-missing-notion-databases --notion-parent-page-id <id>` |
| `static HTML files in static dir are skipped` after import | HTML files were copied into theme static assets | Check `original-demo/` for preserved originals and keep `.html` out of theme `static/` |
| `seo.site_url_missing` after import | Generated config lacks production URL replacement | Replace placeholder site.url: https://example.com before publishing |

## Agent Notes

- Prefer `--build-source markdown` for first-pass import verification because it avoids live Notion credentials.
- Keep Notion writes opt-in and reviewable.
- When behavior and docs disagree, verify against `src/Bukit.Cli/Commands/ImportCommand.cs`, `src/Bukit.Importing/HtmlDemoImporter.cs`, and `src/Bukit.Importing/ImportReportWriter.cs`.
- Do not describe `import html-demo` as a pixel-perfect clone. It creates a maintainable Bukit draft that still needs visual review.
