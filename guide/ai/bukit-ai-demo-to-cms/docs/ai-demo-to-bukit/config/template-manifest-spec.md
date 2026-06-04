# `bukit.templates.yaml` Template Manifest Specification

## 1. Purpose

`bukit.templates.yaml` describes the layout, page, partial, and component templates available in a theme.

AI must keep the manifest consistent with actual template files.

## 2. Recommended Structure

```yaml
layouts:
  base: layouts/base.html

pages:
  index: pages/index.html
  insights: pages/insights.html
  article: pages/article.html
  companies: pages/companies.html
  company: pages/company.html
  about: pages/about.html
  contact: pages/contact.html

partials:
  header: partials/header.html
  nav: partials/nav.html
  footer: partials/footer.html

components:
  article-card: components/article-card.html
  company-card: components/company-card.html
  service-card: components/service-card.html
  faq: components/faq.html
```

## 3. Naming Rules

- Keys should use lowercase and hyphenated names.
- Values must be relative to the theme `layouts/` directory.
- Page templates should use `pages/*.html`.
- Partials should use `partials/*.html`.
- Components should use `components/*.html`.
- Do not use `..`.
- Do not use absolute paths.
- Do not duplicate keys.

## 4. Relationship to Route Map

If `demo.routes.yaml` contains:

```yaml
template: company
```

then `bukit.templates.yaml` should contain:

```yaml
pages:
  company: pages/company.html
```

## 5. Validation

```text
Every template file exists
Every route-map template exists in pages
All include paths are valid
No absolute paths
No .. paths
```
