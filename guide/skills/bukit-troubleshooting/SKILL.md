---
name: bukit-troubleshooting
description: Use when a Bukit command fails or the site output looks wrong — reading error codes and structured log events, a symptom-to-fix table for config, content, routing, template, build, clean, server, audit, and deploy problems, and a minimal diagnosis loop.
---

# Bukit Troubleshooting

## Diagnosis loop

Run the smallest command that fails, fix one thing, repeat:

```bash
bukit config check          # config shape and values
bukit doctor                # theme, templates, content, routes, Notion
bukit build                 # full pipeline
bukit seo audit --dir dist  # output quality
```

Add `logging.level: debug` in `site.yaml` for more detail (`--ci` forces `warn`). Use
`bukit build --log-format json` for machine-readable log lines (the option goes after
the command; error objects are multi-line JSON, see `bukit-cli-reference`). Read the first error, not the last.

## Reading errors

- `build` prints `Error: <message>` on stderr with no code; exit codes are listed in
  `bukit-cli-reference` (2 usage/config/content, 3 render, 1 command-level failure).
- Only `doctor` and `clean` prefix messages with `[BKT-XXXX]`. In `--log-format json`
  output the `code` field is the diagnostic name (e.g. `ContentCollectionMissing`,
  `RouteConflict`), not the number. Match the message text first.
- BKT number ranges: `0001`–`0007` config, `01xx` theme, `02xx` routes, `03xx`
  templates, `04xx` schema, `05xx` content, `06xx` build output, `07xx` plugins,
  `08xx` SEO/GEO, `09xx` media.
- Build log events have the form `event=<name> key=value ...`. More than five warnings
  of one kind are collapsed into `event=diagnostics.summary`.

## Symptoms

