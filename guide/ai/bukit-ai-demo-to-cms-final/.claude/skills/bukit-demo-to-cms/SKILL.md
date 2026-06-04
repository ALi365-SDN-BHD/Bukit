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
→ AI-generated visual HTML Demo
→ User confirms style and functionality
→ AI / Bukit converts Demo into Bukit theme, content data, Notion seed, and configuration
→ Bukit validates, builds, and publishes
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

### Demo Requirements

- Use semantic HTML: `header`, `nav`, `main`, `section`, `footer`
- Use local assets under `assets/`
- Separate list pages and detail pages
- Avoid complex runtime JavaScript
- Include `<title>` and SEO description
- Use stable classes:
  - `article-card`
  - `company-card`
  - `service-card`
  - `faq-item`
- Use `data-field` attributes for extractable content:
  - `title`
  - `summary`
  - `content`
  - `cover`
  - `logo`
  - `country`
  - `industry`
  - `question`
  - `answer`

## Stage 3: Generate `demo.routes.yaml`

Every HTML file must appear in the route map.

```yaml
pages:
  - source: index.html
    route: /
    type: Home
    template: index

  - source: insights.html
    route: /insights/
    type: PostList
    template: insights

  - source: article-detail.html
    route: /insights/{slug}/
    type: PostDetail
    template: article

  - source: companies.html
    route: /companies/
    type: CompanyList
    template: companies

  - source: company-detail.html
    route: /companies/{slug}/
    type: CompanyDetail
    template: company
```

## Stage 4: Wait for User Confirmation

Do not continue to final Bukit engineering until the user confirms:

```text
Visual style
Page structure
Navigation
List cards
Detail pages
Mobile layout
CTA
Copy direction
Image style
URL structure
Content collections
```

## Stage 5: Convert the Final Demo into a Bukit Project

Expected result:

```text
themes/<theme-name>/
  layouts/
    layouts/base.html
    pages/*.html
    partials/header.html
    partials/nav.html
    partials/footer.html
    components/*.html
    bukit.templates.yaml
  assets/

sites/<site-name>/
  site.yaml
  content/
  notion-seed/
    pages.json
    posts.json
    companies.json
    services.json
    sections.json
    faqs.json
    media.json
    components.json
    notion-database-map.yaml
  import-report.md
```

### Conversion Rules

| Demo Element | Bukit Target |
|---|---|
| Shared header | `layouts/partials/header.html` |
| Shared nav | `layouts/partials/nav.html` |
| Shared footer | `layouts/partials/footer.html` |
| Page body | `layouts/pages/*.html` |
| Repeated cards | `layouts/components/*.html` |
| CSS / JS / images | `themes/<theme>/assets/` |
| Page content | `notion-seed/pages.json` |
| Post content | `notion-seed/posts.json` |
| Company content | `notion-seed/companies.json` |
| Service content | `notion-seed/services.json` |
| FAQ / section / media | Review-only seed by default |

## Stage 6: Recommended Commands

### Local Preview

```bash
bukit import html-demo ./demo   --theme <theme-name>   --content-source notion   --build-source markdown   --route-map demo.routes.yaml   --strict warn   --force   --verify
```

### Push to Notion

```bash
bukit notion push   --input sites/<site-name>/notion-seed   --database-map sites/<site-name>/notion-seed/notion-database-map.yaml   --create-missing-databases   --parent-page-id <notion-parent-page-id>   --mode upsert   --update-content replace
```

### Notion-only Build

```bash
bukit import html-demo ./demo   --theme <theme-name>   --content-source notion   --build-source notion   --route-map demo.routes.yaml   --force
```

## Validation Requirements

Always review:

```text
Pages
Content Seeds
Seed Push Scope
Build/Data Source Relationship
Hardcoded Content Residue
Diagnostics
Link Validation
Visual Verification
Manual Review Required
```

Always run:

```bash
bukit doctor --config sites/<site-name>/site.yaml
bukit build --config sites/<site-name>/site.yaml
```

## Final Output Requirements

When completing a Demo-to-Bukit task, provide:

1. File tree
2. Page and route map table
3. Theme template summary
4. Content collection summary
5. Notion database map summary
6. Import command
7. Build command
8. Notion push command
9. Manual review checklist
