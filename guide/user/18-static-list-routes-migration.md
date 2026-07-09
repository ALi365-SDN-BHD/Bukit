# 18 Static List Routes Migration

Bukit Core renders collection lists, filtered lists, taxonomy lists, and
pagination as static routes. This replaces client-only list construction when a
site needs crawlable pages.

## Collection List

```yaml
site:
  collections:
    post:
      permalink: /blog/{slug}/
      template: pages/post.html
      listRoute: /blog/
      listTemplate: pages/list.html
      pagination:
        enabled: true
        pageSize: 12
```

## Filtered List

Use filtered lists for a small number of named pages.

```yaml
filteredLists:
  - field: topic
    operator: equals
    value: release
    listRoute: /blog/release/
    title: Release Notes
    listTemplate: pages/list.html
```

## Taxonomy

Use taxonomy for open-ended categories or tags where terms are content-driven.

```yaml
taxonomy:
  kinds:
    - key: tags
      routePrefix: /tags/
      indexTemplate: pages/taxonomy-index.html
      termTemplate: pages/taxonomy-term.html
```

## Template Fields

List templates receive `pages`, `items`, `pagination`, and one of
`collection`, `filter`, or `taxonomy`. Use those fields directly instead of
shipping a browser-only list renderer.
