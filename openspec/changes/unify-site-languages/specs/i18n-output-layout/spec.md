## ADDED Requirements

### Requirement: Site languages are the only language setting
Bukit SHALL read the site language only from `site.languages`. When
`site.languages` is not set, it SHALL default to `[zh-CN]`. A list with exactly
one language SHALL build a single-language site: output and base URL stay at the
site root, and Bukit SHALL NOT write language directories, hreflang alternates,
or default-language redirect pages. A list with two or more languages SHALL
build a multilingual site as defined by the other requirements of this
capability. Wherever this capability says `site.languages` is empty, it SHALL
mean the site has fewer than two languages, and wherever it says
`site.languages` is non-empty or the site is multilingual, it SHALL mean the
site has two or more languages. An empty list SHALL be treated as not set, and
a list whose entries are all blank SHALL fail validation. The `site.language` key SHALL be
rejected by configuration validation with a message that names
`languages: [<code>]` as the replacement. The template value `site.language`
SHALL still be available: in a single-language site it SHALL be the one
configured language, and in a multilingual site it SHALL be the language being
built. When `site.languages` has one entry, `site.defaultLanguage` SHALL be
either unset or equal to that entry. In a single-language site, a search record
without its own language SHALL use the site language instead of `und`.

#### Scenario: One language builds at the root
- **WHEN** `languages` is `[en]`
- **THEN** the home page is written to `dist/index.html`, no `dist/en/` directory is created, no root redirect page is written, and templates see `site.language` as `en`

#### Scenario: Languages not set
- **WHEN** `site.yaml` has no `languages` key
- **THEN** the site builds as a single-language `zh-CN` site at the output root

#### Scenario: Two languages stay multilingual
- **WHEN** `languages` is `[en, zh-CN]`
- **THEN** the build writes both language variants and their hreflang alternates

#### Scenario: Old key is rejected
- **WHEN** `site.yaml` sets `site.language: en`
- **THEN** configuration validation fails and the message tells the user to write `languages: [<code>]` instead

#### Scenario: Default language outside the single entry
- **WHEN** `languages` is `[en]` and `defaultLanguage` is `zh-CN`
- **THEN** configuration validation fails because `defaultLanguage` is not in `languages`

#### Scenario: Single-language search record language
- **WHEN** `languages` is `[en]` and a page sets no language of its own
- **THEN** its record in `search.json` has `language` set to `en`

## MODIFIED Requirements

### Requirement: Default language prefix is configurable
Bukit SHALL read an optional boolean `site.defaultLanguageInSubdir`, defaulting
to `false`. When the site has two or more languages and the value is `true`,
every language, including the default language, SHALL build into
`<output>/<lang>/` with base URL `<baseUrl>/<lang>`. When the value is `false`,
the default language SHALL build into the output root with the site root base
URL, and every other language SHALL keep `<output>/<lang>/` and
`<baseUrl>/<lang>`. When the site has fewer than two languages, the value SHALL
have no effect and Bukit SHALL log `i18n.config.default_language_layout_ignored`
if it is set to `true`.

#### Scenario: Default language at the root
- **WHEN** `languages` is `[en, zh-CN]`, `defaultLanguage` is `en`, and `defaultLanguageInSubdir` is not set
- **THEN** the English home page is written to `dist/index.html`, the English 404 page to `dist/404.html`, and the Chinese home page to `dist/zh-CN/index.html`

#### Scenario: Default remains prefixed
- **WHEN** `languages` is `[en, zh-CN]` and `defaultLanguageInSubdir` is `true`
- **THEN** the English home page is written to `dist/en/index.html` and no page content is rendered at the output root

#### Scenario: Adding a second language keeps default-language URLs
- **WHEN** a site with `languages: [en]` changes to `languages: [en, zh-CN]` with `defaultLanguage: en` and `defaultLanguageInSubdir` not set
- **THEN** the English pages keep the same output paths and URLs as before

#### Scenario: Setting without languages
- **WHEN** `languages` is `[en]` and `defaultLanguageInSubdir` is `true`
- **THEN** the build output stays at the output root and `i18n.config.default_language_layout_ignored` is logged

