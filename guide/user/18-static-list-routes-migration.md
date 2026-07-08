# Static List Routes Migration

This guide shows how to move a site from JavaScript pagination or browser-side
filtering to Bukit build-time static routes. The goal is that list, category,
country, industry, SEO, sitemap, and feed output all come from the same generated
route graph.

Use this when an existing site loads a full item dataset into a template and then
uses JavaScript to paginate, filter, hide, or show cards in the browser.

## Migration Checklist

1. Move each content group into an explicit `site.collections` entry.
2. Add `listRoute`, `listTemplate`, and `pagination` to large collections.
3. Replace browser-side category pages with `taxonomy.kinds`.
4. Replace fixed country, industry, or curated filters with `filteredLists`.
5. Update list templates to render `items` and `pagination` instead of all pages.
6. Build and audit SEO output so canonical, prev/next, sitemap, and feeds match
   the generated routes.

## Before: Browser-side Pagination

This pattern renders too much content into one HTML page and relies on JavaScript
to decide which cards are visible:

```html
<main id="company-list">
  {{ for company in site.modules.companies }}
    <article data-country="{{ company.country }}" data-industry="{{ company.industry }}">
      <h2>{{ company.title }}</h2>
    </article>
  {{ end }}
</main>

<script src="/assets/list-filter.js"></script>
```

That works for small demos, but it makes large pages slower and hides important
list states from crawlers. After migration, the route `/companies/page/2/` and a
filter route such as `/companies/malaysia/` are real HTML files produced during
`bukit build`.

## Collection Pagination

Use collection pagination for the main list route of a collection.

```yaml
site:
  collections:
    insight:
      permalink: /insights/{slug}/
      template: pages/insight.html
      listRoute: /insights/
      listTemplate: pages/list.html
      pagination:
        enabled: true
        pageSize: 12
        urlPattern: page/{page}/
        firstPageUsesListRoute: true
      output:
        rss: true
        sitemap: true

content:
  sources:
    - type: markdown
      name: insights
      collection: insight
      markdown:
        dir: content/insights
```

With this config:

- `/insights/` renders the first 12 items.
- `/insights/page/2/` renders the next 12 items.
- Detail pages still use `/insights/{slug}/`.
- `sitemap.xml`, feed output, and SEO metadata use the same generated list routes.

`urlPattern` is relative to `listRoute`. It must include `:num`, `{num}`, or
`{page}` and must not start with `/`. Use `firstPageUsesListRoute: false` only
when page 1 should also use the pattern, for example `/insights/p/1/`.

## Taxonomy Category Pages

Use taxonomy when Bukit should generate one route per term from content metadata,
such as every category, tag, topic, region, or service type.

```yaml
taxonomy:
  outputMode: both
  pageSize: 12
  indexEnabled: true
  kinds:
    - key: categories
      kind: category
      title: Categories
      termTemplate: pages/list.html
      routePrefix: /insights/category
```

Content can then declare terms:

```yaml
---
title: Market Outlook
collection: insight
categories:
  - market
  - policy
---
```

With the `routePrefix` above, Bukit can generate:

- `/insights/category/market/`
- `/insights/category/market/page/2/`
- `/insights/category/policy/`

Taxonomy pages receive the same `items` and `pagination` template model as
collection lists. They also receive `taxonomy`, so a shared list template can
render the current term title or slug when needed.

## Fixed Filtered Lists

Use `filteredLists` for fixed entry pages that are product or editorial choices:
country pages, industry pages, featured routes, or a small set of campaign
landing pages. Do not use `filteredLists` to expand every tag or category; use
taxonomy for that.

```yaml
site:
  collections:
    company:
      permalink: /companies/{slug}/
      template: pages/company.html
      listRoute: /companies/
      listTemplate: pages/company-list.html
      pagination:
        enabled: true
        pageSize: 9
      filteredLists:
        - field: country
          operator: equals
          value: Malaysia
          listRoute: /companies/malaysia/
          listTemplate: pages/company-list.html
          pageSize: 9
          urlPattern: page/{page}/
          emptyBehavior: render
        - field: industry
          operator: in
          values:
            - logistics
            - manufacturing
          listRoute: /companies/industrial/
          listTemplate: pages/company-list.html
          pageSize: 9
```

