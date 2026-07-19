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

### `BuildAssetOutputCollision`

Bukit found two output owners for the same destination, or a file/directory
prefix conflict. Check content/list routes, rendered static templates, theme
`static/`, theme `assets/`, localized media, and generated theme-token paths.
On a case-insensitive filesystem, paths that differ only by case also collide.
Rename one destination; changing build parallelism does not resolve ownership.

### Clean Refuses A Directory

Use a dedicated output directory such as `dist`. Bukit intentionally refuses
project root, home/root directories, `.git`, paths outside the project,
symlinked targets, and non-empty directories without
`.bukit-output-marker`. Review or move existing files instead of manually
adding a marker to unrelated data.

## Route Conflicts

Route conflicts are usually caused by repeated slugs in the same collection,
manual route URL overrides, or list routes colliding with content routes. Adjust
the slug, collection permalink, or list route.

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

## Slow Builds

Use `--metrics` to inspect stage timing, `--jobs` to limit or increase render
parallelism, and `--incremental` to reuse unchanged render results.
