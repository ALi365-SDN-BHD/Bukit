# Template Capabilities Manifest

`layouts/bukit.templates.yaml` declares template data dependencies and capability characteristics.

## File Location

Always at the layouts directory root:
```text
layouts/bukit.templates.yaml
```
Or: `themes/<name>/layouts/bukit.templates.yaml`

## Basic Structure

```yaml
templates:
  pages/index.html:
    capabilities:
      needs_page_content: false
  pages/list.html:
    capabilities:
      needs_page_content: true
      supports_pagination: true
      supports_taxonomy: false
      supports_search_snippets: false
```

## Recognized Capabilities

- `needs_page_content`: Whether template depends on `page.content`/`pages[*].content`
- `supports_pagination`: Template suitable as pagination list template
- `supports_taxonomy`: Template suitable as taxonomy/term list template
- `supports_search_snippets`: Template suitable for search summary rendering

## Relationship with `build.listPageContentMode`

- `auto`: Prioritize reading `bukit.templates.yaml`; fall back to compatibility heuristics
- `always`: Always populate list page body
- `never`: Never populate list page body
