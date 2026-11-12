## ADDED Requirements

### Requirement: Shared assets are enabled by default for multilingual sites
Bukit SHALL read an optional `site.sharedAssets` object with a boolean
`enabled` and an optional list `paths`. When `site.languages` is non-empty,
`enabled` SHALL default to `true`. When `site.languages` is empty, the setting
SHALL have no effect. When `enabled` is `false`, every language SHALL keep its
own copy of every asset, exactly as before this change.

#### Scenario: Default sharing
- **WHEN** `languages` is `[en, zh-CN]` and `sharedAssets` is not set
- **THEN** `dist/assets/style.css` exists exactly once and neither `dist/en/assets/` nor `dist/zh-CN/assets/` exists

#### Scenario: Sharing disabled
- **WHEN** `languages` is `[en, zh-CN]` and `sharedAssets.enabled` is `false`
- **THEN** `dist/en/assets/style.css` and `dist/zh-CN/assets/style.css` both exist and no `dist/assets/` is written

### Requirement: Shared paths are output prefixes
Each entry in `sharedAssets.paths` SHALL be an output path prefix relative to a
language output directory. Entries SHALL be normalized by converting `\` to
`/`, removing a leading `/`, and appending a trailing `/` when missing; an entry
that normalizes to the output root SHALL fail configuration validation. Prefix
matching and de-duplication SHALL use the same case rules as output destination
identity. The effective shared set SHALL be the configured or default prefixes
minus internal exclusion prefixes, if any, and every rule that refers to shared paths
SHALL apply to the effective shared set. A copied or generated asset output
SHALL be shared exactly when its path falls within the effective shared set,
regardless of its source. When `paths`
is not set, it SHALL default to the de-duplicated set of `assets/`, the SCSS
output directory, and the content media URL base without its leading slash,
omitting with a warning any derived entry that normalizes to the output root. A
configured list SHALL replace the default. Configuration validation SHALL fail
for an empty list, an absolute path, or a path containing `..`. Rendered
outputs, including pages and templated static files, SHALL never be shared;
when one falls within the effective shared set in any layout, the build SHALL fail with
`i18n-shared-asset-conflict` after routing and before that language writes any
file, naming the source and the matching shared path, and the published output SHALL remain
unchanged.

#### Scenario: Static under assets is shared
- **WHEN** sharing uses the default paths and `static/assets/logo.svg` exists
- **THEN** only `dist/assets/logo.svg` is written

#### Scenario: Other static files are not shared by default
- **WHEN** sharing uses the default paths and `static/fonts/a.woff2` exists
- **THEN** `dist/en/fonts/a.woff2` and `dist/zh-CN/fonts/a.woff2` are written and `dist/fonts/` is not

#### Scenario: Root prefix rejected
- **WHEN** `sharedAssets.paths` is `[/]`
- **THEN** configuration validation fails naming the entry

#### Scenario: SCSS output at the root
- **WHEN** `paths` is not set and the SCSS output directory is the output root
- **THEN** the SCSS output directory is not a default shared prefix and a warning is logged

#### Scenario: Templated static within the effective shared set
- **WHEN** sharing uses the default paths and a templated static file renders to `assets/config.js`
- **THEN** the build fails with `i18n-shared-asset-conflict` naming the static source and `assets/`, and the published output is unchanged

#### Scenario: Media URL base outside assets
- **WHEN** `content.media.urlBase` is `/media` and `paths` is not set
- **THEN** referenced media is written once under `dist/media/`

### Requirement: Pre-build shared assets are prepared before language builds
Theme and parent theme assets, SCSS output, theme tokens, and copied static
files whose output falls within the effective shared set SHALL be planned for every language,
checked for conflicts, and written once to the output root before any language
build writes files. Language asset plans SHALL exclude these outputs. Plugin
outputs produced during AfterBuild SHALL NOT be shared; when one falls within
the effective shared set, the build SHALL fail with
`i18n-shared-asset-conflict` before committing, naming the plugin and the
output path. Shared image variants produced by the built-in image processing
flow are not plugin outputs for this rule. Before
any language build starts, responsive image variants of shared source images
among these assets SHALL be generated once into the shared location, and the
resulting WebP mapping SHALL be made available to every language build.

#### Scenario: Plugin output within the shared set
- **WHEN** sharing uses the default paths and a plugin writes `assets/plugin-data.json` during AfterBuild
- **THEN** the build fails with `i18n-shared-asset-conflict` naming the plugin and `assets/plugin-data.json`, and the published output is unchanged

#### Scenario: Theme tokens shared
- **WHEN** sharing is enabled and the theme defines tokens
- **THEN** `dist/assets/css/theme-tokens.css` exists once and no language directory contains it

### Requirement: Shared media is written once by reference
Each language SHALL write the content media it references to the shared
location, skipping a destination already written from the same physical source
and failing with `i18n-shared-asset-conflict` when the source differs. Rewritten
media URLs SHALL use `site.asset_base_url` plus the media URL base. Each
language's media references SHALL cover all of its current HTML outputs,
including pages skipped by incremental builds. After all languages complete,
the root build layer SHALL record shared media from the union of complete
references and SHALL remove a shared media file only when no language's
complete references contain it.

#### Scenario: Media still referenced by another language
- **WHEN** an image is referenced by one English post and one Chinese post, and the English post is deleted
- **THEN** the image remains under `dist/assets/uploads/` after the next build

#### Scenario: Incremental skip keeps references
- **WHEN** a Chinese page referencing an image is skipped by an incremental build and the only English page referencing it is deleted
- **THEN** the image remains under `dist/assets/uploads/`

### Requirement: Language pipelines keep their stage order
Sharing assets SHALL NOT introduce a cross-language synchronization point
inside a language build. Each language's render, asset, AfterBuild, and report
stages SHALL run in their existing order, and per-language media validation,
projections, and audit reports SHALL observe the final HTML and the shared
files their language references, except where a worktree build defers a
shared media existence check to the parent.

#### Scenario: Media validation sees shared media
- **WHEN** sharing is enabled in an in-process build and a Chinese post references an image
- **THEN** the Chinese media validation passes because the image exists under `dist/assets/uploads/` before the Chinese report stage runs

### Requirement: Shared variant ownership is read from the root record
Every generation of shared image variants, whether pre-build, through the
in-process result cache, or in the worktree parent, SHALL read prior outputs
and freshness from the previous root plugin output record and SHALL write the
new ownership to the root plugin output record. Language manifests SHALL
neither record nor read shared variants.

#### Scenario: Second build with shared variants
- **WHEN** a site with shared theme and media images is built twice without changes
- **THEN** both builds succeed and the second build regenerates no shared variant

### Requirement: Shared assets have a single owner
Shared assets, shared image variants, and their freshness records SHALL be
recorded in the root planned outputs and root manifest, including a root plugin
output record, and excluded from every language's owned outputs. Stale shared
files SHALL be removed only by the root build layer, and language cleanup SHALL
NOT remove shared files. The root build layer SHALL NOT synchronize the output
root in a pruning mode.

#### Scenario: Incremental change in one language
- **WHEN** only a Chinese page changes between two builds with sharing enabled
- **THEN** no shared asset is rewritten or removed and every language directory remains

#### Scenario: Orphaned shared variant
- **WHEN** a shared source image is removed from the theme
- **THEN** its variants under `dist/assets/` are removed using the root plugin output record

### Requirement: Conflicting shared content fails the build
When the same shared output path resolves to different content in different
languages, the build SHALL fail with `i18n-shared-asset-conflict` before that
shared file is written, naming the path and the languages involved. Copied
outputs from the same physical source file SHALL be treated as identical
without hashing; generated outputs SHALL be compared by content hash.
Pre-build conflicts SHALL be reported before any shared file is written.

#### Scenario: Language-specific override
- **WHEN** sharing is enabled and the `zh-CN` plan produces `assets/font.css` with content different from the `en` plan
- **THEN** the build fails with `i18n-shared-asset-conflict` naming `assets/font.css`, `en`, and `zh-CN`, and no shared file is written

### Requirement: Templates reference shared assets through asset_base_url
Templates SHALL receive `site.asset_base_url`. When sharing is enabled it SHALL
equal the site root base URL without a language segment, preserving a non-root
base URL such as `/repo`. Otherwise it SHALL equal the current language
`base_url`. It SHALL use the same absolute or relative form as `base_url`.
Bukit SHALL NOT rewrite asset URLs in rendered HTML. The Starter theme SHALL
reference its assets through `site.asset_base_url`.

#### Scenario: Asset URL on a non-default language page
- **WHEN** sharing is enabled, `site.baseUrl` is `/repo`, and a `zh-CN` page renders `{{ site.asset_base_url }}/assets/style.css`
- **THEN** the rendered URL is `/repo/assets/style.css`

#### Scenario: Asset URL with sharing disabled
- **WHEN** sharing is disabled and a `zh-CN` page renders `{{ site.asset_base_url }}/assets/style.css`
- **THEN** the rendered URL is `/zh-CN/assets/style.css`

### Requirement: Responsive images work with shared sources
The responsive `srcset` transform SHALL remain part of the render-time HTML
transform pipeline and SHALL resolve shared media image URLs prefixed by
`site.asset_base_url` plus the media URL base, emitting `srcset` URLs with the
same prefix. Variant size planning and file naming SHALL be a single function
used by variant generation, the `srcset` transform, and worktree predicted
rewrites. Language builds SHALL NOT scan the effective shared set for theme
image variant generation and SHALL NOT run orphan or freshness cleanup within
the effective shared set. Shared media image variants SHALL be obtained through a result cache
keyed by source destination: the first requesting language generates them, and
every other language waits for and reuses the same result, including WebP
candidates. Image data and WebP mappings SHALL include shared variants from the
shared result rather than requiring them in the language's own generated
outputs. Any shared variant generation failure, or generation blocked by an
unowned file, SHALL fail the build regardless of `site.pluginFailMode`.
Variants of non-shared source images SHALL keep the existing per-language
behavior and failure policy. Each language's WebP picture rewrite SHALL use
its own mapping combined with the shared mapping, SHALL accept image URLs
prefixed by either the current language base URL or `site.asset_base_url`, and
SHALL be idempotent on HTML that it already rewrote.

#### Scenario: Shared theme image on two language pages
- **WHEN** sharing is enabled with WebP output, `site.baseUrl` is `/repo`, and an English page and a Chinese page both use `/repo/assets/hero.jpg`
- **THEN** each variant exists once under `dist/assets/` and both pages contain a WebP `<picture>` source whose URLs start with `/repo/assets/`

#### Scenario: Shared media srcset
- **WHEN** sharing is enabled and a Chinese page renders a media image with `site.baseUrl` `/repo`
- **THEN** the image `srcset` URLs start with `/repo/assets/uploads/`

#### Scenario: Concurrent languages reuse a shared media variant
- **WHEN** an in-process build renders English and Chinese pages in parallel that reference the same media image with WebP output
- **THEN** each variant is generated once and both pages contain the same complete WebP `<picture>` sources

#### Scenario: Shared variant failure in warn mode
- **WHEN** `site.pluginFailMode` is `warn` and generating a shared variant fails
- **THEN** the build fails and the published output is unchanged

#### Scenario: Rewrite of an incrementally skipped page
- **WHEN** a page whose `<picture>` was rewritten in the previous build is skipped by an incremental build
- **THEN** its HTML after the build is byte-identical to the previous build

### Requirement: Worktree builds produce shared assets in the parent
In worktree builds the parent SHALL prepare pre-build shared assets, shared
theme image variants, and the shared WebP mapping before dispatching workers,
and SHALL pass the mapping to every worker. Workers SHALL NOT write shared
paths, SHALL defer shared media existence checks to the parent, and SHALL
derive both WebP rewrites and image data for shared media variants from the
shared size planning function and source widths read from the shared media
cache, matching the content an in-process build obtains from the result cache.
Worker results SHALL carry complete media references as an optional field
restored as empty when absent. After all workers succeed, the parent SHALL copy
shared media, generate shared media variants, run the deferred existence
checks, and record ownership. The parent SHALL reject any worker result that
writes a shared path. Worktree and in-process builds of the same input SHALL
produce the same relative file paths and contents.

#### Scenario: Worker writes a shared path
- **WHEN** sharing is enabled and a worker result lists an output under `assets/`
- **THEN** the parent rejects aggregation and leaves the published output unchanged

#### Scenario: Missing shared media in a worktree build
- **WHEN** a worker reports a media reference whose cached source is missing
- **THEN** the parent fails the build during its deferred existence check and leaves the published output unchanged

### Requirement: Shared prefixes are reserved at the output root
When sharing is enabled and the default language is prefixed, the first path
segment of every configured shared prefix SHALL be a reserved root path. Root
aggregate outputs SHALL NOT be written under a reserved root path, and the
build SHALL fail if one is.

Regardless of layout, configuration validation SHALL fail when sharing is
enabled and a configured language code equals the first path segment of a
configured shared prefix, using the same check as the reserved language prefix
validation.

#### Scenario: Language code named like a shared prefix
- **WHEN** sharing uses the default paths and `languages` contains `assets`
- **THEN** configuration validation fails naming the language code and `assets/`

#### Scenario: Root aggregate output under a shared prefix
- **WHEN** sharing is enabled for a prefixed site and a root aggregate writer produces `assets/index.json`
- **THEN** the build fails naming the output and the reserved root path, and the published output is unchanged

### Requirement: Build statistics count shared assets once
When sharing is enabled, per-language asset and media counts SHALL include only
that language's non-shared files, shared assets and shared media SHALL be
counted once from the effective shared set at the output root, and site totals
SHALL equal the per-language counts plus the shared counts.

#### Scenario: Asset counts with two languages
- **WHEN** sharing is enabled, `languages` is `[en, zh-CN]`, and the theme has 10 asset files and no language-specific assets
- **THEN** each language reports 0 assets, the shared count is 10, and the site total is 10

### Requirement: Shared asset settings affect the incremental hash
The effective `sharedAssets` value SHALL contribute to the incremental site
hash, and a change SHALL clean the output directory through the protected clean
path before writing.

#### Scenario: Disabling sharing
- **WHEN** a site built with sharing enabled is rebuilt with `sharedAssets.enabled: false`
- **THEN** `dist/assets/` no longer exists and each language directory contains its own `assets/`