With this config:

- `/companies/` is the main paginated company list.
- `/companies/malaysia/` contains only matching Malaysia companies.
- `/companies/malaysia/page/2/` is generated when matches exceed `pageSize`.
- `/companies/industrial/` combines companies whose industry matches either
  configured value.

Supported operators are `equals`, `contains`, and `in`. Use `value` with
`equals` and `contains`; use `values` with `in`. `emptyBehavior: skip` prevents
an empty fixed route from being written.

## Template Migration

After enabling static list routes, update list templates to read only the current
page slice. Use `items` for cards and `pagination` for navigation.

```html
{% layout "layouts/base.html" %}
<main>
  <h1>{{ page.title }}</h1>

  <section class="card-grid">
    {{ for item in items }}
      <article>
        <h2><a href="{{ item.url }}">{{ item.title }}</a></h2>
        {{ if item.summary }}<p>{{ item.summary }}</p>{{ end }}
      </article>
    {{ end }}
  </section>

  {{ if pagination && pagination.total_pages > 1 }}
    <nav aria-label="Pagination">
      {{ if pagination.has_prev }}<a href="{{ pagination.prev_url }}">Previous</a>{{ end }}
      <span>{{ pagination.page }} / {{ pagination.total_pages }}</span>
      {{ if pagination.has_next }}<a href="{{ pagination.next_url }}">Next</a>{{ end }}
    </nav>
  {{ end }}
</main>
```

The same template can render collection lists, taxonomy term pages, and filtered
lists. Use optional contexts when the page needs a label:

```html
{{ if taxonomy }}
  <p>Category: {{ taxonomy.term }}</p>
{{ end }}

{{ if filter }}
  <p>Filtered by {{ filter.field }}: {{ filter.value }}</p>
{{ end }}
```

Avoid rendering the entire collection and then hiding items in JavaScript. Keep
JavaScript for UI enhancements only; the final list state should already be in
the static HTML.

## SEO, Sitemap, and Feeds

Static list routes are first-class routes. Bukit uses the generated route graph
for SEO, sitemap, and feed projection:

- Canonical URLs point to the actual collection, taxonomy, or filtered list page.
- Paginated pages get consistent `prev` and `next` links.
- Sitemap output includes collection pages, taxonomy term pages, and filtered
  list pages unless a collection output rule excludes them.
- Collection feeds can be enabled through `site.collections.<name>.output.rss`.
- Taxonomy feeds use the taxonomy route prefix when a kind defines one.

Set `site.url` and `site.baseUrl` before auditing public output:

```yaml
site:
  url: https://example.com
  baseUrl: /
  seo:
    enabled: true
    renderMode: inject
```

Then verify the generated files:

```bash
bukit config check
bukit build
bukit seo audit --dir dist
bukit publish audit --dir dist
```

## Common Migration Problems

| Symptom | Fix |
|---|---|
| Page 2 is missing | Ensure the collection has `pagination.enabled: true` and enough items to exceed `pageSize`. |
| Filtered list route is skipped | Add a parent collection `listRoute`; filtered lists require it. |
| `urlPattern` fails validation | Use a relative pattern such as `page/{page}/`; do not include a leading slash, query string, fragment, or path traversal. |
| Category routes are not generated | Use `taxonomy.kinds[]` with the content field name in `key`, and ensure content actually has that field. |
| The template still shows every item | Replace loops over global data or all pages with `items`. |
| SEO output points to old paths | Rebuild after changing routes, then rerun `bukit seo audit` and `bukit publish audit`. |

For field-level configuration details, see [04 Site YAML Config](./04-site-yaml-config.md).
For the list template model, see [08 Themes and Templates](./08-themes-templates.md).
For generated output behavior, see [10 Built-in Outputs](./10-built-in-outputs.md).
