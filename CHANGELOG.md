# Changelog

All notable changes to Bukit will be documented in this file.

## [Unreleased]

- **llms curation diagnostics now run**: invalid page-level `geo.llms` curation (an unknown key, a bad `visibility` or `tier`, a `priority` that is not an integer such as `"10"` or is outside -100 to 100, `llms: false`, or a non-empty `geo:` value that is not a mapping) now logs `geo.llms_field_unknown`, `geo.llms_visibility_invalid`, `geo.llms_tier_invalid`, `geo.llms_priority_invalid` or `geo.llms_shape_invalid` (a `geo` or `llms` value that is not a mapping), each with `route=<output path>`, during the build for every routed page (including noindex pages), and fails the build under `site.seo.diagnostics: strict`. Since the publish projection refactor these checks never ran, so such pages were dropped from `llms.txt` and `llms-full.txt` without a warning. Sites that use `strict` and have invalid curation will now fail to build; fix the front matter or use `visibility: exclude`. Valid curation and output are unchanged. An empty (or whitespace-only) `geo:` or `llms:` key is now treated as absent: such a page is included in `llms.txt` and `llms-full.txt` and no longer fails a `strict` build (before, it was excluded silently).

## [2.0.15] - 2026-11-11

- **Expired image links fail fast**: when an image download is refused (HTTP 400, 401 or 403) and the URL's own signature has expired (AWS SigV4, Google Cloud Storage V4, AWS SigV2/CloudFront, or the Notion file proxy), it is no longer retried `content.media.maxRetries` times, since an expired signature can never succeed. This mostly speeds up builds from a `readonly` Notion cache that still holds expired links. The failure, `onFailure` result and reuse of already saved images are unchanged, and the warning gains `signature=expired`. Other failures keep their retries.
- **Warning for signed links in shared data**: with incremental builds, Bukit now logs one `event=incremental.signed_links` warning when `site.modules`, `site.data`, `site.data_index`, `site.related_pages`, `site.data_files`, the route metadata source or page fields (key `pages`) still hold links of a recognized signing scheme (AWS SigV4, Google Cloud Storage V4, AWS SigV2/CloudFront, Notion file proxy). It names each read key and its link count, plus `language=` in multi-language builds. These links may change on every fetch, so the pages that read them may render again on every build; localize the files with `content.media` to avoid that. Hashing and output are unchanged.
- **`bodyFingerprint` is no longer shown for other pages**: in the `pages` list and in section `source` items, `fields` no longer holds the internal `bodyFingerprint` value. It identifies the body, which those lists do not track, so a page that printed it could keep stale text after another page's body edit. A page's own `page.fields` is unchanged.
- **Plugin name and option case no longer re-render the site**: plugin names and top-level option names are matched ignoring case, and the incremental hash now treats them the same way, so changing only their case no longer renders every page again.

## [2.0.14] - 2026-11-10

- **Notion bodies follow renderer changes and same-minute edits**: the Notion body cache (format 2) records the renderer version and the fetch time from Notion's `Date` header. In `readwrite` mode a cached body is reused only for the same renderer version and `last_edited_time`, with unexpired links, and only if it was fetched at least 5 seconds after the edit minute ended, so an edit made later in the same minute as a fetch is no longer lost. The incremental body fingerprint becomes `{last_edited_time}:renderer={version}[:unsettled]:content={on|off}`; pages without `last_edited_time` get their content hash from the body. The first `readwrite` build after upgrading fetches every Notion body again, which can take a long time on large databases. `readonly` still reuses matching entries and reports older ones in one `notion.cache.renderer_mismatch` warning; build with `--clean` when switching back to `readwrite`.

## [2.0.13] - 2026-11-10

