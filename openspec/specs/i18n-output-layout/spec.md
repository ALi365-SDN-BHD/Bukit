# i18n-output-layout Specification

## Purpose
定义多语言站点的输出布局合同：默认语言放在语言子目录或输出根目录、各语言 URL 与 alternates、根目录文件的唯一归属、默认语言跳转页、保留语言前缀，以及布局切换时的安全清理。

## Requirements

### Requirement: Default language prefix is configurable
Bukit SHALL read an optional boolean `site.defaultLanguageInSubdir`, defaulting
to `true`. When `site.languages` is non-empty and the value is `true`, every
language, including the default language, SHALL build into `<output>/<lang>/`
with base URL `<baseUrl>/<lang>`, exactly as before this change. When the value
is `false`, the default language SHALL build into the output root with the site
root base URL, and every other language SHALL keep `<output>/<lang>/` and
`<baseUrl>/<lang>`. When `site.languages` is empty, the value SHALL have no
effect and Bukit SHALL log a warning if it is `false`.

#### Scenario: Default remains prefixed
- **WHEN** `languages` is `[en, zh-CN]` and `defaultLanguageInSubdir` is not set
- **THEN** the English home page is written to `dist/en/index.html` and no page content is rendered at the output root

#### Scenario: Default language at the root
- **WHEN** `languages` is `[en, zh-CN]`, `defaultLanguage` is `en`, and `defaultLanguageInSubdir` is `false`
- **THEN** the English home page is written to `dist/index.html`, the English 404 page to `dist/404.html`, and the Chinese home page to `dist/zh-CN/index.html`

#### Scenario: Setting without languages
- **WHEN** `languages` is empty and `defaultLanguageInSubdir` is `false`
- **THEN** the build output is unchanged and a warning is logged

### Requirement: Language URLs and alternates follow the configured layout
When the default language is at the root, its page URLs, canonical URLs,
`hreflang` alternates, `x-default` alternate, sitemap entries, feed links,
search index paths, and template `site.base_url` SHALL use the site root base
URL without a language segment. Other languages SHALL keep their language
segment. A non-root site base URL such as `/repo` SHALL be preserved as the
prefix for every language.

#### Scenario: hreflang for a root default language
- **WHEN** `site.url` is `https://example.com`, the default language `en` is at the root, and `about` exists in `en` and `zh-CN` with the same `i18nKey`
- **THEN** the `en` and `x-default` alternates are `https://example.com/about/` and the `zh-CN` alternate is `https://example.com/zh-CN/about/`

#### Scenario: Project site base URL
- **WHEN** `site.baseUrl` is `/repo` and the default language `en` is at the root
- **THEN** the English base URL is `/repo` and the Chinese base URL is `/repo/zh-CN`

### Requirement: Root output files have exactly one owner
When the default language is at the root, `agent-manifest.json`, `robots.txt`,
`llms.txt`, and `llms-full.txt` at the output root SHALL be produced only by the
root aggregate writers, and the default language SHALL neither plan nor write
them. With `sitemapMode: index`, the root `sitemap.xml` SHALL remain the sitemap
index and the default language sitemap SHALL be written to `<lang>/sitemap.xml`
with unprefixed page URLs, and referenced by the index and by `robots.txt`.
With `sitemapMode: split`, the default language sitemap SHALL be written to the
root `sitemap.xml`. In either layout, no language SHALL plan or write a
per-language sitemap under `sitemapMode: merged` or per-language feeds under
`feed.mode: merged`; only the root aggregate provides them. Any remaining duplicate destination SHALL fail the build instead of
overwriting a file.

#### Scenario: Sitemap index with a root default language
- **WHEN** the default language `en` is at the root, `languages` is `[en, zh-CN]`, and `sitemapMode` is `index`
- **THEN** `dist/sitemap.xml` is an index listing `https://example.com/en/sitemap.xml` and `https://example.com/zh-CN/sitemap.xml`, and `dist/en/sitemap.xml` lists `https://example.com/` rather than `https://example.com/en/`

