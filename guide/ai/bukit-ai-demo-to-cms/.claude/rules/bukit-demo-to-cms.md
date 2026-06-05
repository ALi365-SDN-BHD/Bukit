# Bukit AI Demo-to-CMS Rule

## Scope

Apply this rule when working on HTML Demo generation, Bukit theme generation, Demo conversion, content seed generation, Notion CMS integration, or Bukit build validation.

## Required Workflow

```text
Requirements
→ Migratable HTML Demo
→ User confirmation
→ Bukit theme + content data + configuration
→ Validation
→ Notion CMS
→ Build
```

## Mandatory Rules

- Do not generate disposable HTML.
- Do not skip `demo.routes.yaml`.
- Do not combine list and detail pages.
- Do not keep business copy permanently inside templates.
- Use semantic HTML and local assets.
- Use stable classes: `article-card`, `company-card`, `service-card`, `faq-item`.
- Use `data-field` attributes for extractable content.
- Convert shared structures into partials.
- Convert repeated structures into components.
- Generate content seed and `notion-database-map.yaml`.
- Default Notion push scope is `pages/posts/companies/services`.
- Treat `sections/faqs/media/components` as review-only unless a dedicated schema is defined.
- Do not use `--build-source notion` with a non-Notion content source.

## Demo Requirements

Every Demo should include:

```text
demo/
  index.html
  <other pages>.html
  assets/
demo.routes.yaml
```

## Bukit Project Requirements

The converted project should include:

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

## Recommended Import

```bash
bukit import html-demo ./demo   --theme <theme-name>   --content-source notion   --build-source markdown   --route-map demo.routes.yaml   --strict warn   --force   --verify
```

## Validation

```bash
bukit doctor --config sites/<site-name>/site.yaml
bukit build --config sites/<site-name>/site.yaml
```

Review `import-report.md` before marking the task complete.
