# 11 I18n And SEO

Bukit handles language variants and SEO metadata inside the build pipeline.

## i18n Config

```yaml
site:
  language: en
  languages: [en, zh-CN, ms]
  defaultLanguage: en
  defaultLanguageInSubdir: true   # false puts the default language at the root
  defaultLanguageRedirect: true   # noindex home redirect page
  defaultLanguageLegacyRedirects: false  # true: redirect every old /<default>/... page
  sitemapMode: index
  feed:
    mode: split
  search:
    mode: index
    route: /search/
build:
  languageJobs: 2
```

Content without a language value belongs to `defaultLanguage`.

With `defaultLanguageInSubdir: true` (the default), every language builds into
`dist/<language>/`. With `false`, the default language builds into `dist/` with the
site root base URL, while other languages keep `dist/<language>/`. The default
`defaultLanguageRedirect: true` writes a noindex redirect page so the home URL works
on static hosting: `dist/index.html` → `/<defaultLanguage>/` in the prefixed layout,
or `dist/<defaultLanguage>/index.html` → `/` in the root layout. Set
`defaultLanguageRedirect: false` when the host or CDN already redirects the home
URL and would otherwise be overridden. With the root layout, `search.mode: merged`
is rejected; default-language reports move to `.bukit/variants/<language>/`.

### Migrating an existing site to the root layout

Switching a live site to `defaultLanguageInSubdir: false` moves every default-language
page from `/<defaultLanguage>/<path>/` to `/<path>/`. Only the old home page gets a
redirect by default. If your host can issue 301s (Netlify, Cloudflare Pages, S3/CDN
rules), add a `/<defaultLanguage>/*` → `/*` rule there: a 301 is a stronger migration
signal than a page redirect. Do not enable the option below in that case, because the
generated files can shadow host rules that are not forced.

On static hosts that cannot redirect, such as GitHub Pages, set
`defaultLanguageLegacyRedirects: true`. Bukit then writes a noindex meta-refresh page at
the old prefixed path of every default-language HTML page: content pages, collection
lists and pagination, taxonomy pages, static HTML routes, and aliases (an alias
`/old/` also gets `/<defaultLanguage>/old/`, pointing straight at the page). The home
page stays controlled by `defaultLanguageRedirect`. A redirect is skipped when your
own content, alias, list, or static file already uses that path, and the build logs
one summary line, `i18n.legacy_redirects.derived count=… skipped=… bytes=…`.

Keep in mind:

- Non-HTML files are not redirected: subscribers of `/<defaultLanguage>/rss.xml` need the
  new feed URL, and a moved sitemap must be resubmitted to search engines.
- Taxonomy alias redirects are not migrated.
- Plugin outputs should not use the old `/<defaultLanguage>/` prefix: a plugin file written
  at a redirect's path fails the build.
- Each redirect is a small file (about 0.5 KB), so the default-language file count roughly
  doubles. Route counts in `routes.json` and the build report, and the default language's
  `derived` count in `--metrics`, grow by the same number, and the redirects appear in
  `publish-audit-report.json` as non-indexable.
- The option has no effect with the prefixed layout and logs
  `i18n.config.legacy_redirects_ignored`. Turning it off later removes the redirects.

The root layout reserves other language codes as first path segments. A default
language page, list, taxonomy, alias, derived document, static file, or plugin
output whose first segment equals another language code fails with
`i18n-route-reserved-language-prefix` (case-insensitive). The default language's own
code remains available for legacy aliases such as `/en/about/`. A default-language
`static/robots.txt` becomes the site robots file. The name must be exactly
`robots.txt`: any other casing, such as `Robots.txt`, and any static file that collides
with another root-owned output fail with `i18n-root-output-conflict` before copying.

## Language Diagnostics

The build reports these warnings:

| Event | Meaning |
|---|---|
| `i18n.document.language_unknown` | A page `language` is not in `site.languages` (or is not `site.language` in a single-language build), so the page is dropped. |
| `i18n.document.i18n_key_duplicate` | Two pages in the same language use the same `i18nKey`; hreflang and merged sitemap alternates can point to only one of them. |
| `i18n.config.default_language_ignored` | `site.defaultLanguage` is set but `site.languages` is empty, so the single-language build uses `site.language`. |
| `i18n.config.default_language_layout_ignored` | `site.defaultLanguageInSubdir` or `site.defaultLanguageRedirect` is `false`, or `site.defaultLanguageLegacyRedirects` is `true`, but `site.languages` is empty, so the single-language output is unchanged. |
| `i18n.config.legacy_redirects_ignored` | `site.defaultLanguageLegacyRedirects` is `true` but the default language is still prefixed (`defaultLanguageInSubdir: true`), so there are no old URLs to redirect. |

Warnings are capped at five per kind, followed by one summary line. A single-language
site that intentionally selects one language from a mixed-language source will warn
about the other languages on every build; this is expected unless those sources are
split or filtered.

An explicit `bukit build` with two or more configured languages automatically
isolates each language in a detached Git Worktree and a separate Bukit process.
A single configured language stays in the normal in-process build and does not
require Git. Every Worktree Worker uses the same committed `HEAD` and the same
parent-produced content snapshot; root sitemap, feed, search, agent-manifest,
llms, reports, and manifests are still produced by the existing aggregate
pipeline after every language succeeds.