- **Faster rendering on large sites**: each page used to convert the whole site page index (`pages`) and the site model (`site`, including `site.data.pages_by_id`) again, so render time grew with the square of the page count. They are now converted once per render pass and shared. Output is unchanged for templates that only read them. Behavior change: `site`, `pages`, list-page `items` and everything inside them are now read-only at every level, so a template that adds a new key to them or assigns an array element (such as `pages[0] = x`) fails with a read-only error; inside components and sections the error follows `theme.componentValidation`.
- **Render and report timing**: `--metrics` now records real `pageRender`, `listBuild` and `staticRender` times (they were always 0), plus `renderCall`, `htmlTransform`, `renderWrite`, `renderModelBind` and `renderDispatch`, and the pre-render variant steps `variantSetup`, `mediaResolve`, `renderPlan`, `assetPrepare` and `seoPrepare`. Each report step after rendering logs `event=build.report.timing` with its duration, and each scope ends with `step=total`. Report files and the `metrics.json` structure are unchanged.
- **Faster report stage on large sites**: checking whether each route appears in `sitemap.xml`, `search.json`, `agent-manifest.json`, `llms.txt` and the other aggregate files now scans each file once instead of once per route; the publish audit reads and parses `agent-manifest.json` once instead of once per document; and media output validation computes the effective media settings once instead of once per link. On a 4,651-page site these three steps took most of the build. Output, report files and audit issues are unchanged.
- **Re-signed images keep their file**: a localized image's file name no longer changes when only its signature changes. For URLs signed with AWS SigV4, Google Cloud Storage V4, AWS SigV2/CloudFront (`Signature` with `AWSAccessKeyId`, `GoogleAccessId` or `Key-Pair-Id`), or served by the Notion file proxy (`notion.so`, `*.notion.so`, `*.notion.site`), the signature and expiry parameters are left out of the file's identity; all other parameters still count, and the download still uses the full signed URL. Notion images are therefore downloaded once instead of again on every build after their links expire. When one link to an image has expired but another reference has a fresh signature, the fresh one is used. After upgrading, signed images are downloaded once more under their new stable name, except that a file an earlier version stored is still used for the exact signed URL it came from, so a `readonly` Notion cache with expired links keeps its local images; unsigned images keep their file names.
- **Incremental builds notice engine and plugin changes**: the template hash now includes the Bukit engine version and source revision, so the first `--no-clean` build after upgrading renders every page again (reported as `template_changed`, lists as `list_render`) instead of keeping pages rendered by the old engine. Options of enabled built-in plugins now count as render inputs, so changing one renders the site again; key order does not matter. Builds without git metadata or with uncommitted changes share their release's identity and need `--clean`.
- **Render reason `metadata_changed`**: pages re-rendered because their record or custom fields changed are now counted as `metadata_changed` in `renderReasons` of `.bukit/incremental-manifest.json`; `content_changed` now means the body alone changed, including a changed `bodyFingerprint` (for example a Markdown body edit). Editing a Notion page also updates its `last_edited_time` field, so Notion edits show as `metadata_changed`. Scripts that read `content_changed` should also read `metadata_changed`.
- **Editing one page no longer re-renders the whole site**: `--no-clean` builds now track shared data per page. Bukit records which collections each page's templates (layout, includes, components and sections) read: a `site.data`, `site.modules` or `site.data_index` key, `site.related_pages`, `site.data_files`, or `pages` (also read by sections with a `source`). Editing an article re-renders that article and the pages that read data it changed, such as `site.data.pages_by_id`, lists and pages that loop over `pages`, reported as the new render reason `data_dependency_changed`; site-wide setting changes are still `render_dependency_changed`. A template that uses `site` or `site.data` as a whole, an unknown global, `this` or a section plugin counts as reading everything, as does a custom renderer. Pages that loop over `pages` now also re-render when another page changes, where they used to keep stale HTML. `build-manifest.json` gains the optional entry fields `renderReads` and `renderBaseHash`; the first `--no-clean` build after upgrading renders every page.

- **Content Signals in robots.txt**: `site.seo.robotsTxt.contentSignal` (`search`, `aiInput`, `aiTrain`, each `true` or `false`) writes a `Content-Signal:` line in the `User-agent: *` group, for example `Content-Signal: search=yes, ai-input=yes, ai-train=no`, so a site can say how AI companies may use its content. Only set values are listed. Unset, `robots.txt` is unchanged.

## [2.0.12] - 2026-11-10

