# 11 I18n And SEO

Bukit handles language variants and SEO metadata inside the build pipeline.

## i18n Config

```yaml
site:
  language: en
  languages: [en, zh-CN, ms]
  defaultLanguage: en
  sitemapMode: index
  feed:
    mode: split
  search:
    mode: index
    route: /search/
build:
  languageJobs: 2
```

Each language builds into `dist/<language>/`. Content without a language value
belongs to `defaultLanguage`.

## SEO Config

```yaml
site:
  url: https://example.com
  seo:
    enabled: true
    renderMode: inject
    diagnostics: warn
    homeTitleTemplate: "{siteTitle}"
    pageTitleTemplate: "{pageTitle}{separator}{siteTitle}"
    titleSeparator: " | "
    defaultImage: /assets/og.png
    twitterSite: "@example"
    organization:
      type: NewsMediaOrganization
      name: Example News
      url: https://example.com/
      logo: https://example.com/assets/logo.png
      sameAs:
        - https://www.linkedin.com/company/example/
    schema:
      webPage: true
      collectionPage: true
      searchAction: true
```

`site.seo.schema.searchAction` permits output; `site.search.route` explicitly
declares that a complete search page exists. When both switches and SEO are
enabled, the route must be present in each variant's final HTML route inventory
and `site.url` must be set, or the build fails with `ConfigInvalidValue`. The
target is `<site.url><baseUrl><language-prefix><route>?q={search_term_string}`.
When `route` is omitted, Bukit emits no SearchAction. Search index or UI fragment
generation alone does not enable it.

`site.seo.organization.type` accepts only `Organization` and
`NewsMediaOrganization`. Organization `url` and `logo` may be absolute HTTP(S)
URLs or root-relative URLs resolved against `site.url`; only absolute HTTP(S)
results are emitted. `organization.sameAs` contains explicit identity URLs,
omits an empty array, and is never guessed. The configured organization becomes
the matching publisher for article JSON-LD.

For collection list routes, `site.collections.<name>.noindexWhenEmpty: true`
marks an empty result as `noindex,follow`. The same indexability decision
removes that empty route from sitemap, search output, `llms.txt`, and
`llms-full.txt`.

`SeoPipeline` builds page-level metadata, alternate links, Open Graph, Twitter,
article metadata, JSON-LD, and report issues. The `Title` property on `SeoModel`
remains the SEO and social title. Its `DocumentTitle` property, exposed as
`page.seo.document_title`, is resolved independently for the final HTML
`<title>`. Page-level JSON-LD names, headlines, and the final breadcrumb item
instead use the visible route/content title, so an `seo_title` override does
not replace the content heading in structured data.

The `/` route uses `homeTitleTemplate`; content, list, taxonomy, and pagination
routes use `pageTitleTemplate`. `{pageTitle}` is the already resolved semantic
SEO title, so it includes `seo_title` overrides and pagination suffixes. Supported
placeholders are case-insensitive `{pageTitle}`, `{siteTitle}`, and
`{separator}` only. A page template must contain `{pageTitle}`; a home template
must contain `{pageTitle}` or `{siteTitle}`.

## Taxonomy Metadata Language

Derived taxonomy titles and summaries use the language of the current build
variant. Languages beginning with `zh` use Chinese punctuation and pagination
wording; unsupported languages fall back to English. The built-in Chinese kind
names are `标签` and `分类`. Custom kinds keep their key when no title is
configured.

Term descriptions remain unchanged on page 1. On page 2 and later, Bukit adds
the current-language page number and visible item range. The effective metadata
priority is route metadata SEO fields, route metadata visible fields, term
metadata, taxonomy kind config, then Core localized defaults. The same effective
SEO description feeds meta, Open Graph, Twitter, and CollectionPage JSON-LD.
Use an SEO-specific route metadata field only when that value should differ from
the visible summary.

## Breadcrumb Route Contract

Breadcrumb JSON-LD uses only strict URL ancestors present in the current
language variant's final HTML route inventory. Content, taxonomy, list and
pagination, filtered list, and managed static HTML routes participate. Matching
ignores case and trailing slashes, but does not invent a route from a URL
segment. Thus `/companies/page/2/` can include `/companies/` but not
`/companies/page/`, and a disabled taxonomy index is omitted.

The current route is always last. A non-home page with no real parent keeps a
single current-page item; the home route has no BreadcrumbList. `site.url` and
the active base/language prefix are applied to each target. Without `site.url`,
relative targets retain the existing compatibility behavior and the schema
audit reports a warning.

In `inject` mode, Core removes existing head titles and writes exactly one
HTML-encoded document title. `theme` and `off` still expose the SEO model and
run diagnostics, but leave title rendering to the theme. Per-page
`seo_inject: false` also skips mutation. In all cases the final report inspects
the actual HTML output. HTML without a standard complete head is preserved
rather than repaired implicitly.

## Audits

Run `seo audit` after `build`:

```bash
bukit build --clean
bukit seo audit --dir dist --strict
```

Use `seo diff` to compare reports in CI when you want to limit new warnings or
route removals.

The final report keeps the `seo-report.v1` route schema unchanged and adds title
quality through issue codes:

| Issue | Severity | Meaning |
|---|---|---|
| `seo.document_title_missing` | error | No head document title exists. |
| `seo.document_title_empty` | error | At least one head document title is empty. |
| `seo.document_title_multiple` | error | More than one head document title exists. |
| `seo.document_title_too_long` | warning | The normalized title exceeds 60 characters. |
| `seo.document_title_mismatch` | inject: error; theme/off: warning | Actual HTML differs from the resolved model title. |
| `seo.document_title_duplicate` | warning | Unrelated routes share the same actual HTML title; mutual hreflang alternates are excluded. |
| `seo.html_head_missing` | warning | The output has no standard complete head. |

BreadcrumbList audit codes use the `seo.schema_breadcrumb_*` prefix and check a
non-empty item array, ListItem type, consecutive positions, non-empty names, and
valid item URLs.

If an HTML output file exists without a head, both
`seo.html_head_missing` and `seo.document_title_missing` are emitted. If the
route output file itself is absent, only `seo.output_file_missing` is emitted to
avoid cascading noise.