### Requirement: Changing the layout cleans the output safely
The layout setting SHALL contribute to the incremental site hash only when the
site has two or more languages and the value is `true`. When the recorded layout
of the previous build differs from the current one, Bukit SHALL clean the output
directory through the existing protected clean path before writing, so no
content output from the previous layout remains. When `defaultLanguageRedirect`
is `true`, the root layout may still write `<lang>/index.html` as a redirect
page, and with `search.mode: merged` it writes the default language's own index
at `<lang>/search.json`. Stale file and empty directory removal for the root
default language SHALL NOT remove files or directories owned by another
language.

#### Scenario: Switching to a root default language
- **WHEN** a site previously built with `dist/en/` is rebuilt with `defaultLanguageInSubdir: false`, `defaultLanguageRedirect: false`, and a `search.mode` other than `merged`
- **THEN** `dist/en/` no longer exists after the build and `dist/index.html` is the English home page

#### Scenario: Upgrade without opting in
- **WHEN** a single-language site, or a multilingual site that does not set `defaultLanguageInSubdir`, is rebuilt
- **THEN** the layout setting does not contribute to the incremental site hash

#### Scenario: Incremental removal near another language
- **WHEN** the default language is at the root and the only English page under `/guides/` is deleted while `dist/zh-CN/guides/` exists
- **THEN** the English output `dist/guides/` is removed and `dist/zh-CN/guides/` is unchanged

### Requirement: Default language redirect pages
Bukit SHALL read an optional boolean `site.defaultLanguageRedirect`, defaulting
to `true`, effective only when the site has two or more languages. When it is
`true` and the default language is prefixed, the root aggregate SHALL write
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
- **WHEN** `languages` is `[en, zh-CN]`, `site.url` is `https://example.com`, `defaultLanguageInSubdir` is `true`, and `defaultLanguageRedirect` is not set
- **THEN** `dist/index.html` is a noindex redirect page whose meta refresh and canonical target `https://example.com/en/`, and it is not listed in any sitemap or search index

#### Scenario: Legacy home redirect for a root default language
- **WHEN** `languages` is `[en, zh-CN]` and neither `defaultLanguageInSubdir` nor `defaultLanguageRedirect` is set
- **THEN** `dist/en/index.html` is a noindex redirect page targeting the site root

#### Scenario: User content wins over the legacy redirect
- **WHEN** the default language `en` is at the root and an English page declares the alias `/en/`
- **THEN** `dist/en/index.html` is the alias output and an info message reports that the generated redirect was skipped

#### Scenario: Redirects disabled
- **WHEN** `defaultLanguageRedirect` is `false`
- **THEN** no `dist/index.html` is written for a prefixed site and no `dist/<defaultLang>/index.html` redirect is written for a root default language

### Requirement: Merged search with a root default language
When `site.defaultLanguageInSubdir` is `false`, the site has two or more
languages and `site.search.mode` resolves to `merged`, configuration validation
SHALL accept the site. The root aggregate SHALL write the merged index at
`search.json`, the default language SHALL write its own index at
`<defaultLang>/search.json`, and every other language SHALL keep
`<lang>/search.json`. The projection plan, the build manifest ownership, the
default-language publish audit and the default-language search UI SHALL use
that same path. In every search mode, every search record SHALL carry a
non-empty `language`: the record's own language, or, when it has none or `und`,
the language of the variant that wrote it in a multilingual build and the site
language in a single-language build.

#### Scenario: Root layout with merged search
- **WHEN** `languages` is `[en, zh-CN]`, `defaultLanguage` is `en`, `defaultLanguageInSubdir` is `false`, and `search.mode` is `merged`
- **THEN** `dist/search.json` lists the records of both languages, each with its `language`, `dist/en/search.json` lists only English records, `dist/zh-CN/search.json` lists only Chinese records, and `dist/bukit-search.html` loads `/en/search.json`

#### Scenario: Static file collides with the merged index
- **WHEN** the same site has a default-language `static/search.json`
- **THEN** the build fails with `i18n-root-output-conflict` naming `static/search.json`
