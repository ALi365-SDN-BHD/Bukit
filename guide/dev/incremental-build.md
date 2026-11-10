# Incremental Build

Incremental state is stored under `.cache`. `VariantBuildPipeline` loads a build
manifest during setup when incremental mode is enabled.

## Inputs

Incremental rendering considers:

- Content hash.
- Metadata and route hash.
- Composite template hash: the renderer marker `scriban-renderer-v1`, the
  `Bukit.Engine` InformationalVersion (`<version>+<source revision>`), the
  child, parent and user layout directories, and each `theme.yaml`. A
  different engine build therefore re-renders every page, list and special
  list; the report shows `template_changed` for pages and `list_render` for
  lists. Builds without git metadata only carry the version, and uncommitted
  changes do not change the revision, so test such builds with `--clean`.
- Render dependency hash from config and template model inputs. Each enabled
  non-analytics built-in plugin contributes its name, its enabled flag and its
  `Options` (dictionaries in ordinal key order, so key order does not matter),
  so changing any option re-renders the site, including options that do not
  affect HTML. See "Per-Route Render Dependencies" for shared data.
- Static, asset, media, and plugin output manifests.

## Per-Route Render Dependencies

`RenderDependencyHasher.ComputeSnapshot` splits render dependencies into:

- the base hash: every `RenderDependencyContributorPlan` contributor and the SEO
  alternates. Route metadata and breadcrumb nodes are folded in per route, which
  gives the route base hash;
- one lazily computed fingerprint per shared collection, keyed in lower case:
  `site.data.<key>`, `site.modules.<key>`, `site.data_index.<key>` (including the
  route metadata source, which stays out of the base hash), `site.related_pages`,
  `site.data_files`, and `pages` (each
  render document's URL, output path, template and metadata hash in list order,
  plus all route metadata entries).

While a page, list or special list renders, the engine opens a
`RenderReadRecorder` (thread-static, around the synchronous render call). The
Scriban renderer reports every template the render used: the template and its
layout chain, includes (through `FileTemplateLoader.Load`), components and section
templates. `ScribanTemplateReadAnalyzer` maps each template's references to keys:
`site.data.menu` and `site.data["menu"]` give `site.data.menu`; a dynamic index or
the bare scope gives `site.data.*`; whole `site`, `this`, a parse error, or a
global that is neither a model root, an engine function, a Scriban built-in nor
declared by a used template (names inside `with` and after `import` included)
gives `*`. In list
renders the root `pages` is the list's items and is not recorded. A section with a
`source` records `pages`; a section plugin records `*`. A renderer that is not
Scriban never marks the recorder as tracked, so its routes read `*`.

The manifest stores the read keys (`renderReads`) and the route base hash
(`renderBaseHash`). A route's dependency hash is
`Combine(routeBaseHash, reads)`: the route base hash plus the fingerprints of the
collections read (absent keys hash as absent, so adding a collection changes the
hash). Before rendering, the dispatcher compares against the hash for the
previous reads; after rendering it stores the hash for the new reads. A missing or
malformed `renderReads` means `*`. Reads depend only on the render inputs, so
reusing the previous record is safe: if those inputs changed, the page renders for
that reason and records new reads. A changed hash is reported as
`render_dependency_changed` when the route base hash changed (or was not
recorded), otherwise as `data_dependency_changed`.

## CLI Controls

`build` supports `--incremental`, `--no-incremental`, and `--cache-dir`.
First builds and missing manifests render normally. Changing templates,
theme manifests, config-dependent render inputs, or routes invalidates affected
entries.

Asset/render ownership uses the same output-filesystem destination comparer as
`BuildManifestTracker`. Stale owner cleanup must preserve a current owner that
differs only by case on a case-insensitive volume, and must remove stale
file/directory structural blockers before the new owner writes.

Template capability decisions are not keyed only by timestamp or file length.
`bukit.templates.yaml` uses a content fingerprint, while root/include/layout
static analysis is scoped to one analysis call. A same-process rebuild observes
changed, created, deleted, or corrected inputs on the next decision.
