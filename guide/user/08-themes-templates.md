# 08 Themes And Templates

Core themes are local filesystem themes resolved by `ThemePathResolver` and
bootstrapped by `ThemeBootstrapper`.

## Theme Layout

```text
themes/site/
  theme.yaml
  layouts/
    layouts/base.html
    pages/page.html
    pages/list.html
    pages/post.html
    pages/search.html
  assets/
    style.css
  static/
    robots-extra.txt
```

## Config

```yaml
theme:
  name: site
  layouts: layouts
  assets: assets
  static: static
  staticTemplate: pages/page.html
  params:
    brand: Bukit
```

Default `layouts`, `assets`, and `static` paths resolve under
`themes/<theme.name>/`. Custom path values are resolved from the site root.

## Template Objects

| Object | Available In | Source |
|---|---|---|
| `site` | all templates | `SiteModel` |
| `page` | page and list templates | `PageInfo` |
| `pages` | list templates | list items |
| `items` | list templates | alias for list items |
| `pagination` | paginated lists | `ListPaginationModel` |
| `collection`, `taxonomy`, `filter` | list routes | list route metadata |

## Layouts

Scriban layout directives are parsed before rendering. A page template can start
with:

```scriban
{% layout "layouts/base.html" %}
```

The base layout renders child content with `{{ content }}`.
