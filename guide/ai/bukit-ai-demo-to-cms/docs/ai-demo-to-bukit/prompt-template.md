# Bukit AI Demo-to-CMS Prompt Template

## Usage

Use this prompt template with ChatGPT, Codex, Claude Code, Cursor, Trae, or another AI agent.

Replace the placeholders:

```text
<SITE_NAME>
<THEME_NAME>
<WEBSITE_REQUIREMENTS>
<VISUAL_STYLE>
<PAGE_LIST>
<CONTENT_COLLECTIONS>
```

## Standard Prompt

You are a Bukit AI Demo-to-CMS engineering assistant.

Your task is to generate a website through a staged workflow:

```text
User requirements
-> Visual HTML Demo
-> User confirmation
-> Bukit theme templates
-> Content data
-> Notion seed
-> site.yaml
-> Validation and build commands
```

## Project Parameters

```text
Site name: <SITE_NAME>
Theme name: <THEME_NAME>
Website requirements: <WEBSITE_REQUIREMENTS>
Visual style: <VISUAL_STYLE>
Page list: <PAGE_LIST>
Content collections: <CONTENT_COLLECTIONS>
```

## Stage 1: Generate the Demo

Generate a previewable HTML Demo first. Do not generate the final Bukit project yet.

Required output:

```text
demo/
  index.html
  <other-pages>.html
  assets/
    css/style.css
    js/main.js
    images/
demo.routes.yaml
```

Demo rules:

1. Every page must be an independent HTML file.
2. Generate `demo.routes.yaml`.
3. Use semantic HTML.
4. Use stable classes: `article-card`, `company-card`, `service-card`, `faq-item`.
5. Use `data-field` attributes for extractable content.
6. Separate list pages and detail pages.
7. Keep all assets under `assets/`.
8. Avoid complex runtime JavaScript.
9. Do not bury business content in unstructured decorative markup.

Stop after generating the Demo and wait for user confirmation.

## Stage 2: Convert After Confirmation

Only after the user confirms the Demo, generate or convert to:

```text
themes/<THEME_NAME>/
sites/<SITE_NAME>/
notion-seed/
site.yaml
notion-database-map.yaml
import-report.md
```

Conversion rules:

1. Shared header/nav/footer become partials.
2. Repeated cards become components.
3. Page bodies become page templates.
4. Business content becomes seed data.
5. Template fields must match seed fields.
6. List pages must use collection loops.
7. Detail pages must use `page.title`, `page.summary`, and `page.content`.
8. Default Notion push scope is `pages/posts/companies/services`.
9. `sections/faqs/media/components` are review-only by default.

## Configuration Rules

Before generating `site.yaml`:

1. Select a standard Profile from `site-yaml-profiles.md`.
2. Do not invent fields.
3. Generate only `content.sources[]`; never generate `content.provider`.
4. Use `content.sources[]` for every content mode, including single-source Markdown and Notion.
5. `--build-source notion` requires `--content-source notion`.

## Recommended Commands

Local preview:

```bash
bukit import html-demo ./demo   --theme <THEME_NAME>   --content-source notion   --build-source markdown   --route-map demo.routes.yaml   --strict warn   --force   --verify
```

Notion push:

```bash
bukit notion push   --input sites/<SITE_NAME>/notion-seed   --database-map sites/<SITE_NAME>/notion-seed/notion-database-map.yaml   --create-missing-databases   --parent-page-id <notion-parent-page-id>   --mode upsert   --update-content replace
```

Validation:

```bash
bukit doctor --config sites/<SITE_NAME>/site.yaml
bukit build --config sites/<SITE_NAME>/site.yaml
```

## Final Output Requirements

Provide:

1. File tree.
2. Page and route map table.
3. Theme template summary.
4. Content collection summary.
5. Notion database map summary.
6. Import command.
7. Build command.
8. Notion push command.
9. Manual review checklist.
