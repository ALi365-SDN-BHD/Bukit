# SEO and Analytics

Bukit builds SEO in the engine first. Themes can render the model explicitly, or the engine can inject the generated head tags after rendering.

For the Google Search Central rules behind these choices, see [Google Search Central SEO 学习笔记](google-search-central-learning.md).

## Configuration

```yaml
site:
  url: https://example.com
  baseUrl: /
  search:
    route: /search/
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

`site.seo.schema.searchAction` is an allow switch, while `site.search.route` is
the explicit search capability declaration. Bukit emits WebSite SearchAction
only when SEO and the allow switch are enabled, the route is configured, the
route exists in the final HTML route inventory, and `site.url` is available.
The target is generated as
`<site.url><baseUrl><language-prefix><route>?q={search_term_string}`. Route
matching ignores case and trailing-slash differences.

The route must be an internal path beginning with `/` and cannot contain a
scheme, `//`, backslash, query, fragment, control character, or `.`/`..` path
segment. An enabled declaration with a missing final route or missing
`site.url` fails the build with `ConfigInvalidValue`. If the route is omitted,
or SEO or SearchAction is disabled, Bukit emits no SearchAction and skips the
route-existence check. `search.json`, `bukit-search.html`, a search template, or
theme `capabilities.search` does not enable this contract by itself.

## Render Modes

- `inject`: default engine-owned mode. Bukit parses the rendered HTML `<head>`, removes managed SEO/GA tags, and injects canonical, description, robots, Open Graph, Twitter, hreflang, JSON-LD, and GA4 gtag.
- `theme`: explicit compatibility mode. The engine exposes `page.seo` and `site.analytics`; the theme renders the SEO partial. Diagnostics report missing or duplicate core tags.
- `off`: disables engine-managed HTML SEO tag output. The engine still computes indexing policy unless `site.seo.enabled: false`.

Use `theme` only when you intentionally want the theme to own head output. Use `off` for unusual deployments that do not want Bukit to build HTML SEO tags, while still preserving index policy for sitemap/search unless SEO is fully disabled.

## Starter Theme Defaults

The starter theme is inject-first. Its base layout only provides a standard HTML `<head>` with charset, viewport, title, RSS/sitemap links, and stylesheet links. It does not include SEO or Analytics partials by default. Bukit injects the managed SEO head and GA4 tags when `site.seo.renderMode: inject`.

Starter still ships `partials/seo.html` and `partials/analytics.html` as reference partials for users who intentionally switch to `renderMode: theme`. Those partials must use `html.escape` for HTML attributes.

## Description Fallbacks

Content pages use this SEO description priority:

1. `seo_desc`
2. `summary`
3. `site.description`

Home, collection list, taxonomy, and pagination pages use `page.summary` when available and otherwise fall back to `site.description`. If `site.description` is missing, these generated routes will produce `seo.description_missing` warnings in `seo-report.json`.

Taxonomy titles and summaries are derived in the active `site.language`.
Languages beginning with `zh` use Chinese punctuation, count/range wording, and
`第 N 页`; other languages currently fall back to English and use `- Page N`.
For example, a Chinese category term can produce `商务资讯：市场观察` and
`浏览“市场观察”下的内容，共 3 项。`. A term `description` is preserved on page
1 and receives the localized page/range suffix on later pages.

Taxonomy metadata resolves in this order: route metadata SEO fields, route
metadata visible fields, term metadata, `taxonomy.kinds[]` metadata, then the
Core localized default. Meta description, Open Graph, Twitter, and
WebPage/CollectionPage JSON-LD all consume the same effective SEO description;
an explicit SEO-only route metadata field may intentionally differ from the
visible summary.

## Engine Guarantees

Bukit builds a `SeoIndex` per final route, including content pages, derived pages, homepage, taxonomy pages, pagination pages, and collection/list pages. It uses that index as the policy source for sitemap, RSS, search index outputs, HTML SEO models, and diagnostics. A page with `robots: noindex` or `robots: none` is excluded from those outputs even if the theme forgets to render a robots meta tag.

Canonical URLs are generated from `site.url + site.baseUrl + route.url` with normalized slashes. i18n alternate relationships become HTML `hreflang` links when related pages exist.

Diagnostics run at both index and HTML levels. Bukit reports missing `site.url`, double-slash or external canonical URLs, duplicate canonical URLs across final routes, missing `x-default` on hreflang groups, missing core head tags, and duplicate canonical tags. Use `diagnostics: strict` in CI to fail the build instead of logging warnings.

The schema audit requires a WebSite SearchAction only when the effective
configuration above enables it. A SearchAction that is present is always
validated for type, absolute target, and `query-input`, even when it was not
expected from configuration.

BreadcrumbList is resolved from the final HTML route inventory rather than by
splitting URL segments. Content, derived taxonomy, list/pagination/filtered
list, and managed static HTML routes can be real parents. Matching ignores case
and trailing-slash differences; `/` is omitted; the current page is always the
last item. If no real parent exists, Bukit emits a one-item BreadcrumbList. The
home route emits none. A disabled taxonomy index and a pagination marker such
as `/page/` therefore cannot appear unless that exact HTML route exists.

## SEO Audit Report

