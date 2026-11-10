---
name: bukit-build
description: Use when running or tuning `bukit build` — clean vs no-clean, drafts, incremental rendering and `.cache`, `--jobs`, `--metrics`, build transactions and rollback, `.bukit/` reports, the security gate, or multilingual worktree builds.
---

# Bukit Build

`bukit build` turns `site.yaml`, content, and the theme into a static output
directory (default `dist`). Every build is transactional: it writes into a staging
directory and replaces the published output only when everything succeeded.

## Pipeline

1. Begin a transaction and recover any interrupted earlier one.
2. Plan: apply CLI overrides, validate config, resolve and validate the theme,
   prepare the output directory.
3. Content: load sources, filter drafts, localize media, validate the content graph.
4. Assets: compile SCSS and process images.
5. Per language variant: routes, derived pages (taxonomy, pagination, aliases...),
   SEO model, parallel render, HTML transforms (SEO inject, analytics, images),
   static/asset sync, machine-readable projections, audit reports.
6. Metrics, output inventory, build reports, security gate.
7. Save the incremental manifest, write `.bukit-output-marker`, mark
   `.bukit-build-state.json` completed, commit. Logs `Build completed: <dir>`.

A failure before commit leaves the previous output and build caches unchanged
(writable Notion caches are kept so the next run does not refetch). Fix the cause
and rebuild; never delete the last good output as a "fix".

## Clean vs no-clean

| Mode | Behavior |
|---|---|
| `build.clean: true` (default) / `--clean` | Staging starts empty; the output is fully regenerated. |
| `build.clean: false` / `--no-clean` | Staging starts from the current output. Only files Bukit previously generated are removed; untracked user files stay. |

An earlier interrupted build (`status` not `completed`) triggers one automatic protected
clean (`event=build.recovery ... action=autoClean`).

## Incremental rendering

Incremental is on by default (`--no-incremental` turns it off). A page is reused only
when its output file already exists and its template, metadata, route,
render-dependency, and content hashes all match the manifest in
`<cacheDir>/build-manifest.json` (`build-manifest.<lang>.json` per language).

Because the default clean build starts from an empty staging directory, every page
reports `new_page` (no manifest entry yet) or `output_missing` and is re-rendered.
Reuse happens only with `--no-clean` (or `build.clean: false`) and in `bukit dev`
rebuilds.

```bash
bukit build --no-clean --metrics .bukit/metrics.json
```

Site-wide inputs (site identity, SEO config, taxonomy, analytics, theme/template model,
image processing, collections, plugin enablement) invalidate the affected pages
automatically. Do not tell users to delete `.cache` to see template changes. To reset
everything use `bukit clean --config site.yaml` or `bukit build --no-incremental`.

Known gaps: `--no-clean` reuse does not notice (a) a Bukit version upgrade and (b)
changes in other documents that a page's template reads (page lists, cross-links,
related blocks). Such pages keep their old HTML. Use `--no-clean` for local speed only;
run the default clean build after upgrading Bukit and for every release or deploy.

`.cache/` holds `build-manifest*.json`, `media/` (localized media unless
`content.media.downloadDir` is set), and `notion/` (only when a Notion source sets
`cacheMode: readonly|readwrite`; `notion.cacheDir` relocates it). `--cache-dir`
relocates `.cache/` for `bukit build`; it is relative to the current directory, and
`bukit dev` and `bukit clean` still use `<root>/.cache`. Keep `dist/`, `.cache/`, and
the project `.bukit/` (metrics files) out of Git.

Unless it refuses the output, `bukit clean` deletes `<root>/.cache` without checking
what is in it. Run it only
for a Bukit project root, never where `.cache` is shared with other tools.

## Performance options

- `--jobs <n>`: render parallelism, default CPU count, clamped to 1..2×CPU.
- `--metrics <file.json>`: stage timings and render reasons, plus a sibling `.html`.
  The path is relative to the project root, so `--metrics .bukit/metrics.json` writes
  to `<root>/.bukit/`, not into `dist`. Render stages to compare: `pageRender`,
  `listBuild`, `staticRender` (per kind), `renderCall` (template), `htmlTransform`,
  `renderWrite`, `renderModelBind`, `renderDispatch`. Stage times are summed across
  `--jobs` workers, so they can exceed wall-clock time.
- Slow after rendering: each report step logs an info line
  `event=build.report.timing scope=variant|finalize step=<name> duration_ms=<n>`
  (`scope=variant` adds `language=<code>` in multilingual builds), and each scope ends with
  `step=total`. `--ci` hides info lines.
- `content.media.maxConcurrency` limits concurrent media downloads; it is independent
  of `--jobs`.

## Drafts

Front matter `draft: true` documents are dropped (`event=content.draft_filtered
removed=N`) unless `--draft` is passed or `build.draft: true` is set. `bukit dev` has no `--draft`
flag and shows drafts only when `build.draft: true`.

## Output files

Output root (internal, never served or deployed):

- `.bukit-output-marker`: proves the directory is Bukit output; required for safe clean.
- `.bukit-build-state.json`: `{"version":1,"status":"started|completed",...}`;
  deploy requires `completed`.

`<output>/.bukit/` reports:

| File | Written |
|---|---|
| `security-report.json` | Always. |
| `seo-report.json`, `publish-audit-report.json`, `geo-report.json`, `seo-route-map.json` | Always (read by audits and insights). |
| `release-bundle-checksums.json`, `artifact-manifest.json`, `build-manifest-digest.json` | Always. |
| `build-report.json`, `routes.json`, `assets.json`, `incremental-manifest.json`, `publish-url-snapshot.json`, `analytics-report.json` | When `build.report.enabled: true` (default). |

`build-report.json` counts build log events; SEO and publish issue totals live in their
own reports. Its `generatedFiles` lists public output only.

## Security gate

`build.report.securityFailMode`: `auto` (default), `off`, `warn`, `strict`. `auto`
becomes strict under `--ci` or a release environment (`BUKIT_BUILD_MODE`,
`BUKIT_PROFILE`, `BUKIT_RELEASE`; `BUKIT_RELEASE=0` or `false` does not count). In
strict mode failed checks in
`security-report.json` stop the build with `BKT-BUILD-SECURITY-0001` (exit 1).

## Transactions and recovery

- Each writable target gets sibling `.bukit-txn-<id>-stage` / `-backup` directories.
- Concurrent builds that share an output, cache, or metrics path fail fast.
- `Rollback failed; preserve backup '<path>'`: keep that backup for manual recovery.
- `Build committed; backup cleanup failed`: the build succeeded; only remove the
  leftover backup after checking it.
- Ctrl+C cancels and rolls back; a second Ctrl+C exits immediately.

## Multilingual builds

With two or more `site.languages`, `bukit build` builds each language in a Git worktree
worker. Inputs must be committed: errors `i18n-worktree-git-required`, `-dirty`, or
`-input-untracked` mean commit or ignore the files. Any untracked file under the site
root counts, including `--metrics .bukit/metrics.json` output and a generated
`site.schema.json`, so git-ignore the project `.bukit/` or write those files outside
the site root. `build.languageJobs` sets worker
count (default 1). `dev` and `deploy` build in-process without this requirement.
See `bukit-i18n`.

## Output directory safety

The output must be a dedicated directory. Bukit refuses (`BKT-0601`) the project root,
home, paths outside the project, `.git` paths, and symlinked segments, and refuses
(`BKT-0602`) to clean a non-empty directory without `.bukit-output-marker`. Content
sources may not overlap the output or `.cache`.

## Verify

```bash
bukit build --metrics .bukit/metrics.json
ls dist/.bukit/
```