#### Scenario: Merged sitemap without per-language copies
- **WHEN** `languages` is `[en, zh-CN]`, `sitemapMode` is `merged`, and `defaultLanguageInSubdir` is not set
- **THEN** `dist/sitemap.xml` is the merged sitemap and neither `dist/en/sitemap.xml` nor `dist/zh-CN/sitemap.xml` is written

#### Scenario: Aggregate files at the root
- **WHEN** the default language is at the root and GEO `llmsTxt` is enabled
- **THEN** exactly one `dist/llms.txt` exists and it is the merged root aggregate

### Requirement: Default language static files respect root ownership
When the default language is at the root, a default language `static/robots.txt`
SHALL be used as the site robots file and the root robots writer SHALL NOT
generate one. A default language static file whose output path equals another
root-owned file (`agent-manifest.json`, `llms.txt`, `llms-full.txt`, the root
sitemap under `merged` or `index` mode, or a merged root feed) SHALL fail the
build with `i18n-root-output-conflict` before any file is copied, naming the
static source path and the conflicting setting. Other static files SHALL be
copied to the output root unchanged.

#### Scenario: Static robots override
- **WHEN** the default language is at the root and `static/robots.txt` exists
- **THEN** `dist/robots.txt` equals the static file and no generated robots file is written

#### Scenario: Static llms conflict
- **WHEN** the default language is at the root, GEO `llmsTxt` is enabled, and `static/llms.txt` exists
- **THEN** the build fails with `i18n-root-output-conflict` naming `static/llms.txt`

### Requirement: Merged search is rejected with a root default language
Configuration validation SHALL fail when `site.defaultLanguageInSubdir` is
`false`, `site.languages` is non-empty, and `site.search.mode` resolves to
`merged`.

#### Scenario: Merged search combination
- **WHEN** `defaultLanguageInSubdir` is `false` and `search.mode` is `merged`
- **THEN** configuration validation fails and names both settings

### Requirement: Root default language reports do not collide with merged reports
When the default language is at the root, every per-language build report it
writes, including `seo-report.json`, `publish-audit-report.json`,
`seo-route-map.json`, `geo-report.json`, and `analytics-report.json`, SHALL be
written under `<output>/.bukit/variants/<lang>/`. Merged root reports SHALL keep
their existing paths under `<output>/.bukit/`. Every consumer that locates a
per-language report, including the build report artifact inventory, SHALL
resolve it from the language's actual report directory.

#### Scenario: SEO reports
- **WHEN** the default language `en` is at the root and the build completes
- **THEN** `dist/.bukit/seo-report.json` is the merged report and the English report is `dist/.bukit/variants/en/seo-report.json`

#### Scenario: Analytics report in the build report
- **WHEN** analytics is enabled and the default language `en` is at the root
- **THEN** the build report artifact inventory lists `.bukit/variants/en/analytics-report.json`

### Requirement: Other language codes are reserved route prefixes
When the default language is at the root, the build SHALL fail with
`i18n-route-reserved-language-prefix` if the first path segment of any default
language output equals another configured language code. The comparison SHALL
be case-insensitive on every operating system. The check SHALL cover content
routes, list and pagination routes, taxonomy routes, aliases, derived documents,
static files, and plugin outputs. Route, static, and planned asset checks SHALL
run before any file of that language is written. Plugin outputs SHALL be
checked before the output is committed; when a default language plugin output
violates the rule, the build SHALL report that violation as the primary
failure even if another language also fails. The error SHALL name the
offending document id or source path and the reserved language code. The
default language's own code SHALL NOT be reserved.

#### Scenario: Taxonomy term named like a language
- **WHEN** `languages` is `[en, ms]`, `en` is at the root, and an English post has the tag `ms`
- **THEN** the build fails with `i18n-route-reserved-language-prefix` naming the taxonomy route and `ms`

#### Scenario: Case-insensitive collision on any platform
- **WHEN** `languages` is `[en, zh-CN]`, `en` is at the root, and an English page has permalink `/ZH-cn/notes/`
- **THEN** the build fails with `i18n-route-reserved-language-prefix` on both case-sensitive and case-insensitive file systems

