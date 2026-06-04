# Bukit AI Demo-to-CMS Engineering Specification

## 1. Purpose

This specification defines a staged workflow for generating, validating, and converting AI-created website Demos into Bukit projects.

The target output is a maintainable Bukit site with:

```text
Bukit theme
site.yaml
content seed
Notion seed
notion-database-map.yaml
import-report.md
validation commands
```

It applies to ChatGPT, Codex, Claude Code, Cursor, Trae, and other AI agents.

## 2. Supported Use Cases

This workflow is suitable for:

- Corporate websites
- Industry news sites
- Company directories
- Product showcase websites
- Landing pages
- Local service websites
- SEO / GEO content websites
- Notion-backed static sites
- Multi-language content sites

It is not suitable for:

- Highly interactive SaaS applications
- Complex authenticated dashboards
- Transactional applications
- Client-rendered apps that depend on runtime JavaScript for core content

## 3. Standard Workflow

```text
User requirements
-> AI generates a site blueprint
-> AI generates a migratable HTML Demo
-> User confirms visual style and functionality
-> AI / Bukit converts the Demo into a Bukit project
-> Bukit validates with doctor and build
-> Content is pushed to Notion when required
-> Notion-only build can be enabled
-> Site is deployed
```

## 4. Requirements Analysis

Before generating a Demo, the AI should identify:

```text
Site name
Theme name
Site purpose
Target audience
Core sections
Page list
Visual style
Language
Content collections
Notion CMS requirement
Multi-database Notion requirement
Local preview requirement
```

Example:

```text
Site name: Silkroad Business
Theme: silkroadbiz
Purpose: China-Malaysia business news and company directory
Core entries: Business insights, company directory
Pages: Home, insights, post detail, companies, company detail, about, contact, join
Content collections: pages, posts, companies, services, sections, faqs
CMS: Notion multi-database
Build strategy: Markdown preview first, Notion-only later
```

## 5. Demo Generation Requirements

The Demo must be a previewable set of HTML files:

```text
demo/
  index.html
  insights.html
  article-detail.html
  companies.html
  china-companies.html
  malaysia-companies.html
  company-detail.html
  about.html
  contact.html
  join.html
  assets/
    css/style.css
    js/main.js
    images/
demo.routes.yaml
```

If a page is not needed, it may be omitted, but the omission must be explained.

## 6. Semantic HTML Requirements

Each page should include:

```html
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <title>Page Title</title>
  <meta name="description" content="Page SEO description">
  <link rel="stylesheet" href="assets/css/style.css">
</head>
<body data-page-type="Page">
  <header class="site-header"></header>
  <nav class="site-nav"></nav>
  <main></main>
  <footer class="site-footer"></footer>
  <script src="assets/js/main.js"></script>
</body>
</html>
```

The AI should avoid deeply nested, unstructured, or decorative-only markup that makes content extraction difficult.

## 7. Page Type Requirements

Allowed page types:

```text
Home
Page
PostList
PostDetail
CompanyList
CompanyDetail
ServiceList
ServiceDetail
Contact
Join
```

Page type may be expressed in HTML with `data-page-type` and must also be represented in `demo.routes.yaml`.

## 8. Stable Class and Field Rules

Article card:

```html
<article class="article-card" data-collection="posts">
  <img data-field="cover" src="assets/images/news-1.jpg" alt="Post cover">
  <span data-field="category">Business Insight</span>
  <h3 data-field="title">Post title</h3>
  <p data-field="summary">Post summary</p>
  <a data-field="url" href="article-detail.html">Read more</a>
</article>
```

Company card:

```html
<article class="company-card" data-collection="companies">
  <img data-field="logo" src="assets/images/company-1.png" alt="Company logo">
  <h3 data-field="title">Company Name</h3>
  <p data-field="summary">Company summary</p>
  <span data-field="country">Malaysia</span>
  <span data-field="industry">Technology</span>
  <a data-field="url" href="company-detail.html">View company</a>
</article>
```

Service card:

```html
<article class="service-card" data-collection="services">
  <h3 data-field="title">Service Name</h3>
  <p data-field="summary">Service summary</p>
  <a data-field="url" href="service-detail.html">Learn more</a>
</article>
```

FAQ item:

```html
<div class="faq-item" data-collection="faqs">
  <h3 data-field="question">Question</h3>
  <p data-field="answer">Answer.</p>
</div>
```

## 9. Route Map Requirements

Every HTML file must appear in `demo.routes.yaml`.

Example:

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

Rules:

- `source` must point to a real HTML file.
- `route` must start with `/`.
- Detail routes must use `{slug}`.
- List and detail pages must be separate.
- `template` should be stable and should not include `.html`.

## 10. User Confirmation Gate

The AI must not proceed to final Bukit engineering until the user confirms:

```text
Visual style
Page structure
Navigation
List cards
Detail pages
Mobile behavior
CTA
Copy direction
Image style
URL structure
Content collections
```

This confirmation gate prevents repeated changes at the template and configuration layer.

## 11. Bukit Project Output

After the Demo is confirmed, conversion should produce:

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

## 12. Conversion Rules

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
| FAQ / sections / media / components | Review-only seed by default |

## 13. Template Rules

Detail page:

```html
<h1>{{ page.title }}</h1>
<p>{{ page.summary }}</p>
<div class="content">
  {{ page.content }}
</div>
```

List page:

```html
{{ for item in pages }}
  {{ include "components/article-card.html" }}
{{ end }}
```

Component:

```html
<article class="company-card">
  <h3>{{ item.title }}</h3>
  <p>{{ item.summary }}</p>
  <span>{{ item.country }}</span>
  <span>{{ item.industry }}</span>
  <a href="{{ item.url }}">View company</a>
</article>
```

## 14. Content Scope

Default Notion push collections:

```text
pages
posts
companies
services
```

Default review-only collections:

```text
sections
faqs
media
components
```

If review-only collections need to be CMS-managed, define dedicated schemas first.

## 15. Configuration Contracts

Configuration files must follow:

```text
docs/ai-demo-to-bukit/config/
```

Machine-readable schemas are located in:

```text
schemas/
```

## 16. Required Validation

Every converted project must run:

```bash
bukit doctor --config sites/<site-name>/site.yaml
bukit build --config sites/<site-name>/site.yaml
```

If supported:

```bash
bukit config validate --config sites/<site-name>/site.yaml
bukit doctor --config sites/<site-name>/site.yaml --strict
```

## 17. Import Report Review

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

If hardcoded content residue is high, return to the Demo or template extraction stage.

## 18. Notion Push

Recommended command:

```bash
bukit notion push   --input sites/<site-name>/notion-seed   --database-map sites/<site-name>/notion-seed/notion-database-map.yaml   --create-missing-databases   --parent-page-id <notion-parent-page-id>   --mode upsert   --update-content replace
```

## 19. Notion-only Build

After Notion content is ready:

```bash
bukit import html-demo ./demo   --theme <theme-name>   --content-source notion   --build-source notion   --route-map demo.routes.yaml   --force
```

## 20. Failure Handling

If any validation command fails, the AI must:

1. Read the error.
2. Identify whether it is a schema, path, template, data, or source problem.
3. Fix the configuration or generated files.
4. Re-run validation.
5. Not mark the task complete until validation passes.
