# Multi-source Data

`content.sources` can combine Markdown and Notion sources. Each source can generate routes or provide data to templates.

## Content Mode

`mode: content` creates pages through collection routing.

```yaml
content:
  sources:
    - type: markdown
      name: posts
      mode: content
      collection: post
      markdown:
        dir: content/posts
```

## Data Mode

`mode: data` makes the source available to templates without generating page routes.

```yaml
content:
  sources:
    - type: markdown
      name: faq
      mode: data
      markdown:
        dir: data/faq
```

Templates can read generated data through `data` or site data exposed by the renderer.

## Source Rules

Source names are optional, but names must be unique when present. Use names for data sources so template usage stays clear.

Do not mix legacy single-provider config with `content.sources`.

## Verification

```bash
bukit doctor
bukit build
```
