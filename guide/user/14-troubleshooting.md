# 14 Troubleshooting

Use the smallest command that isolates the failure.

## Config Fails

```bash
bukit config check --config site.yaml
```

Common causes:

| Message Pattern | Meaning |
|---|---|
| `Unknown config field` | The YAML field is not in `ConfigStrictFieldValidator`. |
| `content.sources is required` | Bukit 1.0 does not use legacy top-level provider config. |
| `NOTION_TOKEN is required` | Notion provider validation needs the token in the environment. |
| `deploy.provider must be 'github-pages'` | Core has one deploy provider. |

## Build Fails Before Rendering

- Run `doctor` to inspect templates and providers.
- Check `site.collections` and `content.sources[].collection`.
- Check that route patterns include `{slug}` where required.
- Keep `build.output` inside a dedicated output directory.

### Multilingual Worktree Diagnostics

These stable codes apply when an explicit `bukit build` has `site.languages`:

| Code | Meaning |
|---|---|
| `i18n-worktree-git-required` | Git, a repository, or a resolvable `HEAD` is unavailable. |
| `i18n-worktree-dirty` | The repository has staged, unstaged, or untracked changes. |
| `i18n-worktree-input-untracked` | A required config/content/theme/static/plugin input is untracked, ignored, external, or crosses the repository through a symlink. |
| `i18n-worktree-worker-failed` | A language Worker exited unsuccessfully or the isolated build failed. |
| `i18n-worktree-result-invalid` | The Worker result schema, hash, language, completion state, or output path failed validation. |
| `i18n-worktree-cleanup-failed` | Bukit could not remove an owned temporary Worktree. |

Commit the intended build inputs and retry. Do not add `dist/`, `.cache/`, or
temporary generated files merely to make the repository clean; keep them in
`.gitignore`. A failed run does not publish its staging output. Bukit removes
only Worktrees registered to that run and attempts targeted recovery on the
next multilingual build.

### `BuildAssetOutputCollision`

Bukit found two output owners for the same destination, or a file/directory
prefix conflict. Check content/list routes, rendered static templates, theme
`static/`, theme `assets/`, localized media, and generated theme-token paths.
On a case-insensitive filesystem, paths that differ only by case also collide.
Rename one destination; changing build parallelism does not resolve ownership.

### Clean Refuses A Directory

Use a dedicated output directory such as `dist`. The clean operation refuses
project root, home/root directories, `.git`, paths outside the project,
symlinked targets, and non-empty directories without
`.bukit-output-marker`. Review or move existing files instead of manually
adding a marker to unrelated data.

## Route Conflicts

Route conflicts are usually caused by repeated slugs in the same collection,
manual route URL overrides, or list routes colliding with content routes. Adjust
the slug, collection permalink, or list route.

## Route Ends In A Static File Extension

A route URL or permalink that ends in a static file extension such as `.css`,
`.js`, `.pdf`, an image, font, archive, audio/video, or Office extension fails
the build (route URL with the invalid internal URL code, permalink with the
invalid permalink pattern code). Pages are rendered from templates, so these
files cannot be routes. Put them in the theme's `static/` directory (copied to
the same output path, e.g. `static/files/report.pdf` → `/files/report.pdf`) or
`assets/` directory (asset pipeline, served under `/assets/`). If the URL is a
page that merely contains a dot, such as `/docs/node.js`, end it with `/`.

## Template Fails

- Confirm the template path exists under the resolved layouts directory.
- Check layout directives are at the start of the file.
- Use `page`, `site`, `pages`, `items`, `pagination`, `collection`, `taxonomy`,
  and `filter` according to template type.

In `dev`, edits to template manifests, root templates, includes, and layout
targets are re-read for the next build. If old behavior remains, confirm that
the edited file is under the resolved theme and that the watcher observed it;
cache deletion should not be required for normal invalidation.

## Missing Files Behind A Symlink

Default recursive content, static, media, hash, and report discovery skips
directory symlinks and reparse points. Move publishable files into an ordinary
directory. `build.followSymlinks: true` applies only to documented supported
copy paths and is not a global scanner switch.

## Search Content Is Shorter Than Expected

Check `site.search.maxContentLength`. It limits only each record's `content`
field across document, list, plugin, and multilingual search outputs. The value
must be positive; the default is `8000` UTF-16 code units.

## Media Hosts Are Rate-Limiting Builds

Lower `content.media.maxConcurrency`. The setting applies separately to each
rewrite operation and each localized body store. An operation gate is shared
by its documents, HTML bodies, and media fields; a store gate is shared by its
concurrent reads. It is separate from `--jobs` and is not a process-wide limit.

## Build Report Counts Look Different From Audit Counts

This is expected. `.bukit/build-report.json` counts build log diagnostics only.
SEO, publish, and security reports use separate issue definitions. The build
report `generatedFiles` list contains public output only and intentionally
excludes `.bukit` reports and build state files.

## SCSS Process Diagnostics

For non-CI CLI builds, set `logging.level: debug` to see the SCSS tool start and
end events. CI CLI builds retain their existing Warn minimum;
library callers can collect Debug through their logger. Match the `attempt`
ID to correlate probe/compile events even when workers interleave. `tool` is
the logical `sass` or `dart-sass` candidate, not its physical resolved path.

`exit_code` is the actual returned runner result. `elapsed_ms` measures the
runner call, including wrapper/group setup, scheduling and cleanup, not Sass
CPU time. A timeout/cancellation/error without a returned result reports
`unavailable` for exit/stderr; it does not invent exit 0. A returned exit 0
alone does not prove CSS exists. Probe/compile limits remain 3/5 seconds.

New Debug events fully suppress stderr text: `stderr` is `empty`, `redacted`
or `unavailable`; `stderr_chars` is the returned diagnostic string's UTF-16
length (including any truncation marker), not raw stream bytes. They do not
log stdout, environment values, arguments, paths or exception messages.
These supplemental events are best-effort if a logger fails. Existing
compile-failure warnings are unchanged and are not covered by this new
masking guarantee. This observation does not establish the historical
missing-log failure's cause or guarantee that it is fixed.

## Slow Builds

Template directory fingerprints have a budget of 10,000 files and 100 MiB of
content per directory (child, parent, or user layouts). If either budget is
exceeded, Bukit warns and disables incremental reuse for the affected build
variant, then renders it fully. It does not reuse a partial path/length-only
fingerprint. This can make large-template builds slower, but avoids stale pages;
normal incremental reuse can resume on subsequent builds once the directories
fit the budgets. Ordinary read/permission failures are not treated as this
budget fallback. Use `--no-incremental` to request full rendering explicitly.

Use `--metrics` to inspect stage timing, `--jobs` to limit or increase render
parallelism, and `--incremental` to reuse unchanged render results. For an
explicit multilingual build, tune `build.languageJobs` separately to control
the number of language Worker processes; increasing it also multiplies the
per-Worker `--jobs` resource demand.
