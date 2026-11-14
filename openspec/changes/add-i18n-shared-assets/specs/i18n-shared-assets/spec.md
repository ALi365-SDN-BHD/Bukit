## ADDED Requirements

### Requirement: Shared assets are opt-in for multilingual root-layout sites
Bukit SHALL read an optional `site.sharedAssets` object with a boolean
`enabled` and an optional list `paths`. `enabled` SHALL default to `false`.
When the site has fewer than two languages, the setting SHALL have no effect,
and Bukit SHALL log the warning `i18n.config.shared_assets_ignored` when
`enabled` is `true`. When `enabled` is `true` and
`site.defaultLanguageInSubdir` is `true`, configuration validation SHALL fail
naming both settings. When sharing is not enabled, every language SHALL keep
its own copy of every asset, exactly as before this change.

#### Scenario: Sharing not enabled
- **WHEN** `languages` is `[en, zh-CN]`, the default language `en` is at the root, and `sharedAssets` is not set
- **THEN** both `dist/assets/style.css` and `dist/zh-CN/assets/style.css` exist

#### Scenario: Sharing enabled
- **WHEN** `languages` is `[en, zh-CN]`, the default language `en` is at the root, and `sharedAssets.enabled` is `true`
- **THEN** `dist/assets/style.css` exists and `dist/zh-CN/assets/style.css` does not

#### Scenario: Prefixed layout rejected
- **WHEN** `sharedAssets.enabled` is `true` and `defaultLanguageInSubdir` is `true`
- **THEN** configuration validation fails naming `sharedAssets.enabled` and `defaultLanguageInSubdir`

#### Scenario: Single-language site
- **WHEN** `languages` is `[en]` and `sharedAssets.enabled` is `true`
- **THEN** the output is identical to a build without `sharedAssets` and the warning `i18n.config.shared_assets_ignored` is logged

### Requirement: The effective shared set is a set of output prefixes
Each entry in `sharedAssets.paths` SHALL be an output path prefix relative to a
language output directory. Entries SHALL be normalized by converting `\` to
`/`, removing a leading `/`, and appending a trailing `/` when missing.
Configuration validation SHALL fail for an empty list, an absolute path, a path
containing `..`, or an entry that normalizes to the output root. Prefix
matching and de-duplication SHALL use the same case rules as output destination
identity. When `paths` is not set, it SHALL default to the de-duplicated set of
`assets/` and the SCSS output directory, omitting with a warning an SCSS output
directory that normalizes to the output root. A configured list SHALL replace
the default. The content media prefix, derived from `content.media.urlBase`,
SHALL always be excluded: an entry equal to or inside the media prefix SHALL
fail configuration validation, and an entry containing the media prefix SHALL
have it excluded internally. The effective shared set SHALL be the configured
or default prefixes minus the media prefix, and every rule that refers to
shared paths SHALL apply to the effective shared set.

#### Scenario: Root prefix rejected
- **WHEN** `sharedAssets.paths` is `[/]`
- **THEN** configuration validation fails naming the entry

#### Scenario: SCSS output at the root
- **WHEN** `paths` is not set and the SCSS output directory is the output root
- **THEN** the SCSS output directory is not a default shared prefix and a warning is logged

#### Scenario: Explicit media prefix rejected
- **WHEN** `content.media.urlBase` is `/assets/uploads` and `sharedAssets.paths` is `[assets/uploads/]`
- **THEN** configuration validation fails stating that media sharing is not supported

#### Scenario: Media stays per language
- **WHEN** sharing uses the default paths and a Chinese post references a media image
- **THEN** the image is written under `dist/zh-CN/assets/uploads/` and its URL keeps the Chinese base URL

### Requirement: The default language owns shared assets
When sharing is enabled, the default language build SHALL plan, write, record,
and clean its assets exactly as without sharing. Every other language SHALL
omit from its asset plan each copied or generated output of the theme asset,
token, or static categories whose destination falls within the effective
shared set. Pages, templated static files, plugin outputs, projections, and
media SHALL be planned and written per language unchanged, including when they
fall within the effective shared set. The root manifest and language manifest
formats SHALL NOT change.

#### Scenario: Static under assets is shared
- **WHEN** sharing uses the default paths and `static/assets/logo.svg` exists
- **THEN** `dist/assets/logo.svg` exists and `dist/zh-CN/assets/logo.svg` does not

#### Scenario: Other static files are not shared by default
- **WHEN** sharing uses the default paths and `static/fonts/a.woff2` exists
- **THEN** both `dist/fonts/a.woff2` and `dist/zh-CN/fonts/a.woff2` exist

#### Scenario: Configured static prefix
- **WHEN** `sharedAssets.paths` is `[assets/, fonts/]` and `static/fonts/a.woff2` exists
- **THEN** `dist/fonts/a.woff2` exists and `dist/zh-CN/fonts/a.woff2` does not

#### Scenario: Theme tokens shared
- **WHEN** sharing is enabled and the theme defines tokens
- **THEN** `dist/assets/css/theme-tokens.css` exists and `dist/zh-CN/assets/css/theme-tokens.css` does not

#### Scenario: Plugin output within the shared set
- **WHEN** sharing uses the default paths and a plugin writes `assets/plugin-data.json` during the Chinese AfterBuild
- **THEN** the build succeeds and `dist/zh-CN/assets/plugin-data.json` exists

### Requirement: The default language builds first when sharing is enabled
When sharing is enabled, the default language build SHALL complete before any
other language build starts, in both in-process and worktree builds. The other
languages SHALL then run concurrently up to `build.languageJobs`. When the
default language build fails, no other language build SHALL start and the
published output SHALL remain unchanged. When sharing is not enabled, language
scheduling SHALL be unchanged.

#### Scenario: Default language listed last
- **WHEN** sharing is enabled, `languages` is `[zh-CN, en]`, the default language is `en`, and `languageJobs` is `2`
- **THEN** the `en` build completes before the `zh-CN` build starts

#### Scenario: Default language failure
- **WHEN** sharing is enabled and the default language build fails
- **THEN** no other language build starts and the published output is unchanged

### Requirement: Templates reference shared assets through asset_base_url
Templates SHALL receive `site.asset_base_url`. When sharing is enabled it SHALL
equal the site root base URL without a language segment, preserving a non-root
base URL such as `/repo`. Otherwise it SHALL equal the current language
`base_url`. It SHALL use the same normalization as `base_url`. Bukit SHALL NOT
rewrite asset URLs written by templates. The Starter theme SHALL reference its
assets through `site.asset_base_url`.

#### Scenario: Asset URL on a non-default language page
- **WHEN** sharing is enabled, `site.baseUrl` is `/repo`, and a `zh-CN` page renders `{{ site.asset_base_url }}/assets/style.css`
- **THEN** the rendered URL is `/repo/assets/style.css`

#### Scenario: Asset URL with sharing disabled
- **WHEN** sharing is not enabled, `site.baseUrl` is `/repo`, and a `zh-CN` page renders `{{ site.asset_base_url }}/assets/style.css`
- **THEN** the rendered URL is `/repo/zh-CN/assets/style.css`

### Requirement: Core-generated site URLs follow the shared set
When sharing is enabled, every site-relative URL that Bukit itself resolves to
an absolute or base-prefixed URL, including SEO images, author images, and
structured data URLs, SHALL use the site root base URL when its path falls
within the effective shared set, and the current language base URL otherwise.
In a non-default language, when the content media fallback image path falls
within the effective shared set, its rewritten URL SHALL use the site root base
URL and media validation SHALL require the file to exist under the shared asset
root instead of in that language's planned outputs.

#### Scenario: Default SEO image on a non-default language page
- **WHEN** sharing is enabled, `site.url` is `https://example.com`, `site.baseUrl` is `/repo`, `seo.defaultImage` is `/assets/og.png`, and a `zh-CN` page has no own image
- **THEN** its `og:image` is `https://example.com/repo/assets/og.png`

