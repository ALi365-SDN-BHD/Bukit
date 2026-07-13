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
    schema:
      webPage: true
      collectionPage: true
      searchAction: true
```

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

If an HTML output file exists without a head, both
`seo.html_head_missing` and `seo.document_title_missing` are emitted. If the
route output file itself is absent, only `seo.output_file_missing` is emitted to
avoid cascading noise.