Worktree mode fails closed. The site must be inside a Git repository, and the
configuration, Markdown content, theme/layout/static inputs, and local plugins
used by the build must be tracked and clean. The site directory must also have
no staged, unstaged, or untracked changes, including files outside the configured
inputs. Required input symlinks must be relative and resolve within the repository;
absolute input symlinks are rejected because they bypass the snapshot. Passing this
check does not override a source reader's own no-follow rules: for example, a
Markdown source directory symlink is not supported. A custom
`content.media.downloadDir` must be a relative path without `..`.
Its resolved path must be inside the repository, and tracked changes there block the
build. Git-ignored files in a custom media directory are included in one staged
snapshot shared by all workers. Downloads are committed with the output only after
every language succeeds. Changes to the original media directory are detected by relative
path, file size, and modification time and abort the commit; do not edit it during the build.
A same-size change that preserves the modification time may not be detected. Symbolic links
inside the media directory are rejected, so a media directory containing any symbolic link
now fails a multilingual build. The default
media cache is passed separately. Unrelated changes elsewhere in the repository do not block the
build. Other ignored or external required inputs are rejected. `bukit dev`, the build
performed by `bukit deploy`, and direct `SiteEngine` callers retain the
in-process behavior and do not impose these Git requirements.

`build.languageJobs` limits simultaneously running language Worker processes.
`bukit build --jobs N` continues to limit page rendering inside each Worker.
For memory-constrained builders, start with `languageJobs: 1` and `--jobs 1`, then
measure before increasing concurrency.
Worktree isolation fixes the Git snapshot, process failure boundary, and output
commit boundary; it is not a plugin security sandbox and does not guarantee a
faster build.

If a process stops during a build, the next build attempts to recover its recorded
transaction. Interrupted record updates leave uniquely named pending files; Git
builds store them in Git's administrative directory, and Bukit does not delete
files whose ownership is unknown. If a backup is missing or output changed, Bukit
stops rather than deleting files. Preserve reported files and inspect them before
retrying. This is not a power-loss guarantee.

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
    defaultAuthor: Example News Desk
    defaultAuthorType: Organization
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
      breadcrumbHome: true
      breadcrumbParent: categories
```

BreadcrumbList JSON-LD lists real parent routes by URL path, then the current
page. `site.seo.schema.breadcrumbHome: true` starts every list with the home page
(its title, or `site.title` when there is no home route). `breadcrumbParent` names
a taxonomy kind: content listed on one of its term pages gets that term's first
page as parent, so `/2596/` in category "news" becomes Home › news › post. The
document's own term order wins; otherwise the first term page listing it. Term
names follow `content.routeMetadata` titles. Both are off by default. A
`breadcrumbParent` that is not a configured taxonomy kind (or `tags`/`categories`
without `taxonomy.kinds`) fails validation. With either option on, incremental
builds re-render a page when any of its breadcrumb names or targets change.

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
results are emitted. `site.seo.organization.sameAs` contains explicit identity URLs,
omits an empty array, and is never guessed. The configured organization becomes
the matching publisher for article JSON-LD.

`site.seo.defaultAuthor` is the last author fallback for article JSON-LD and
`page.seo.article.author`. It is used only when a page has no `authoredBy`
relation, no `author` field, and no GEO author; any of those wins. The type is
`site.seo.defaultAuthorType` (`Person` by default, or `Organization`). Use it
when one person or organization publishes all content without per-page
bylines. It does not change the content model, feeds, or `llms.txt`.

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

## Migrating to the root layout

Switching `defaultLanguageInSubdir` from `true` to `false` removes every old
`/<language>/...` default-language URL. The generated `/<language>/index.html`
redirect only covers the old home page. Before deploying:

- For the first root-layout build, confirm `dist/.bukit-output-marker` exists. If
  it does not, move the old output aside or point `build.output` at a dedicated
  empty directory; the protected clean path refuses an unmarked output directory.

- Add host redirects from `/<language>/*` to `/*` (for example a Netlify
  `_redirects` rule or a Cloudflare Pages redirect rule). GitHub Pages cannot issue
  a server 301; keep the redirect page or write aliases for important old URLs.
- Remove any existing `/` → `/<language>/` redirect rule, which would now override
  the new home page, then purge the CDN cache.
- Rebuild the `seo diff` baseline and resubmit sitemaps in Search Console. In
  `split` mode the default language sitemap moves to the root `sitemap.xml`; in
  `index` mode it stays at `/<language>/sitemap.xml`.
- Update Analytics path filters and CI scripts that hard-code `dist/<language>`.
- Read default-language reports from
  `dist/.bukit/variants/<language>/seo-report.json`, for example
  `bukit seo audit --report dist/.bukit/variants/en/seo-report.json`.
- Before adding a language, check existing default-language routes for a first
  segment equal to the new language code; the build fails with
  `i18n-route-reserved-language-prefix`.

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
| `render.output_truncated` | error | The output opens `<html>` but has no `</html>`; rendering was cut short. Title and head checks are skipped for that page. |

BreadcrumbList audit codes use the `seo.schema_breadcrumb_*` prefix and check a
non-empty item array, ListItem type, consecutive positions, non-empty names, and
valid item URLs.

If an HTML output file exists without a head, both
`seo.html_head_missing` and `seo.document_title_missing` are emitted. If the
route output file itself is absent, only `seo.output_file_missing` is emitted to
avoid cascading noise.