#### Scenario: Media fallback on a non-default language page
- **WHEN** sharing uses the default paths, `site.baseUrl` is `/repo`, `defaultImageUrl` is `/assets/images/noneimg-news.jpg`, and a `zh-CN` page falls back to it
- **THEN** the page references `/repo/assets/images/noneimg-news.jpg` and the Chinese media validation passes

### Requirement: Non-default languages reuse shared image variants read-only
Variant size planning and file naming SHALL be a single function that
distinguishes generated files from `srcset` and WebP candidates, and that is
used by variant generation, WebP mapping, the `srcset` transform, and the
shared mapping below. When sharing is enabled, a non-default language build
SHALL build a shared WebP mapping for source images within the effective shared
set by reading the shared asset root, including a candidate only when its file
exists and its freshness record is valid for the current source. Its WebP
picture rewrite SHALL use its own mapping combined with the shared mapping and
SHALL accept image URLs prefixed by either its own base URL or
`site.asset_base_url`. A non-default language build SHALL NOT create, modify,
or delete any file within the effective shared set at the shared asset root,
and SHALL NOT run orphan or freshness cleanup there. Variant generation for the
default language and its failure policy SHALL be unchanged.

#### Scenario: Shared theme image on two language pages
- **WHEN** sharing is enabled with WebP output, `site.baseUrl` is `/repo`, and an English page and a Chinese page both use `/repo/assets/hero.jpg`
- **THEN** each variant exists once under `dist/assets/` and both pages contain a WebP `<picture>` source whose URLs start with `/repo/assets/`

#### Scenario: Default language variant failure in warn mode
- **WHEN** sharing is enabled, `site.pluginFailMode` is `warn`, and generating the WebP variant of a shared image fails in the default language
- **THEN** the Chinese page that uses the image keeps its original `<img>` and references no missing variant

### Requirement: Shared asset settings affect incremental rendering
The effective `sharedAssets` value and `site.asset_base_url` SHALL contribute to
the render dependency hash. After the setting changes between builds, every
page SHALL be re-rendered, and the existing per-language stale output cleanup
SHALL remove or restore non-default language copies without a protected clean.

#### Scenario: Enabling sharing on an incremental build
- **WHEN** a site built with sharing disabled and `build.clean: false` is rebuilt with sharing enabled
- **THEN** `dist/zh-CN/assets/style.css` no longer exists, `dist/assets/style.css` remains, and Chinese pages rendered with the Starter theme reference `/assets/style.css`

#### Scenario: Disabling sharing on an incremental build
- **WHEN** a site built with sharing enabled and `build.clean: false` is rebuilt with sharing disabled
- **THEN** `dist/zh-CN/assets/style.css` exists again and Chinese pages rendered with the Starter theme reference `/zh-CN/assets/style.css`

### Requirement: Worktree builds share assets like in-process builds
In worktree builds with sharing enabled, the parent SHALL run the default
language worker to completion before dispatching other workers, and SHALL pass
every worker the shared asset root, which is the staging output root. Workers
SHALL apply the same asset plan filtering, URL rules, and read-only variant
reuse as in-process language builds. Worktree and in-process builds of the same
input SHALL produce the same relative file paths and contents.

#### Scenario: Worktree and in-process equivalence
- **WHEN** the same clean commit with sharing enabled is built once by a worktree build and once in process
- **THEN** both outputs contain the same relative file paths with the same contents, and neither contains `zh-CN/assets/style.css`
