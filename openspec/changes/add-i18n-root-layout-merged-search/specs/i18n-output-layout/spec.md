## REMOVED Requirements

### Requirement: Merged search is rejected with a root default language
**Reason**: The root layout now supports merged search by moving the default language's own index to `<defaultLang>/search.json`.
**Migration**: None. The combination was rejected, so no existing site depends on the rejection.

## MODIFIED Requirements

### Requirement: Changing the layout cleans the output safely
The layout setting SHALL contribute to the incremental site hash only when it is
`false`. When the recorded layout of the previous build differs from the current
one, Bukit SHALL clean the output directory through the existing protected
clean path before writing, so no content output from the previous layout
remains. When `defaultLanguageRedirect` is `true`, the root layout may still
write `<lang>/index.html` as a redirect page, and with `search.mode: merged` it
writes the default language's own index at `<lang>/search.json`. Stale file and empty directory
removal for the root default language SHALL NOT remove files or directories
owned by another language.

#### Scenario: Switching to a root default language
- **WHEN** a site previously built with `dist/en/` is rebuilt with `defaultLanguageInSubdir: false`, `defaultLanguageRedirect: false`, and a `search.mode` other than `merged`
- **THEN** `dist/en/` no longer exists after the build and `dist/index.html` is the English home page

#### Scenario: Upgrade without opting in
- **WHEN** an existing multilingual site upgrades Bukit without setting `defaultLanguageInSubdir` or `defaultLanguageRedirect`
- **THEN** the incremental site hash and every existing output path are unchanged, except the per-language sitemap and feed copies that `sitemapMode: merged` or `feed.mode: merged` no longer writes, and the only new public file is the root redirect `dist/index.html`

#### Scenario: Incremental removal near another language
- **WHEN** the default language is at the root and the only English page under `/guides/` is deleted while `dist/zh-CN/guides/` exists
- **THEN** the English output `dist/guides/` is removed and `dist/zh-CN/guides/` is unchanged

### Requirement: Default language static files respect root ownership
When the default language is at the root, a default language `static/robots.txt`
SHALL be used as the site robots file and the root robots writer SHALL NOT
generate one. A default language static file whose output path equals another
root-owned file (`agent-manifest.json`, `llms.txt`, `llms-full.txt`, the root
sitemap under `merged` or `index` mode, a merged root feed, or the merged root
`search.json`) SHALL fail the
build with `i18n-root-output-conflict` before any file is copied, naming the
static source path and the conflicting setting. Other static files SHALL be
copied to the output root unchanged.

#### Scenario: Static robots override
- **WHEN** the default language is at the root and `static/robots.txt` exists
- **THEN** `dist/robots.txt` equals the static file and no generated robots file is written

#### Scenario: Static llms conflict
- **WHEN** the default language is at the root, GEO `llmsTxt` is enabled, and `static/llms.txt` exists
- **THEN** the build fails with `i18n-root-output-conflict` naming `static/llms.txt`

## ADDED Requirements

### Requirement: Merged search with a root default language
When `site.defaultLanguageInSubdir` is `false`, `site.languages` is non-empty
and `site.search.mode` resolves to `merged`, configuration validation SHALL
accept the site. The root aggregate SHALL write the merged index at
`search.json`, the default language SHALL write its own index at
`<defaultLang>/search.json`, and every other language SHALL keep
`<lang>/search.json`. The projection plan, the build manifest ownership, the
default-language publish audit and the default-language search UI SHALL use
that same path. In every search mode, every search record SHALL carry a
non-empty `language`: the record's own language, or, when it has none or `und`,
the language of the variant that wrote it in a multilingual build and `und`
otherwise.

#### Scenario: Root layout with merged search
- **WHEN** `languages` is `[en, zh-CN]`, `defaultLanguage` is `en`, `defaultLanguageInSubdir` is `false`, and `search.mode` is `merged`
- **THEN** `dist/search.json` lists the records of both languages, each with its `language`, `dist/en/search.json` lists only English records, `dist/zh-CN/search.json` lists only Chinese records, and `dist/bukit-search.html` loads `/en/search.json`

#### Scenario: Static file collides with the merged index
- **WHEN** the same site has a default-language `static/search.json`
- **THEN** the build fails with `i18n-root-output-conflict` naming `static/search.json`
