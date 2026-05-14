# SEO and Analytics

Bukit builds SEO in the engine first. Themes can render the model explicitly, or the engine can inject the generated head tags after rendering.

For the Google Search Central rules behind these choices, see [Google Search Central SEO 学习笔记](google-search-central-learning.md).

## Configuration

```yaml
site:
  url: https://example.com
  baseUrl: /
  seo:
    enabled: true
    renderMode: inject # inject | theme | off
    diagnostics: warn # off | warn | strict
    defaultImage: /assets/og-default.png
    twitterSite: "@example"
    organization:
      name: Example Inc.
      url: https://example.com
      logo: https://example.com/assets/logo.png
    robotsTxt:
      enabled: false
    schema:
      webPage: true
      collectionPage: true
      searchAction: true
  analytics:
    enabled: true
    google_analytics_id: G-XXXXXXXXXX
    disableInPreview: true
```

`site.analytics.google_analytics_id` must be a GA4 ID starting with `G-`. Analytics is emitted when `google_analytics_id` exists and `enabled` is not `false`.

When `disableInPreview` is `true`, `bukit preview` removes Bukit-managed GA4 gtag scripts from served HTML when it can discover the nearest `site.yaml` for the preview directory. The generated files on disk are unchanged.

## Render Modes

- `inject`: default engine-owned mode. Bukit parses the rendered HTML `<head>`, removes managed SEO/GA tags, and injects canonical, description, robots, Open Graph, Twitter, hreflang, JSON-LD, and GA4 gtag.
- `theme`: explicit compatibility mode. The engine exposes `page.seo` and `site.analytics`; the theme renders the SEO partial. Diagnostics report missing or duplicate core tags.
- `off`: disables HTML SEO model output. The engine still computes indexing policy unless `site.seo.enabled: false`.

Use `theme` only when you intentionally want the theme to own head output. Use `off` for unusual deployments that do not want Bukit to build HTML SEO tags, while still preserving index policy for sitemap/search unless SEO is fully disabled.

## Engine Guarantees

Bukit builds a `SeoIndex` per final route, including content pages, derived pages, homepage, taxonomy pages, pagination pages, and collection/list pages. It uses that index as the policy source for sitemap, RSS, search index outputs, HTML SEO models, and diagnostics. A page with `robots: noindex` or `robots: none` is excluded from those outputs even if the theme forgets to render a robots meta tag.

Canonical URLs are generated from `site.url + site.baseUrl + route.url` with normalized slashes. i18n alternate relationships become HTML `hreflang` links when related pages exist.

Diagnostics run at both index and HTML levels. Bukit reports missing `site.url`, double-slash or external canonical URLs, duplicate canonical URLs across final routes, missing `x-default` on hreflang groups, missing core head tags, and duplicate canonical tags. Use `diagnostics: strict` in CI to fail the build instead of logging warnings.

## SEO Audit Report

Every build writes `seo-report.json` next to the generated site output. In multilingual builds, Bukit also writes a root-level merged report that covers all language variants. The report schema and compatibility contract are documented in [SEO Audit Report Schema](seo-report-schema.md).

The report is designed as a CI artifact and stable URL inventory. It includes:

- every final route URL and output path
- schema metadata: `schema` and `schemaVersion`
- title, description, canonical, robots, indexable state, lastmod, content type, and source item id
- sitemap/search/RSS inclusion state
- hreflang alternates
- JSON-LD schema types
- machine-readable `issues` with `severity`, `code`, `route`, and `message`
- summary counts for routes, indexable routes, warnings, and errors

Current audit rules cover title/description missing, length, and duplication; canonical absolute URL, fragment, HTTPS preference, and index consistency; noindex leakage into sitemap; hreflang fully-qualified URL, locale, self-reference, and return-link checks; JSON-LD parse/type checks plus type-specific checks for WebSite/SearchAction, BlogPosting/Article, and ItemList fields; sitemap XML/output-file consistency; robots.txt sitemap/blocking conflicts; missing standard HTML `<head>` for inject mode; and OG/Twitter image URL/file/MIME/dimension checks. External network validation is intentionally opt-in so normal builds stay deterministic.

Run the CI audit command after build:

```bash
bukit seo audit --dir dist
bukit seo audit --dir dist --strict
bukit seo audit --report dist/seo-report.json
bukit seo audit --dir dist --external
```

Default mode returns non-zero when the report has errors. `--strict` also fails on warnings. The audit command validates the report schema URL, schema version, top-level fields, route inventory fields, issue fields, summary counters, and disallows unknown report fields before applying those thresholds, so malformed or unsupported reports fail with exit code `2`.

`--external` performs live HTTP checks for canonical URLs, page links, and HTML/OG/Twitter images found in generated pages. These checks can fail because of network, DNS, rate limits, or unpublished environments, so use them as an explicit CI stage rather than as part of the default static build.

Use `seo diff` to prevent SEO regressions between two archived reports:

```bash
bukit seo diff --baseline previous/seo-report.json --current dist/seo-report.json
bukit seo diff --baseline previous/seo-report.json --current dist/seo-report.json --max-new-errors 0 --max-new-warnings 5
bukit seo diff --baseline previous/seo-report.json --current dist/seo-report.json --fail-on-new-code seo.noindex_in_sitemap,seo.title_missing
bukit seo diff --baseline previous/seo-report.json --current dist/seo-report.json --fail-on-route-removed --fail-on-indexable-drop
```

The diff gate compares issue identity by severity, code, route, and message. It also reports added routes, removed routes, and routes that changed from indexable to non-indexable.

## JSON-LD

Bukit emits JSON-LD through JSON serialization rather than template string concatenation. Current schemas include:

- `WebSite`, optionally with `SearchAction`
- `Organization`, when configured
- `WebPage`
- `CollectionPage` for list pages when enabled
- `ItemList` for list, taxonomy, and pagination pages that expose list item fields
- `BreadcrumbList` for nested paths
- `BlogPosting` for post content

## Theme Partial

Official themes can render the engine model directly:

```html
{{ if page.seo }}
<link rel="canonical" href="{{ page.seo.canonical | html.escape }}" />
{{ if page.seo.description }}
<meta name="description" content="{{ page.seo.description | html.escape }}" />
{{ end }}
{{ for json in page.seo.json_ld }}
<script type="application/ld+json">{{ json }}</script>
{{ end }}
{{ end }}

{{ if site.analytics.enabled && site.analytics.google_analytics_id }}
<script async src="https://www.googletagmanager.com/gtag/js?id={{ site.analytics.google_analytics_id | html.escape }}"></script>
<script>
  window.dataLayer = window.dataLayer || [];
  function gtag(){dataLayer.push(arguments);}
  gtag('js', new Date());
  gtag('config', '{{ site.analytics.google_analytics_id | html.escape }}');
</script>
{{ end }}
```

For new themes, the default `renderMode: inject` already provides the strongest engine guarantee. Shared SEO/Analytics partials remain useful for explicit `theme` mode and for local customization, but they are no longer required for a complete head.

## robots.txt

`site.seo.robotsTxt.enabled: true` generates a basic `robots.txt` with a sitemap URL. Bukit does not overwrite an existing static `robots.txt`.
