# `demo.routes.yaml` Route Mapping Specification

## 1. Purpose

`demo.routes.yaml` maps HTML Demo pages to Bukit page types, URLs, and templates.

AI must not rely on filename guessing. Every HTML file must appear in the route map.

## 2. Structure

```yaml
pages:
  - source: index.html
    route: /
    type: Home
    template: index
```

## 3. Fields

| Field | Type | Required | Description |
|---|---|---:|---|
| `source` | string | yes | HTML file in the Demo |
| `route` | string | yes | Target URL |
| `type` | string | yes | Page type |
| `template` | string | yes | Bukit template name |
| `slug` | string | no | Explicit slug |
| `description` | string | no | Page description |

## 4. Allowed Page Types

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

## 5. Standard Example

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

  - source: china-companies.html
    route: /china-companies/
    type: CompanyList
    template: china-companies

  - source: malaysia-companies.html
    route: /malaysia-companies/
    type: CompanyList
    template: malaysia-companies

  - source: company-detail.html
    route: /companies/{slug}/
    type: CompanyDetail
    template: company

  - source: about.html
    route: /about/
    type: Page
    template: about
```

## 6. Rules

- `source` must point to a real HTML file.
- `route` must start with `/`.
- Detail routes must use `{slug}`.
- List and detail pages must be separate.
- `template` must not include `.html`.
- `template` must match the generated page template.
- Dynamic `{slug}` routes must not be used to infer a concrete slug.

## 7. Validation

```text
Every HTML file appears in pages
Every source file exists
Every route is unique
Every template is unique unless reuse is intentional
Every detail route contains {slug}
```