| Symptom | Cause | Fix |
|---|---|---|
| `Unknown config field '<path>'` | Strict keys, camelCase, case-sensitive | Rename or remove the key (`bukit config schema` lists valid keys). |
| `content.sources is required` | No source | Add a `markdown` source. |
| `no content source has a 'collection' field, but site.collections defines ...` | Collections need source assignment | Add `collection:` to each content source (`bukit-content`). |
| `missing required collection` (`ContentCollectionMissing`) | Content without collection | Set source `collection` or front matter `collection`. |
| `No route rule matches content document` | No collection rule or type permalink (or a typo in `collection`) | Add `site.collections.<c>` or `site.permalinks.<type>` (`bukit-routing`). |
| `Route conflict on url\|outputPath` (`RouteConflict`) | Two routes share a URL or file | Change slug/permalink; check `url: /`, aliases, taxonomy prefixes. |
| `No theme template matches kind='taxonomy_index'` / `Taxonomy template not found` | Front matter `tags`/`categories` auto-creates taxonomy pages | Add taxonomy templates or set `taxonomy.outputMode: data` (`bukit-routing`). |
| Pages missing from output | `site.language` default `zh-CN` filters other languages; `draft: true`; Notion `Published` filter | Set `site.language`; use `--draft`; adjust `filterType`. |
| `content/index.md` shows at `/index/` | Home `/` is always the home list | Put home content in the home template or data. |
| HTML in Markdown shows as text | Raw HTML is escaped | Move HTML to templates or shortcodes. |
| Posts unordered / dates 1970 | Only `publishAt` is the date | Rename `date:` to `publishAt:`. |
| `{% for %}` printed literally | Liquid syntax | Use Scriban `{{ for x in y }}...{{ end }}` (`bukit-templating`). |
| `Template path is invalid:` with an empty path (exit 3) | A page has front matter `aliases` (Core defect in 2.0.10/2.0.11) | Remove `aliases`; redirect at the host or CDN (`bukit-outputs`). |
| `Exceeding number of iteration limit 1000` | Scriban loop limit | Use `limit:` or pagination. |
| `Template not found` / `Render failed` (exit 3) | Wrong path or template bug | Check lookup order: site `layouts/`, theme, parent. |
| `event=canonical.validation code=canonical_media_alt_missing` / `Canonical content validation failed with N error(s).` | `image` or `cover` without alt text (`requireMediaAlt` default); fails under `schemaFailMode: strict` | Add `image_alt` / `cover_alt` to the front matter (`bukit-content`, `bukit-content-model`). |
| No CSS from SCSS | Dart Sass missing (`event=scss.skip reason=sass_cli_not_found`) | Install `sass` on PATH. |
| Theme CSS or tokens not applied | Never auto-linked | Add `<link>` tags in the layout. |
| No sitemap, feed, or robots.txt | `site.url` missing; robots off by default | Set `site.url`; `site.seo.robotsTxt.enabled: true`. |
| `event=robots.content_signal_inactive` / `robots.ai_policy_inactive` | robots.txt not generated or static file wins | Enable robots, set `site.url`, or edit the static file. |
| `event=route.url.decoded` / `route.alias.decoded` | Percent-encoded URL in front matter | Write the decoded Unicode path in the source. |
| `[BKT-0602] ... does not contain .bukit-output-marker` | Output dir holds non-Bukit files | Use an empty dedicated `build.output`; never fake a marker. |
| `[BKT-0601] Refusing to clean unsafe output directory` | Output is root, home, outside project, `.git`, or symlink | Use `dist` or `public`. |
| `Asset output collision at '<path>'` (exit 1) | Two sources write one path, or case-only clash | Rename one file. |
| `BKT-BUILD-SECURITY-0001 ... (mode=strict)` | Failed security checks under `--ci` | Read `dist/.bukit/security-report.json`. |
| `Multilingual build requires a clean site directory ...` / `... inside a Git repository` (JSON codes `i18n-worktree-*`, exit 1) | Multilingual build needs committed inputs in a Git repo; any untracked file under the site root counts | Commit inputs; ignore `dist/`, `.cache/`, and the project `.bukit/` (metrics files). |
| Build failed mid-way | Transaction rolled back | Previous output is intact; fix and rebuild. Keep any backup named in `Rollback failed; preserve backup`. |
| Every build re-renders all pages | Default `build.clean: true` | `bukit build --no-clean` for incremental reuse, for local speed only; it can keep stale pages after a Bukit upgrade or when other pages a template lists change (`bukit-build`). |
| Media downloads fail or rate-limit | `media.download_failed`, `media.ssrf_blocked` events | Lower `content.media.maxConcurrency`; check `skipHosts`; private hosts are blocked (`bukit-content-model`). |
| Remote image not localized | `media.download_too_large limit=...`, `media.download_empty`, `media.failed_reference` | Raise `content.media.maxFileSizeBytes` or fix the source URL (`bukit-content-model`). |
| No `-<width>w` / `.webp` / AVIF image variants | `image_resize.error`, `image_webp.error`, `image_optimize.error`, `image_processing.skip reason=invalid_source`; `image_optimize.skip reason=no_tool` (AVIF needs ImageMagick `magick`); `*.skip reason=unowned_existing_output` (a file with the variant's name already exists) | Fix or replace the source image, install the tool, or rename the clashing file (`bukit-theme`). |
| SCSS not compiled, Sass installed | `event=scss.error file=... reason=...` | Fix the Sass error in `detail`. |
| `event=taxonomy.term.decoded` | Percent-encoded tag or category value | Write the decoded text in the source (`hint=fix_option_in_notion`: fix the Notion option). |
| `event=notion.relation.resolve_failed` / `notion.cache.read_failed` | A Notion relation target or cached page could not be read | Read the `message`; check the integration's access to the related database. |
| `NOTION_TOKEN is required ...` | Env var missing | Export `NOTION_TOKEN` (`bukit-content-model`). |
| `SEO report not found under ... Run a full build first.` | Audit `--dir` defaults to `dist` | Pass `--dir <build.output>`. |
| Links 404 in `preview`/`dev` | Servers ignore `baseUrl` | `bukit build --base-url /` then `bukit preview`; for dev use `BUKIT_SITE__BASE_URL=/ bukit dev`. |
| `404.html` or static `robots.txt` not copied | Named theme ignores project-root `static/` | Move it to `themes/<name>/static/` (`bukit-outputs`). |
| `dev` ignores config or `data/` edits | `site.yaml` is read once; `data/` is not watched | Restart `bukit dev`. |
| `refused to bind a non-loopback host` | LAN exposure needs consent | Add `--allow-lan`. |
| `Build output is not ready for deployment` | No completed build | Run a successful `bukit build` first. |
| `GITHUB_TOKEN environment variable is required` | Token var unset | Set it or use `--token-env NAME`. |
| `Non-fast-forward push rejected` | Target branch diverged | `--force` only if overwriting is intended. |
| Page missing from llms.txt | Invalid `geo.llms` curation | Look for build warnings `geo.llms_field_unknown`, `geo.llms_visibility_invalid`, `geo.llms_tier_invalid`, `geo.llms_priority_invalid` (they fail the build under `strict`; Bukit 2.0.11 and earlier do not log them) and the publish audit warning `publish.llms_missing_route route=<url>`; check that `geo.llms` uses only `visibility`, `tier`, and an integer `priority` (`bukit-geo`). |

## Reports to inspect

`dist/.bukit/`: `build-report.json` (counts, generated files), `routes.json`,
`incremental-manifest.json` (render reasons), `seo-report.json`,
`publish-audit-report.json`, `geo-report.json`, `security-report.json`,
`analytics-report.json`. Metrics: `bukit build --metrics .bukit/metrics.json`.

## Recovery rules

- Do not delete the last good output or `.cache` as a first fix.
- `bukit clean --config site.yaml` resets the configured output and `.cache`; it never
  touches the project `.bukit/`. The safety checks cover the output path only:
  unless clean refuses, `<root>/.cache` is deleted without inspection, so run it only
  for a Bukit project root.
- Never create `.bukit-output-marker` by hand.
