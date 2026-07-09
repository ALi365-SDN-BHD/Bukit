# 15 Recipes

## Blog Collection

```yaml
site:
  collections:
    post:
      permalink: /blog/{year}/{month}/{slug}/
      template: pages/post.html
      listRoute: /blog/
      listTitle: Blog
      listTemplate: pages/list.html
      pagination:
        enabled: true
        pageSize: 10
```

## Filtered List

```yaml
site:
  collections:
    post:
      permalink: /blog/{slug}/
      template: pages/post.html
      filteredLists:
        - field: category
          operator: equals
          value: engineering
          listRoute: /blog/engineering/
          title: Engineering
          listTemplate: pages/list.html
```

## Taxonomy

```yaml
taxonomy:
  outputMode: both
  pageSize: 20
  kinds:
    - key: tags
      title: Tags
      routePrefix: /tags/
      indexTemplate: pages/taxonomy-index.html
      termTemplate: pages/taxonomy-term.html
```

## Build Reports For CI

```bash
bukit build --clean --ci --metrics .bukit/metrics.json
bukit seo audit --dir dist --strict
bukit publish audit --dir dist --strict
```
