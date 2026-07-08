# Notion Content

Notion is a Core content source. It is configured in `site.yaml`; there is no separate Core Notion command.

## Minimal Source

```yaml
content:
  sources:
    - type: notion
      name: cms
      collection: post
      notion:
        databaseId: "${NOTION_DATABASE_ID}"
        pageSize: 50
        filterProperty: Published
        filterType: checkbox_true
```

`NOTION_TOKEN` must be available in the environment for validation and builds.

## Property Mapping

```yaml
content:
  sources:
    - type: notion
      name: cms
      collection: post
      notion:
        databaseId: "${NOTION_DATABASE_ID}"
        propertyMap:
          Title: Title
          Slug: Slug
          Type: Type
          PublishAt: PublishAt
          Language: Language
          I18nKey: I18nKey
          Summary: Summary
          Collection: Collection
          SeoTitle: SeoTitle
          SeoDescription: SeoDescription
          SeoImage: SeoImage
          Canonical: Canonical
```

## Filters and Cache

Allowed filter types include `checkbox_true`, `checkbox_false`, `select_equals`, `status_equals`, `rich_text_equals`, and `none`.

Allowed cache modes are `off`, `readwrite`, and `readonly`.

```yaml
notion:
  databaseId: "${NOTION_DATABASE_ID}"
  filterType: status_equals
  filterValue: Published
  cacheMode: readwrite
  cacheDir: .cache/notion
```

## Verification

```bash
bukit config check
bukit doctor
bukit build
```