Every build writes the SEO report to `.bukit/seo-report.json` under the generated site output. `bukit seo audit` uses that file as its default discovery target and validates only the SEO schema (`https://bukit.dev/schemas/seo-report.v1.json`) by default. Non-SEO schema inputs are not discovered automatically; pass `--report` explicitly when you need compatibility loading (for example `.bukit/publish-audit-report.json`).

The report is designed as a CI artifact and stable URL inventory. It includes:

- every final route URL and output path
- schema metadata: `schema` and `schemaVersion`
- title, description, canonical, robots, indexable state, lastmod, content type, and source item id
- sitemap/search/RSS inclusion state
- hreflang alternates
- JSON-LD schema types
- machine-readable `issues` with `severity`, `code`, `route`, and `message`
- summary counts for routes, indexable routes, warnings, and errors

Current audit rules cover title/description missing, length, and duplication; canonical absolute URL, fragment, HTTPS preference, and index consistency; noindex leakage into sitemap; hreflang fully-qualified URL, locale, self-reference, and return-link checks; JSON-LD parse/type checks plus type-specific checks for WebSite/SearchAction, BlogPosting/Article, ItemList, and BreadcrumbList fields; sitemap XML/output-file consistency; robots.txt sitemap/blocking conflicts; missing standard HTML `<head>` for inject mode; and OG/Twitter image URL/file/MIME/dimension checks. BreadcrumbList validation requires a non-empty item array, ListItem type, consecutive positions, non-empty names, and valid item URLs. A relative internal item remains compatible when `site.url` is missing but produces a warning. External network validation is intentionally opt-in so normal builds stay deterministic.

Run the CI audit command after build:

```bash
bukit seo audit --dir dist
bukit seo audit --dir dist --strict
bukit seo audit --report dist/.bukit/publish-audit-report.json --strict
bukit seo audit --dir dist --external
```

Default mode returns non-zero when the report has errors. `--strict` also fails on warnings. The default audit command validates the SEO report schema URL, schema version, top-level fields, route inventory fields, issue fields, summary counters, and disallows unknown report fields before applying those thresholds, so malformed or unsupported reports fail with exit code `2`.

`--external` performs live HTTP checks for canonical URLs, page links, and HTML/OG/Twitter images found in generated pages. These checks can fail because of network, DNS, rate limits, or unpublished environments, so use them as an explicit CI stage rather than as part of the default static build.

Use `seo diff` to prevent SEO regressions between two reports:

```bash
bukit seo diff --baseline previous/.bukit/seo-report.json --current dist/.bukit/seo-report.json
bukit seo diff --baseline previous/.bukit/seo-report.json --current dist/.bukit/seo-report.json --max-new-errors 0 --max-new-warnings 5
bukit seo diff --baseline previous/.bukit/seo-report.json --current dist/.bukit/seo-report.json --fail-on-new-code seo.noindex_in_sitemap,seo.title_missing
bukit seo diff --baseline previous/.bukit/seo-report.json --current dist/.bukit/seo-report.json --fail-on-route-removed --fail-on-indexable-drop
```

The diff gate compares issue identity by severity, code, route, and message. It also reports added routes, removed routes, and routes that changed from indexable to non-indexable.
By default, `seo diff` requires both inputs to use the same SEO report schema; use `--allow-cross-schema` only when comparing SEO and publish audit artifacts explicitly.

## JSON-LD

Bukit emits JSON-LD through JSON serialization rather than template string concatenation. Current schemas include:

- `WebSite`, optionally with `SearchAction`
- `Organization`, when configured
- `WebPage`
- `CollectionPage` for list pages when enabled
- `ItemList` for list, taxonomy, and pagination pages that expose list item fields
- `BreadcrumbList` for every non-home route, using only real strict ancestors
- `BlogPosting` for post content

Migration note: existing sites that relied only on the historical default
`searchAction: true` now emit no SearchAction. Add `site.search.route` only after
the site has a complete, final HTML search page at that route. Bukit does not
promote `bukit-search.html` into a formal page or add it to navigation.

Taxonomy migration note: derived metadata no longer hard-codes English. Sites
that require the previous exact wording should set term metadata,
`taxonomy.kinds[]` title/description fields, or `content.routeMetadata`.
Breadcrumb consumers should also stop relying on lexical placeholder parents;
only routes that produce final HTML are emitted.

Canonical ownership is authoritative for article authors. When no canonical
author is present, the existing `geo.author` value remains a compatibility
fallback and is treated as a `Person`. `authorType` accepts `Person` or
`Organization` (case-insensitive) and defaults to `Person` when a canonical
author is present but no type is declared. Bukit emits the normalized canonical
type only inside the article's `author` property:

```json
{
  "@type": "BlogPosting",
  "author": {
    "@type": "Organization",
    "name": "Silk Road Editorial Desk"
  }
}
```

`authorType` does not derive from the author's name, canonical `organization`,
or `site.seo.organization`. Those organization fields describe content/site
ownership and publisher identity, not the article byline. A matching
`geo.author` may enrich the canonical author with `url` and `sameAs`; it does
not override the canonical name or type. An invalid explicit author type is
reported by canonical validation and is omitted from article JSON-LD instead
of being guessed as `Person`.

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