- **Percent-encoded URLs**: a `url` or `route.url` field, or an `aliases` entry, that was pasted already percent-encoded (such as `/%e8%81%94%e7%b3%bb%e6%96%b9%e5%bc%8f/`) is now decoded once (here to `/联系方式/`), so the output folder matches the address browsers request instead of returning 404. Escapes that would change the path (`/`, `\`, `.`, `%`), control characters, invisible or look-alike characters (zero-width, bidi overrides, NBSP) and invalid UTF-8 are left encoded. The build logs `event=route.url.decoded` or `event=route.alias.decoded` with the original value, so the source can be fixed. URLs without `%` are unchanged.

## [2.0.11] - 2026-11-09

- **Site-wide same-time order**: `content.ordering.tieBreakField` (`url`, `title`, or any content field such as `ID`) and `content.ordering.tieBreakOrder` (`asc` | `desc`) order entries with the same publish time in every list: the home page `pages`, collection lists and pagination, archives, site and collection feeds (RSS, Atom, JSON Feed, and the audit's feed window), and `llms.txt`. Term pages use it when `taxonomy.tieBreakField` is unset, so one site no longer shows the same posts in different orders on the home page and its category pages. `title` compares the document titles case-insensitively in every list, the same as the built-in term-page order: `title` `asc` written or unset gives the same term pages and incremental caches, and `title` `desc` is its exact reverse (it used to fall back to ascending). Whole numbers compare exactly, so IDs longer than 15 digits stay in order. Archive month pages list same-time posts in the collection-list order. A lone `taxonomy.tieBreakOrder` now applies to the inherited field (or title) instead of being ignored. Term RSS feeds now order same-time entries like their term pages (tie-break, then title, then URL) instead of by URL alone. Unset, other output and incremental caches are unchanged.
- **Empty RSS feeds**: a feed with no items no longer writes `lastBuildDate` as `Thu, 01 Jan 1970`; the optional element is left out. Atom keeps its required `<updated>`.
- **Taxonomy same-time order**: `taxonomy.tieBreakField` (`title`, `url`, or any content field such as `ID`) and `taxonomy.tieBreakOrder` (`asc` | `desc`) order term-page entries with the same publish time before pagination, so a migrated WordPress site can keep its old ID order across pages. Numbers compare by value; entries without the field come last; term RSS feeds use the same tie-break. Unset, or `title` `asc`, keeps the built-in title order, current output and incremental caches.
- **Large output no longer truncated**: templates no longer use Scriban's 1 MiB string limit, which inside `include` cut output near 524k characters and blanked the rest of the page. ItemList JSON-LD lists at most 100 entries (with `numberOfItems` when capped), so unpaginated list pages stay small. A page that opens `<html>` but never closes it is reported as `render.output_truncated` instead of `seo.document_title_missing`.
- **Breadcrumb home and taxonomy parent**: `site.seo.schema.breadcrumbHome` starts BreadcrumbList JSON-LD with the home page, and `site.seo.schema.breadcrumbParent` (a taxonomy kind such as `categories`) puts a content page's first term page before it, so flat URLs like `/2596/` get Home › category › post. An unknown taxonomy kind fails validation, and incremental builds re-render pages whose breadcrumb nodes change. Both are off by default; unset, output and incremental caches are unchanged.
- **Default SEO author**: `site.seo.defaultAuthor` and `site.seo.defaultAuthorType` (`Person` by default, or `Organization`) set the article JSON-LD author when a page has no author relation, no `author` field, and no GEO author. Unset, output and incremental caches are unchanged.

## [2.0.10] - 2026-11-09

- **Media localization controls**: `content.media.onFailure: placeholder | keep` (default `placeholder`) keeps the original URL when a download fails; `content.media.downloadHosts` and `skipHosts` limit downloads by host (`*.` wildcards). Responses typed `application/octet-stream` or without a type are identified from their file signature and must pass the existing image checks.
- **Large-site builds**: the render dependency hash allows up to 10,000,000 nodes; Notion summary prefetch logs `event=notion.progress`; a failed or cancelled build keeps the staged Notion cache (`event=build.cache_preserved`); Ctrl+C cancels the build cleanly, and a second Ctrl+C exits immediately; template runtime errors include the root cause.
- **Warning summary**: repeated warnings of one kind print five lines, then one `event=diagnostics.summary` line with the total.
- **Privacy scan**: the publish privacy check now runs in linear time instead of tokens × text.
- **Taxonomy**: `taxonomy.emptyTerms` controls empty term pages; percent-encoded terms are decoded; the tags index no longer includes category terms.
- **SEO diagnostics**: a stray quote in a head tag no longer hides `<title>`; pages that render no JSON-LD get one `seo.json_ld_not_rendered` instead of per-field schema issues; outside strict mode, title issues are reported once by the publish audit; title and description lengths use display width (CJK counts 2); derived descriptions are cut to width 160 with "…" (explicit `seo_desc` is kept); `bukit doctor` warns when theme-mode templates never output `seo.document_title` or `seo.json_ld`.
- **Analytics consent is optional**: GA and GTM providers no longer require `site.analytics.consent.google`. Without it, Bukit emits no consent default command and Google applies its own defaults (effectively all storage granted); configuring consent without a Google provider is still rejected. Existing configurations render unchanged.

## [2.0.8] - 2026-11-07

- **Multilingual media staging**: A custom `content.media.downloadDir` is now staged once and shared by all language workers. New downloads and `.media-index.json` commit with the output and cache only after every language succeeds. Media changes are detected by path, size, and modification time; symbolic links inside the media directory are rejected. This is a behavior change: a media directory that contains any symbolic link now fails a multilingual build.
- **Build interruption recovery**: The next build recovers the last complete transaction record for its output, including output, cache, staged media, and external metrics. Unknown pending files are preserved; a changed target or missing backup stops recovery and keeps the scene for inspection. Explicit multilingual builds now share the build-resource lease with in-process builds. Required input symlinks must be relative and remain inside the repository; absolute input symlinks are rejected.
- **Analytics audit fixes**: head-start fragments follow the page's encoding declaration, the CSP requirements report adds `connectSrc` and `imgSrc`, and GTM's `<noscript>` iframe is omitted when any Google consent default is denied.
- **SEO audit fixes**: root-relative content canonicals resolve against `site.url` + `baseUrl` without double-prefixing an existing base path, other relative forms are reported as `seo.canonical_relative`, and `og:locale` uses underscore separators such as `zh_CN` with a non-fatal `seo.og_locale_region_missing` diagnostic for bare languages, including under strict diagnostics.
- **AI crawler policy**: `robots.txt` groups AI crawlers by purpose (training, AI search, user fetch) and adds `aiBotMode: block-training` (it also blocks `Google-Extended`, which controls Gemini training and grounding; the build logs `event=robots.ai_grounding_blocked`). Behavior change: `selective` mode with `aiBotAllowList` now blocks every other known crawler, AI search crawlers included; the build warns with `event=robots.ai_search_blocked`. The build also warns with `event=robots.ai_policy_inactive` when an AI policy is configured but cannot be written.
- **Sitemap scope**: sitemaps list only canonicals under `site.url` + `baseUrl` (default ports and host case normalized), once each; the publish audit no longer expects pages whose canonical points elsewhere.
- **AI agent discovery**: with GEO enabled in `inject` mode, indexable content pages link their Markdown and JSON versions with `<link rel="alternate">`, and `llms.txt` links `agent-manifest.json` in its `## Optional` section. Toggling `site.seo.geo.enabled` now re-renders pages in incremental builds. In `theme` and `off` modes, layouts can read the same URLs from `page.representation_urls`, which is set only for published pages without `noindex`. `agent-manifest.json` is now written without a UTF-8 BOM.

## [2.0.7] - 2026-11-04

- **Version**: product version in `Directory.Build.props` set to 2.0.7 to match the release line (v2.0.6 was released with the file still at 2.0.4; release builds override it with `-p:VersionPrefix`).
- **Security regression self-test**: each selector in `scripts/security/security-regression.sh` must now match a real test declaration in a file carrying `[Fact]`/`[Theory]`; unparseable entries fail instead of being skipped; the self-test runs in the CI fast gate and handles checkout paths with spaces.
- **Release docs**: added 2.0.0 to 2.0.6 changelog sections and the 2.0 internal release checklist.

## [2.0.6] - 2026-11-03

- Integrated Core stages 6-10 (engine output and audit closeout, pipeline end-to-end tests, CLI and plugin coverage, architecture/coverage/three-platform gates), plugin error and secret-masking contracts, and test-gate audit fixes (PRs #66-#86).

### SEO/GEO Internal Integration

- Added the verified organization publisher contract (`type`, absolute HTTP(S)
  URL output, and `sameAs`), cross-source relation projection, relation-backed
  authors, empty-list indexability, company entities, and deterministic publish
  URL snapshot/change-set schemas.
- Added the isolated IndexNow process plugin, its minimal project example, and
  an `osx-arm64` internal Core/plugin install manifest with real SHA-256 values.
  This is an internal artifact, not a public release or release-gate result.

### Product Positioning

- **Internal-first operating mode**: Bukit Core adopts Route 2, a deterministic
  trusted-content publishing compiler, as its product direction and Route 3,
  an internal stable engine, as its current operating mode. The public
  repository and license remain available, but regular public binary releases,
  public support, compatibility guarantees, SLA commitments, and a fixed
  release cadence are paused.

### Audit Remediation

- Closed fourteen review findings: invalid Markdown front matter now fails closed instead of dropping draft metadata; deployment refuses to write through `CNAME`/`.nojekyll` symlinks; Notion upsert resolves the configured unique field and rejects missing, unsupported, or multi-page matches; WeChat stops retrying ambiguous draft submissions and preserves recovery state.
- Media localization now only rewrites image references, recursive `**/` globs include content-root files, seed arrays are emitted as YAML sequences, dev watching covers configured Markdown and inherited theme inputs, Notion caches refresh expired signed links and online relation targets, multilingual data preparation observes cancellation, and Markdown forwards auto-summary settings.
- Reused the shared canonical path resolver and removed duplicate/dead path and field-reader helpers. Added regression coverage plus a recovery guide for `DraftSubmitting`.

### Security And Reliability

- **Safe output cleanup (F-01)**: Explicit/configured clean, build cleanup, and recovery now share one guarded cleaner. Dangerous roots, `.git` descendants, outside-project and symlink/reparse targets, and non-empty unmarked directories are refused.
- **Default search UI DOM safety (F-02)**: Content title/snippet values are rendered with text nodes and explicit `<mark>` elements instead of an HTML interpretation sink; placeholder text is encoded.
- **Deterministic output ownership (F-03)**: Static, assets, media, generated tokens, and render destinations are preflighted before publication writes. Cross-category, structural, and case-variant conflicts on case-insensitive volumes fail with `BuildAssetOutputCollision`.
- **Safe recursive discovery (F-04)**: Default content, static, media, hash, and report inventory paths skip directory symlinks and reparse points. Explicit following remains limited to supported copy paths.
- **Fresh template decisions (F-05)**: Capability manifests use content fingerprints, static dependency analysis is call-scoped, and same-process rebuilds observe manifest/root/include/layout changes without mutable cached results leaking to callers.
- **Search content cap enforcement (F-06)**: Existing `site.search.maxContentLength` now applies to document, list, plugin, publish-projection, and multilingual search `content`; default/schema shape are unchanged.
- **Download-level media concurrency (F-07)**: Existing `content.media.maxConcurrency` now limits active localization downloads within rewrite-operation and localized-body-store scopes.
- **Truthful build health (F-08)**: Existing `build-report.v1` fields now contain current build diagnostic counts and a stable public `generatedFiles` inventory; the frozen schema shape is unchanged.

## [2.0.5]

- Version skipped: no `v2.0.5` tag or GitHub Release exists.

## [2.0.4] - 2026-09-13

- **Build transactions**: documented build transactions and protected staged output paths; fixed build-transaction analysis regressions.
- **Publish audit**: projection audits now track published URLs.
- **Native packaging**: native packages are slimmed and incomplete artifacts are rejected.

## [2.0.3] - 2026-09-12

- **Responsive images**: isolated responsive image source discovery, repaired the responsive image lifecycle, preserved empty image format lists, and refreshed incremental media output when image formats change.
- **Internal paths**: internal paths are protected from public output.
- **CI**: hardened CI gates and release evidence verification; clarified release tag SHA validation.

## [2.0.2] - 2026-09-12

- **Images**: added responsive image processing with an ImageSharp fallback; optimized image-processing regexes and size normalization.

## [2.0.1] - 2026-09-12

- **Output safety**: the public output lifecycle now fails closed; `404.html` static output path is preserved; relative plugin paths are enforced.
- **Preview**: Preview closes internal file serving and LAN exposure; internal path policy classifies by resolved physical target, closing in-root symlink alias bypass.
- **Process execution**: external tools on macOS run through a process-group wrapper that removes shell interpolation.
- **Content and SEO**: Markdown body projections preserve links; Notion caching and text parsing fixes; robots.txt audit rules are centralized; HTML routing and media discovery corrected; page titles included in Google Analytics config.
- **CI**: unique coverage results are retained and platform fixtures bounded; `bukit-test.slnx` now includes `Bukit.Content.Notion.Tests`.

## [2.0.0] - 2026-08-07

- **Public API**: the reviewed 2.0 public API surface is accepted and a 2.x stability policy is defined; plugin process and manifest record binary contracts are preserved.
- **SSRF and paths**: shared SSRF guard fails closed when host resolution is unsafe and retries public connection addresses; default path policy is published atomically; rooted absolute paths are normalized.
- **Process groups**: Linux uses `setsid` for process-tree management.
- **Engine**: simple page URL attributes are escaped; resolved output copy paths are honored.

## [2.0.0-RC.1] - 2026-07-24

- First 2.0 release candidate. Core is repositioned as an internal-first deterministic publishing compiler (see Product Positioning under Unreleased).

## [1.0.0] - 2026-06-09
### 1.0 Trust Hardening
- **Config strict mode**: `ConfigRemovedFieldScanner` rejects all pre-1.0 config fields (`content.markdown`, `content.provider`, `site.rssMode`, etc.) with migration hints.
- **Publish audit**: `MachineReadabilityTrustAuditBuilder` generates comprehensive SEO/GEO trust reports with multi-format coverage validation.
- **Import safety**: `ImportSafetyScanner` and `HtmlDemoImporter` share a unified `ImportSafetyPatterns` module for consistent sensitive-file detection.
- **Test migration**: All test fixtures migrated from `content.markdown` to `content.sources[].markdown` 1.0 schema.
- **Audit rule fix**: `PublishDocumentAuditScope.IsContentBacked` correctly excludes generated list routes from content-quality audit checks.
- **DevFileWatcher**: Tests now run in serial collection to avoid parallel-execution timing issues.
- **Schema alignment**: Root `site.yaml` updated to 1.0 schema (`content.sources[]` instead of `content.provider`).

### Preview

HTML Demo import (`bukit import html-demo`), import seed (`bukit import seed`), and Notion migration (`bukit notion push`) are Preview features and are **not** part of the Bukit 1.0 stable core contract. These commands may change without notice in future releases.

### Breaking
- **Content meta ABI removed**: `ContentItem.Meta` is no longer part of the runtime content ABI. Providers normalize front matter / Notion properties into typed `ContentField` values and canonical content records.
- **Plugin protocol routed pages now expose canonical content**: protocol routed page payloads include `fields` plus a typed `content` object with identity, lifecycle, provenance, trust, entities, relations, and media.

### Added
- **Document-first publishing foundation**: Added `RawContentDocument`, `ContentDocument`, and `RoutedContentDocument` as the vNext content pipeline types. Route, render, build context, publish projection, and report stages now carry document-first views alongside legacy item tuples during migration.
- **Raw provider ingestion contract**: Added `IRawContentProvider` and connected Markdown, Notion, and composite providers through raw document loading before normalization.
- **Content model schema validation**: Added `ContentModelSchema` and canonical validation rules for status, review/sync status, provenance, ownership, relation targets, media metadata, and entity IDs.
- **Machine-readable publish projections**: per-document JSON/Markdown representations and `agent-manifest.json` are generated from canonical content records.
- **Publish audit command path**: `bukit publish audit` and `bukit publish diff` are the primary audit commands; `seo audit` now prefers `.bukit/publish-audit-report.json` when present.

### Changed
- **Canonical graph enriched**: entities now support URL and `sameAs`; relations include target type/id; media extraction keeps alt/caption/description/license when provided.
- **Template context enriched**: page templates can read `page.content_model`, `page.content_record`, `page.entities`, `page.provenance`, `page.trust`, and `page.representations`.
- **SiteEngine refactored**: 856 → 592 line orchestrator with 8 independent pipeline classes (`BuildPipeline`, `ContentPipeline`, `RoutePipeline`, `RenderPipeline`, `AssetPipeline`, `SeoPipeline`, `PluginPipeline`, `BuildReportPipeline`), plus `ThemeBootstrapper`, `BuildOptionsMapper`, `FixedContentProviderFactory`. Dual `BuildAsync` paths unified into single pipeline chain. All reflection-based test helpers eliminated (zero `BindingFlags` remaining). Added performance regression tests.

### Added (Build Hardening)
- **Build core hardening**: 15-task TDD repair covering static HTML routing, theme inheritance fix, incremental stale cleanup, route/output path security, plugin hardening, safe output filesystem, remote theme reproducibility, and composite template fingerprinting
  - **Plugin environment isolation**: Process plugins now run in a clean environment with only `BUKIT_PLUGIN_NAME`, `BUKIT_PLUGIN_HOOK`, `BUKIT_PROJECT_ROOT`, and `BUKIT_OUTPUT_DIR` exposed by default; `allowEnvironment` config allows explicit host variable passthrough
  - **Plugin output limits**: `maxStdoutBytes` / `maxStderrBytes` fields in `externalPlugins` config cap plugin stdout/stderr; exceeding the limit kills the process
  - **Plugin output manifest tracking**: `build-manifest.json` now records structured `pluginOutputs` (plugin/hook/path/hash); stale plugin outputs from previous builds are automatically cleaned during incremental builds
  - **Build asset hash mode**: `build.assetHashMode: "sha256"` enables SHA256 content-based asset copy detection (recommended for CI and network filesystems)
  - **Route security validation**: `RouteSecurityValidator` rejects path traversal (`../`), absolute paths, cross-drive paths, and Windows reserved names (`CON`, `PRN`, etc.) in all generated routes and output paths
  - **Static HTML route inventory**: Static `.html` files are now included in route conflict detection alongside content pages and derived pages
  - **Safe output filesystem**: `IOutputFileSystem` / `SafeOutputFileSystem` API ensures all output write/delete operations stay within the build output root; stale file cleanup uses this guard
  - **Output clean marker**: `build.clean` now requires a `.bukit-output-marker` file (written on every successful build) before deleting the output directory; refuses to clean project root, home directory, filesystem root, or `.git` directories
  - **Remote theme reproducibility**: Cached remote themes no longer auto-`git pull` during build; `@ref` checkouts record the resolved commit in `bukit-theme.lock.json`; mismatched lock commits cause build failure
  - **Composite template fingerprint**: Incremental template hash now combines child/parent/user layouts, `theme.yaml`, and a renderer version marker — parent theme or user layout changes correctly trigger re-rendering
  - **Multilingual concurrency budget**: Multi-language builds now respect a global concurrency budget to prevent resource exhaustion
  - **Theme inheritance asset/static order**: Parent theme `assets/` and `static/` are now copied before child/project directories, ensuring correct override order
  - **Remote theme checkout hardening**: Missing version tags cause immediate build failure instead of silent fallback; Git operations use process tree kill on timeout
  - **Partial route override**: `route.outputPath`-only overrides now correctly derive the corresponding URL
  - Hierarchical taxonomy: `taxonomy.kinds[].hierarchical: true` enables parent-child term relationships via `ParentSlug`, with automatic `children` and `ancestors` computation
  - Term metadata: `_index.md` convention (Hugo-style) at `content/_taxonomy/<kind>/<slug>/_index.md` for per-term description, image, weight, parent
  - RSS 2.0 feeds: each term automatically generates `<output>/<kind>/<slug>/feed.xml`
  - Slug transliteration: Unicode NFD decomposition (`é→e`, `ß→ss`, `æ→ae`, `œ→oe`, `ø→o`), CJK characters preserved
  - Alias redirects: `Aliases` field generates HTML `<meta http-equiv="refresh">` redirect pages
  - Term visibility control: `IsVisible` and `Weight` fields for term ordering and filtering
  - `taxonomy.json` schema upgraded to v2 (includes `children` and `ancestors` arrays)
- **SlugHelper**: Shared slug generation utility in `Bukit.Shared`, consolidating 3 duplicate implementations with Latin transliteration support

### Changed
- **TaxonomyPlugin** refactored from 1194 lines to 245 lines — extracted 7 internal helpers: `TaxonomyIndexBuilder`, `TaxonomyPageCreator`, `TaxonomyDataWriter`, `TaxonomyTemplateResolver`, `TaxonomySortHelper`, `TaxonomyHierarchyBuilder`, `TaxonomyMetadataLoader`
- **TaxonomyTerm model** enriched with `Description`, `Image`, `Weight`, `IsVisible`, `ParentSlug`, `Aliases`, `Pages` fields
- `TaxonomyKindConfig` gains new `Hierarchical` boolean field (default `false`)

### Tests
- 5 new test files (38 test cases): `SlugHelperTests` (22), `TaxonomyHierarchyBuilderTests` (3), `TaxonomyMetadataLoaderTests` (6), `TaxonomyFeedWriterTests` (3), `TaxonomyRedirectWriterTests` (4)
- All 1311 tests passing (Shared 67, Engine 793, Content 451)

## [1.0.6] - 2026-05-21

### Added
- **Shortcodes system**: `theme.shortcodes` in site.yaml — define reusable snippets (`youtube`, `callout`, etc.) that work in both Markdown (`{% name args %}`) and Scriban templates (`{{ shortcode }}`)
- **Content schema validation**: `schema` in collection config — validate Front Matter fields by type (string/number/bool/date/list) with warn/strict failure modes
- **SCSS compilation pipeline**: `theme.scss` — automatic `.scss` → `.css` compilation during build using system `sass`/`dart-sass` CLI
- **Theme inheritance**: `theme.extends` in site.yaml — child themes cascade from parent; template lookup (child-first, parent-fallback), static/assets merging
- **Component-based templates**: `theme.components` — declare reusable components with props in site.yaml, use `{{ comp.render "Name" args }}` in Scriban templates
- **Image optimization pipeline**: `theme.images` — automatic WebP/AVIF conversion using system `cwebp`/`magick` CLI, with `ImageOptimizer.BuildSrcset()` for responsive images
- **HMR development server**: `bukit dev` command — file watching, incremental rebuild on change, WebSocket live reload to all connected browsers, debounced 300ms
- **Shared layout directive parser**: `ScribanLayoutDirectiveParser` extracted to `Bukit.Shared`, eliminating DRY violations between renderer and static analyzer
- **Async I/O in core pipeline**: `TemplateCapabilitiesResolver` now uses `File.ReadAllTextAsync` with `Task<T>` caching for non-blocking YAML manifest loading

### Changed
- **Refactored God Classes**: `SiteEngine` reduced from 1122 to 558 lines (extracted `SeoAlternatesService`, `RobotsTxtWriter`, `StaticFileService`); `PageRenderDispatcher` reduced from 581 to 491 lines (extracted `SpecialListRouteBuilder`)
- **ScribanTemplateRenderer** now accepts `shortcodes` and `components` dictionaries for runtime function injection
- **FileTemplateLoader** supports cascading lookup: primary directory first, optional fallback directory for theme inheritance
- **BuildVariantContext** extended with `ParentLayoutsDir`, `ParentAssetsDir`, `ParentStaticDir` for inherited themes
- **ConfigLoader** extended with new YAML deserialization helpers: `ReadComponents`, `ReadImageOptimizationConfig`, `ReadScssConfig`, `ReadSchema`

## [0.10.0] - 2026-05-05

### Added
- Initial release of Bukit, a .NET 10 Native AOT static site generator
- Markdown content source with Front Matter support
- Notion content source with database mapping and field normalization
- Multi-source content aggregation (`markdown` + `notion`)
- Scriban template engine with AOT-compatible vendored build
- Collections-driven routing system with permalink compatibility layer
- Built-in plugins: sitemap, RSS, search JSON, taxonomy, pagination, archive, pages-index
- Multi-language site support with split/merged/index modes
- Incremental build with manifest-based change detection
- Theme system with `theme list` / `theme use` commands
- Modules data source (`mode=data`) for structured content
- External plugin protocol v1/v2 (process and WASM runtimes)
- Plugin source generator for zero-reflection registration
- `doctor` diagnostic command for environment and configuration checks
- `webhook` command for Notion-to-GitHub Actions repository dispatch
- Intent system for AI-assisted site configuration
- AOT publishing with single-file output
- Performance metrics output (`--metrics`)
- Parallel rendering with configurable concurrency (`--jobs`)
- CI mode (`--ci`) with JSON structured logging
- Comprehensive user documentation (16 chapters, 3 languages)
- Developer documentation (35+ files covering architecture, CLI, plugins, etc.)
- ChatGPT prompt pack for conversational site building
- 322 unit tests across CLI, Content, Engine, and Rendering layers
- Smoke test scripts for Windows and Linux/macOS
- Documentation asset consistency check scripts
