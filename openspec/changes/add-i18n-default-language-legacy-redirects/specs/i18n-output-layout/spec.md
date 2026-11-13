## ADDED Requirements

### Requirement: Migration redirects for default-language legacy URLs
Bukit SHALL read an optional boolean `site.defaultLanguageLegacyRedirects`,
defaulting to `false`. It SHALL take effect only when `site.languages` is
non-empty and `site.defaultLanguageInSubdir` is `false`. When it is `true` in
a multilingual site whose default language is prefixed, Bukit SHALL log the
warning `i18n.config.legacy_redirects_ignored` and derive nothing. When
`site.languages` is empty and it is `true`, Bukit SHALL log
`i18n.config.default_language_layout_ignored`.

When it takes effect, the default language SHALL derive one redirect document
for every HTML output among its content pages, list and pagination routes,
non-redirect derived documents, and static HTML routes, at
`<defaultLang>/<output path>` with the route URL `/<defaultLang><route url>`,
targeting the page's current URL (absolute when `site.url` is set). The home
route `/` and `404.html` SHALL be excluded; the legacy home redirect remains
controlled only by `site.defaultLanguageRedirect`. Candidates SHALL be
de-duplicated by output path ignoring case. For every alias redirect, Bukit
SHALL derive `/<defaultLang><alias url>` targeting the aliased page directly,
skipping aliases whose first segment already equals the default language code.
A redirect SHALL be skipped when any content page, derived document, list
route, static HTML route, or static source file already occupies its path.
Redirect documents SHALL use the id and slug `i18n-legacy-<lang>-<hash>`, SHALL
be noindex, and SHALL be excluded from sitemaps, search indexes, `llms.txt`,
`llms-full.txt`, `agent-manifest.json`, feeds, and content projections. Bukit
SHALL refuse to write a redirect through a symbolic link in the output. The
setting SHALL NOT contribute to the incremental site hash.

#### Scenario: Old content URL redirects
- **WHEN** `languages` is `[en, zh-CN]`, `defaultLanguageInSubdir` is `false`, `defaultLanguageLegacyRedirects` is `true`, and an English page is at `/about/`
- **THEN** `dist/en/about/index.html` is a noindex redirect page targeting the English page at `/about/`

#### Scenario: Home redirect stays independent
- **WHEN** the setting is `true` and `defaultLanguageRedirect` is `false`
- **THEN** no `dist/en/index.html` is written and other legacy URLs still redirect

#### Scenario: User content wins over a migration redirect
- **WHEN** the setting is `true` and an English page declares the alias `/en/about/`
- **THEN** `dist/en/about/index.html` is that alias output and the summary log counts one skipped redirect

#### Scenario: Prefixed layout ignores the setting
- **WHEN** `languages` is `[en, zh-CN]`, `defaultLanguageInSubdir` is `true`, and the setting is `true`
- **THEN** the build succeeds, no migration redirect is written, and `i18n.config.legacy_redirects_ignored` is logged

#### Scenario: Disabling removes the redirects
- **WHEN** an incremental build follows a build with the setting `true` and the setting is now `false`
- **THEN** every migration redirect is removed and no other page is re-rendered
