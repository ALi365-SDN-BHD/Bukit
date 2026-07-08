# Recipes

## Blog Collection

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
        pageSize: 10
content:
  sources:
    - type: markdown
      name: posts
      collection: post
      markdown:
        dir: content/posts
```

## Data-only FAQ

```yaml
content:
  sources:
    - type: markdown
      name: faq
      mode: data
      markdown:
        dir: data/faq
```

## GitHub Pages Subpath

```yaml
site:
  url: https://example.github.io
  baseUrl: /project-name/
deploy:
  provider: github-pages
  branch: gh-pages
```

## Strict SEO Gate

```bash
bukit build
bukit seo audit --dir dist --strict
bukit publish audit --dir dist --strict
```