#### Scenario: Adding a language
- **WHEN** a root-layout site with `languages: [en, zh-CN]` has an English page at `/ms/guide/` and `ms` is added to `languages`
- **THEN** the build fails with `i18n-route-reserved-language-prefix` naming the page and `ms`

#### Scenario: Legacy alias under the default language code
- **WHEN** `en` is at the root and an English page declares the alias `/en/about/`
- **THEN** the build succeeds and writes a redirect page at `dist/en/about/index.html`

### Requirement: Changing the layout cleans the output safely
The layout setting SHALL contribute to the incremental site hash only when it is
`false`. When the recorded layout of the previous build differs from the current
one, Bukit SHALL clean the output directory through the existing protected
clean path before writing, so no content output from the previous layout
remains. When `defaultLanguageRedirect` is `true`, the root layout may still
write `<lang>/index.html` as a redirect page. Stale file and empty directory
removal for the root default language SHALL NOT remove files or directories
owned by another language.

#### Scenario: Switching to a root default language
- **WHEN** a site previously built with `dist/en/` is rebuilt with `defaultLanguageInSubdir: false` and `defaultLanguageRedirect: false`
- **THEN** `dist/en/` no longer exists after the build and `dist/index.html` is the English home page

#### Scenario: Upgrade without opting in
- **WHEN** an existing multilingual site upgrades Bukit without setting `defaultLanguageInSubdir` or `defaultLanguageRedirect`
- **THEN** the incremental site hash and every existing output path are unchanged, except the per-language sitemap and feed copies that `sitemapMode: merged` or `feed.mode: merged` no longer writes, and the only new public file is the root redirect `dist/index.html`

#### Scenario: Incremental removal near another language
- **WHEN** the default language is at the root and the only English page under `/guides/` is deleted while `dist/zh-CN/guides/` exists
- **THEN** the English output `dist/guides/` is removed and `dist/zh-CN/guides/` is unchanged

### Requirement: Default language redirect pages
Bukit SHALL read an optional boolean `site.defaultLanguageRedirect`, defaulting
to `true`, effective only when `site.languages` is non-empty. When it is `true`
and the default language is prefixed, the root aggregate SHALL write
`<output>/index.html` redirecting to `<baseUrl>/<defaultLang>/`. When it is
`true` and the default language is at the root, the default language SHALL write
`<output>/<defaultLang>/index.html` redirecting to `<baseUrl>/`, unless another
default language output already occupies that path, in which case the redirect
SHALL be skipped and an info message logged. Every redirect page SHALL contain a
meta refresh, a canonical link to the target, a `<title>`, and
`<meta name="robots" content="noindex">`, SHALL use an absolute target URL when
`site.url` is set, and SHALL be excluded from sitemaps, search indexes,
`llms.txt`, `llms-full.txt`, `agent-manifest.json`, and feeds. When it is
`false`, neither redirect page SHALL be written. The setting SHALL contribute to
the incremental site hash only when it is `false`.

#### Scenario: Root redirect for a prefixed site
- **WHEN** `languages` is `[en, zh-CN]`, `site.url` is `https://example.com`, and neither `defaultLanguageInSubdir` nor `defaultLanguageRedirect` is set
- **THEN** `dist/index.html` is a noindex redirect page whose meta refresh and canonical target `https://example.com/en/`, and it is not listed in any sitemap or search index

#### Scenario: Legacy home redirect for a root default language
- **WHEN** `languages` is `[en, zh-CN]` and `defaultLanguageInSubdir` is `false`
- **THEN** `dist/en/index.html` is a noindex redirect page targeting the site root

#### Scenario: User content wins over the legacy redirect
- **WHEN** the default language `en` is at the root and an English page declares the alias `/en/`
- **THEN** `dist/en/index.html` is the alias output and an info message reports that the generated redirect was skipped

#### Scenario: Redirects disabled
- **WHEN** `defaultLanguageRedirect` is `false`
- **THEN** no `dist/index.html` is written for a prefixed site and no `dist/<defaultLang>/index.html` redirect is written for a root default language
