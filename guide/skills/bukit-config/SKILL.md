---
name: bukit-config
description: Use when working with Bukit site.yaml fields, defaults, validation errors, or config examples.
---

# Bukit Config

## Content Classification Contract

`site.yaml` maps to `AppConfig`; unknown fields fail validation. Required
top-level sections are `site` and `content`, and `content.sources[]` is
required.

| Setting | Contract |
|---|---|
| `content.sources[].type` | Provider identifier (`markdown` or `notion`), not document metadata type. |
| `mode: content` | Requires a non-empty collection after source/item projection; missing type defaults to `page`. |
| `mode: data` | Does not require collection; missing type defaults to `module`; not routed as a collection page. |
| `content.sources[].collection` | Overrides item collection without changing item type. |
| `markdown.defaultType` | Sets only a missing document type. It never supplies collection. |
| `addToCollections` | Creates explicit cloned documents/routes for target collections. |

Type and collection never derive from each other. Therefore this does not
build: `mode: content` plus `markdown.defaultType: article` with no source or
item collection. Add `collection: news` to the source or item.

```yaml
content:
  sources:
    - type: markdown
      mode: content
      collection: news
      markdown:
        dir: content/news
        defaultType: article
```

Collection can also come from Markdown front matter or Notion
`content.sources[].notion.propertyMap.Collection`.

## Common Mistakes

- Treating source `type: markdown` as document type.
- Assuming `defaultType` or a type permalink supplies collection ownership.

Key validation files: `ConfigStrictFieldValidator`, `ConfigValidator`,
`ProviderValidators`, `CollectionsValidator`, and `ConfigJsonSchemaGenerator`.

Use `bukit config check` for validation and `bukit config schema` to emit the
current schema.

## Reliability-Sensitive Fields

| Field | Contract |
|---|---|
| `site.search.maxContentLength` | Positive UTF-16 code-unit cap for search `content` across document, list, plugin, publish-projection, and multilingual output. It does not cap title, summary, or generated snippet. |
| `content.media.maxConcurrency` | Positive active-download limit within one rewrite operation or localized body store. It is separate from `--jobs` and not process-global. |
| `build.followSymlinks` | Enables following only in supported copy paths. Default recursive content/static/media/report scanners still skip directory links and reparse points. |
| `site.search.placeholderText` | Plain text; the default UI encodes it and does not accept markup. |
