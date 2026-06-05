# Bukit `site.yaml` Configuration Specification

## 1. Purpose

This specification defines the configuration contract that AI must follow when generating Bukit `site.yaml` files.

AI must not invent fields, hierarchy, or configuration combinations.

## 2. Profile Selection

Before generating `site.yaml`, AI must choose one standard Profile:

```text
Profile A: Markdown local preview
Profile B: Notion single-database mode
Profile C: Notion multi-database mode
Profile D: JSON/YAML seed + Markdown build mode
```

## 3. Allowed Top-level Fields

```yaml
site:
content:
collections:
build:
theme:
```

Do not generate unknown fields such as:

```yaml
base_url: https://example.com
themePath: themes/demo
notionDatabase:
  id: xxx
```

## 4. `site`

```yaml
site:
  title: Silkroad Business
  baseUrl: https://example.com
  language: en
```

| Field | Type | Required | Description |
|---|---|---:|---|
| `site.title` | string | yes | Site title |
| `site.baseUrl` | string | no | Production URL |
| `site.language` | string | no | Default language |

## 5. Markdown Provider

```yaml
content:
  provider: markdown
  markdown:
    dir: content
    defaultType: page
```

Rules:

- `content.markdown.dir` must be relative to the site directory.
- Use `content` by default.
- Do not generate absolute paths.
- Do not use `sites/<site-name>/content` as the value.

## 6. Notion Single-database Provider

```yaml
content:
  provider: notion
  notion:
    databaseId: ${NOTION_DATABASE_ID}
    tokenEnv: NOTION_TOKEN
    filterProperty: Published
    filterType: checkbox_true
    sortProperty: Title
    sortDirection: ascending
```

Rules:

- `databaseId` is required.
- `tokenEnv` is required.
- Never write a real token into the configuration.
- Use environment variable placeholders.

## 7. Notion Multi-database Sources

```yaml
content:
  sources:
    - type: notion
      name: pages
      mode: content
      collection: page
      notion:
        databaseId: ${NOTION_PAGES_DATABASE_ID}
        tokenEnv: NOTION_TOKEN
        filterProperty: Published
        filterType: checkbox_true
        sortProperty: Title
        sortDirection: ascending
```

Rules:

- `content.sources` must be an array.
- `sources[].name` must be unique.
- `sources[].collection` is required.
- `sources[].notion.databaseId` is required.
- `sources[].notion.tokenEnv` is required.
- Notion multi-database mode must use `content.sources`.

## 8. Forbidden Combination

Do not generate:

```yaml
content:
  provider: notion
  sources: []
```

`content.provider` and `content.sources` are mutually exclusive.

## 9. Compatibility Matrix

| Scenario | content-source | build-source | site.yaml |
|---|---|---|---|
| Local preview | notion | markdown | `content.provider: markdown` |
| Local preview | json | markdown | `content.provider: markdown` |
| Local preview | yaml | markdown | `content.provider: markdown` |
| Notion single DB | notion | notion | `content.provider: notion` |
| Notion multi DB | notion | notion | `content.sources` |
| Invalid | json | notion | not allowed |
| Invalid | yaml | notion | not allowed |

## 10. `collections`

```yaml
collections:
  post:
    listRoute: /insights/
    listTemplate: pages/insights.html
    detailTemplate: pages/article.html
    permalink: /insights/{slug}/
```

Rules:

- `permalink` must contain `{slug}`.
- Template paths must point to real template files.
- Collection names must match the content data.

## 11. `build`

```yaml
build:
  output: dist
  clean: true
```

## 12. `theme`

```yaml
theme:
  name: silkroadbiz
```

Do not use `theme.path` instead of `theme.name`.

## 13. Validation Workflow

After AI generates configuration, run:

```bash
bukit doctor --config sites/<site-name>/site.yaml
bukit build --config sites/<site-name>/site.yaml
```

If supported:

```bash
bukit config validate --config sites/<site-name>/site.yaml
bukit doctor --config sites/<site-name>/site.yaml --strict
```

## 14. Expected Future Diagnostics

Bukit doctor should ideally report precise errors:

```text
Unknown field: content.notion.database
Missing required field: content.sources[0].collection
Invalid combination: content.provider and content.sources cannot coexist
Invalid build source: notion requires content source notion
```
