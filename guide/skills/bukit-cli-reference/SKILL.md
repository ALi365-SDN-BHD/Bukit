---
name: bukit-cli-reference
description: Use when you need the exact Bukit Core command, subcommand, option, default, exit code, or environment variable — before writing any `bukit ...` command line, CI step, or script.
---

# Bukit CLI Reference

Bukit Core registers exactly twelve static commands. Any other command name (for
example `init`, `lint`, or a `--version` flag) is not a Core command and exits 2; use
`version` for the version. The one exception is external-plugin tooling (Labs, not
covered here): `plugin`, and commands exposed by plugins in `.bukit/plugins.yaml`.

## Command map

| Command | Subcommands | Purpose |
|---|---|---|
| `build` | — | Build the site into the output directory. |
| `doctor` | — | Run project health checks (config, theme, templates, content, routes, Notion). |
| `config` | `config check`, `config schema` | Validate `site.yaml`; emit its JSON Schema. |
| `preview` | — | Serve an existing output directory. No build, no watch. |
| `dev` | — | Build, serve, watch, and LiveReload. |
| `clean` | — | Delete a Bukit output directory and the project `.cache`. |
| `version` | — | Print version and runtime. |
| `completion` | — | Print a shell completion script. |
| `seo` | `seo audit`, `seo diff`, `seo insights`, `seo question-insights`, `seo generative-insights`, `seo authority-insights` | SEO report gate, regression diff, observation joins. |
| `geo` | `geo audit` | Print the GEO report and score. |
| `publish` | `publish audit`, `publish diff` | Release-readiness gate and diff. |
| `deploy` | — | Build (unless told not to) and push to GitHub Pages. |

`bukit`, `bukit help`, `--help`, `-h` list commands; `bukit <command> --help` prints
that command's usage. Both `--opt value` and `--opt=value` work; a value that starts
with `-` (for example `--message=-wip`) must use the `=` form, otherwise it is
`Missing value for <opt>` (exit 2).

Positional words are not options. Only subcommand names, `completion <shell>`, and
`seo|publish diff [<baseline> <current>]` read them. Any other extra word is silently
ignored (`bukit build site.yaml` still builds `./site.yaml`), so always pass
`--config`, `--site`, or `--dir` explicitly. The insights commands reject extra words
(`usage_invalid`, exit 2).

## Options by command

Project selection, shared by most commands:

- `--config <path>`: config file, default `./site.yaml`. The project root is the
  config file's directory (for `<root>/sites/<name>/site.yaml` the root is `<root>`).
- `--site <name>`: use `./sites/<name>.yaml`; root is the current directory.
  `--config` wins when both are given. Paths escaping `sites/` are rejected.
- `--site-url <url>`: override `site.url` for this run.
- `--notion-snapshot <dir>` (`build`): record a Notion offline snapshot outside the site; add
  `--notion-offline` (`build`, `doctor`, `config check`) to read only that snapshot, with no
  `NOTION_TOKEN` and no network.

| Command | Options |
|---|---|
| `build` | `--config`, `--site`, `--output`, `--base-url`, `--site-url`, `--clean`, `--no-clean`, `--draft`, `--ci`, `--incremental`, `--no-incremental`, `--cache-dir`, `--metrics`, `--jobs`, `--log-format`, `--notion-snapshot`, `--notion-offline` |
| `doctor` | `--config`, `--site`, `--site-url`, `--notion-snapshot`, `--notion-offline` |
| `config check` | `--config`, `--site`, `--site-url`, `--notion-snapshot`, `--notion-offline` |
| `config schema` | `--output` (file; stdout when omitted) |
| `preview` | `--dir`, `--host`, `--port`, `--strict-port`, `--config`, `--site`, `--allow-lan`, `--public` |
| `dev` | `--config`, `--site`, `--host`, `--port`, `--output`, `--no-watch`, `--allow-lan`, `--public` |
| `clean` | `--dir`, `--config`, `--site` |
| `version` | none |
| `completion` | `<shell>`: `bash` (default), `zsh`, `fish` |
| `seo audit`, `publish audit` | `--dir`, `--report`, `--strict`, `--external` |
| `seo diff`, `publish diff` | `--baseline`, `--current`, `--max-new-errors`, `--max-new-warnings`, `--max-new-issues`, `--fail-on-new-code`, `--fail-on-route-removed`, `--fail-on-indexable-drop` |
| `seo insights` | `--dir`, `--routes`, `--observations`, `--rules`, `--out`, `--strict-join` |
| `seo question-insights` | `--dir`, `--routes` (alias `--route-map`), `--targets`, `--observations`, `--rules`, `--out`, `--strict-join` |
| `seo generative-insights`, `seo authority-insights` | `--dir`, `--routes` (alias `--route-map`), `--observations`, `--rules`, `--out`, `--strict-join` |
| `geo audit` | `--dir` |
| `deploy` | `--config`, `--site`, `--dry-run`, `--skip-build`, `--base-url`, `--site-url`, `--output`, `--branch`, `--repo`, `--token-env`, `--message`, `--ci`, `--force` |

Bare `bukit seo`, `bukit publish`, `bukit geo`, and `bukit config` print usage and exit 2 (bare
`bukit config` first prints `Unknown config command: default`).

### `build` option semantics

| Option | Effect |
|---|---|
| `--output <dir>` | Override `build.output` (default `dist`). Relative to the project root, not the current directory; must stay inside the project and not overlap `.cache` or `--cache-dir` (exit 2). |
| `--base-url <path>` | Override `site.baseUrl` (default `/`). |
| `--clean` / `--no-clean` | Override `build.clean` (default `true`). Mutually exclusive. |
| `--draft` | Force `build.draft: true` (keep `draft: true` documents). Without it `build.draft` decides (default `false`, drafts removed); no flag turns off `build.draft: true`. |
| `--ci` | Log level `warn`; forces `build.followSymlinks: false`; turns `build.report.securityFailMode: auto` into strict. |
| `--incremental` / `--no-incremental` | Incremental rendering is on by default. Mutually exclusive. |
| `--cache-dir <dir>` | Replace `<root>/.cache` for the build manifest and media cache. Relative to the current directory; must not overlap the output (exit 2); the Notion cache stays at `<root>/.cache/notion`; not used by `dev`, not removed by `clean`. |
| `--metrics <file.json>` | Write stage metrics JSON plus a sibling `.html`, relative to the project root. |
| `--jobs <n>` | Render parallelism, integer ≥ 1 (default CPU count; values above 2×CPU are clamped). `0` or a non-integer exits 2. |
| `--log-format text\|json` | Must come after the command name (`bukit build --log-format json`); before it, it is read as the command and exits 2. Only `build` validates the value; other commands treat unknown values as `text`. See "Logs and JSON errors". |

Details: `bukit-build`. Servers: `bukit-local-server`. Audits and deploy:
`bukit-seo`, `bukit-geo`, `bukit-publish-deploy`, `bukit-seo-insights`.

### Other options at a glance

| Command | Option | Default | Meaning | Details |
|---|---|---|---|---|
| `preview` | `--dir <dir>` | `build.output` with `--config`/`--site`, else `./dist` | Directory to serve; relative to the current directory and wins over `--config`/`--site` (which then only select the analytics policy). | `bukit-local-server` |
| `preview`, `dev` | `--host <host>` | `localhost` | Bind address; non-loopback needs `--allow-lan`. | `bukit-local-server` |
| `preview` | `--port <n\|auto>` | `4173` | Port; `auto` picks a free one. | `bukit-local-server` |
| `dev` | `--port <n>` | `35729` | Integer only; `0` picks a free port. | `bukit-local-server` |
| `preview` | `--strict-port` | off | Fail instead of trying later ports. | `bukit-local-server` |
| `preview`, `dev` | `--allow-lan` / `--public` | off | Allow binding a non-loopback host (aliases). | `bukit-local-server` |
| `dev` | `--output <dir>` | `build.output` | Output directory for dev builds, relative to the project root. | `bukit-local-server` |
| `dev` | `--no-watch` | off | Build once and serve; no watch, no reload. | `bukit-local-server` |
| `clean` | `--dir <dir>` | `./dist` | Target; ignored with `--config`/`--site`. | below |
| `doctor`, `config check` | `--site-url <url>` | `site.url` | Override `site.url` for the checks. | `bukit-config` |
| `config schema` | `--output <file>` | stdout | Write the JSON Schema to a file. | `bukit-config` |
| `seo audit`, `publish audit` | `--dir <dir>` | `dist` | Output directory holding `.bukit/`. | `bukit-seo` |
| `seo audit`, `publish audit` | `--report <file>` | `<dir>/.bukit/<seo-report\|publish-audit-report>.json` | Read this report instead. | `bukit-seo` |
| `seo audit`, `publish audit` | `--strict` | off | Warnings also exit 1. | `bukit-seo` |
| `seo audit`, `publish audit` | `--external` | off | Live HTTP checks of canonicals, images, links. | `bukit-seo` |
| `seo diff`, `publish diff` | `--baseline`, `--current` | required (or positional `diff <baseline> <current>`) | Reports to compare; missing → usage, exit 2. | `bukit-seo` |
| `seo diff`, `publish diff` | `--max-new-errors`, `--max-new-warnings`, `--max-new-issues` | no budget | Allowed new issue counts. | `bukit-seo` |
| `seo diff`, `publish diff` | `--fail-on-new-code`, `--fail-on-route-removed`, `--fail-on-indexable-drop` | off | Extra failure rules. | `bukit-seo` |
| `seo *-insights`, `seo insights` | `--dir <dir>` | `dist` | Build output. | `bukit-seo-insights` |
| `seo *-insights`, `seo insights` | `--routes <file>` | `<dir>/.bukit/seo-route-map.json` | Route map (`--route-map` alias except `seo insights`). | `bukit-seo-insights` |
| `seo *-insights`, `seo insights` | `--observations <a,b>` | required | 1–10 local observation files. | `bukit-seo-insights` |
| `seo *-insights`, `seo insights` | `--rules <file>` | required | Rules profile. | `bukit-seo-insights` |
| `seo question-insights` | `--targets <file>` | required | Question target map. | `bukit-seo-insights` |
| `seo *-insights`, `seo insights` | `--out <file>` | `<dir>/.bukit/<command report>.json` | Report path. | `bukit-seo-insights` |
| `seo *-insights`, `seo insights` | `--strict-join` | off | Exit 1 on unmatched or ambiguous rows. | `bukit-seo-insights` |
| `geo audit` | `--dir <dir>` | `dist` | Output directory. | `bukit-geo` |
| `deploy` | `--dry-run` | off | Print the plan (info level) and exit 0. Checks only config (no provider secrets), `--repo`/`--token-env` syntax, and a completed non-empty output; not git, the token variable, branch/CNAME, the remote, or the publish/privacy scan, so exit 0 does not prove a deploy will succeed. | `bukit-publish-deploy` |
| `deploy` | `--skip-build` | off | Push the existing output. | `bukit-publish-deploy` |
| `deploy` | `--base-url`, `--site-url`, `--output`, `--ci` | config | Passed to the internal clean build. `--output` also picks the directory pushed with `--skip-build`/`--dry-run` (relative to the project root). `--ci` sets the log level to `warn`, hiding the dry-run plan and success line. | `bukit-publish-deploy` |
| `deploy` | `--branch <name>` | `deploy.branch` or `gh-pages` | Target branch. | `bukit-publish-deploy` |
| `deploy` | `--repo <owner/name>` | `deploy.repo`, then `origin` | Target repository. | `bukit-publish-deploy` |
| `deploy` | `--token-env <NAME>` | `GITHUB_TOKEN` | Variable holding the token. | `bukit-publish-deploy` |
| `deploy` | `--message <text>` | `deploy.message` or `bukit deploy` | Commit message. | `bukit-publish-deploy` |
| `deploy` | `--force` | off | Overwrite a diverged branch. | `bukit-publish-deploy` |

### `version` and `completion`

`bukit version` prints two lines, `bukit <version>` and `runtime: native-aot`; parse the
first line in scripts. `bukit completion` without an argument prints the bash script.
Unknown shells exit 2 with `Usage: bukit completion [bash|zsh|fish]`. The scripts
complete command and subcommand names only, not options.

```bash
bukit completion bash > ~/.local/share/bash-completion/completions/bukit
bukit completion zsh > "${fpath[1]}/_bukit"     # then restart zsh
bukit completion fish > ~/.config/fish/completions/bukit.fish
```

## Exit codes

| Code | Meaning |
|---:|---|
| 0 | Success. |
| 1 | Command-level failure: audit found errors (or warnings with `--strict`); diff budget exceeded; insights `--strict-join` gaps; security gate failed; `config check` and `deploy` config errors; `doctor` validation and health-check failures; missing default audit report; deploy readiness or push failure; multilingual worktree errors; any unexpected build error (e.g. asset output collision, JSON code `cli-error`). |
| 2 | Usage or input error: unknown command/option, missing or invalid option value, conflicting options, config or content errors during `build` and `dev`, config load errors for `clean` with `--config`/`--site` and for `preview` (including a `site.yaml` it finds above the served directory: `Preview analytics policy error`), `doctor` when `site.yaml` cannot be loaded (missing file, YAML error, unknown key), an explicit `--report` file that is missing or an invalid SEO/publish report, unsafe `clean` target, refused non-loopback host, unknown completion shell, invalid insight inputs. |
| 3 | Template render failure (`RenderException`). |

`geo audit` exits 0 whatever the score; it fails only for a missing or invalid report (1)
or bad usage/missing directory (2).

## Environment variables

| Variable | Effect |
|---|---|
| `BUKIT_<SECTION>__<KEY>...` | Override any `site.yaml` scalar before validation. Segments are snake case mapped to camelCase: `BUKIT_SITE__BASE_URL=/docs/` sets `site.baseUrl`. Applies to every command that loads config. An empty value can fail validation for every command. |
| `NOTION_TOKEN` | Required for Notion sources when secrets are validated (`build`, `dev`, `config check`, `doctor`, and `deploy` without `--skip-build`/`--dry-run`). |
| `GITHUB_TOKEN` | Default deploy token; choose another variable with `--token-env NAME` (pass the name, never the value). |
| `BUKIT_DEPLOY_GIT_TIMEOUT_SECONDS` | Per-git-command deploy timeout, default 300, `0` = no limit. |
| `BUKIT_BUILD_MODE=release\|profile`, `BUKIT_PROFILE`, `BUKIT_RELEASE` | Make `securityFailMode: auto` strict, like `--ci`. `BUKIT_PROFILE` counts with any non-empty value; `BUKIT_RELEASE` with any non-empty value except `0` or `false`. |
| `BUKIT_TRACE_ID`, `BUKIT_SPAN_ID` | Added to JSON log lines. |

### Logs and JSON errors

Logs go to stderr: `[info] ...` in text mode, one JSON object per line with
`--log-format json`. Errors raised to the top level print one pretty-printed
(multi-line) JSON object on stderr; parse it as a whole document, not line by line:

```json
{"schema": "https://bukit.dev/schemas/cli-error.v1.json", "version": "1.0",
 "command": "build", "exitCode": 2,
 "errors": [{"code": "ContentCollectionMissing", "message": "...", "showUsage": false}]}
```

`usage` appears only for usage errors (`showUsage: true`). Log lines are single-line
`{"ts": ..., "level": "Debug|Info|Warn|Error", "msg": ...}` objects; in a failed run
they come first, and the error object starts at a line that is exactly `{`.

Messages a command prints itself stay plain text in JSON mode: audit results,
`report not found`, preview/clean/geo/insights validation errors, deploy
config/`--repo`/`--token-env` errors, and `config check` / `doctor` output. Deploy
readiness and push failures and dev's host refusal are log lines (JSON in JSON mode).

Level comes from `logging.level` (`debug|info|warn|error`);
`--ci` forces `warn`.

## Default loop

```bash
bukit config check
bukit doctor
bukit build
bukit seo audit --dir dist
bukit publish audit --dir dist
bukit preview        # long-running: serves until Ctrl+C; run it last or in the background
```

Audit commands default `--dir` to `dist`, not to `build.output`. Pass `--dir` when the
output directory is different.

## `clean` safety

`bukit clean` with no options targets `./dist`. `bukit clean --config site.yaml` targets
the configured `build.output`, and `--dir` must stay inside the current directory.
`--dir` is ignored when `--config` or `--site` is given.
It deletes the target and `<root>/.cache`. It never deletes the project `.bukit/`.

The refusals below check only the output path. When clean does not refuse,
`<root>/.cache` (the current directory without `--config`/`--site`) is deleted without
inspection, even when no output directory exists; a refusal (exit 2) leaves both the
output and `.cache` intact. Run `bukit clean` only for a Bukit project root and never where `.cache` is
shared with other tools.

It refuses, with exit 2 and the target left intact: the project root, the home
directory, paths outside the project, any path with a `.git` segment, symlinked
segments, the project `.bukit/` directory, and any non-empty directory without
`.bukit-output-marker`, and (with `--config`/`--site`) a Markdown content source that
overlaps the output or `.cache`. Never create a marker by hand to force a clean.
Success prints `Cleaned: <dir>`, even when nothing existed.

## `doctor` results

Output goes to stdout. Checks run in order and stop at the first `✖` line, or at
`- result: refuse` in the `Output directory safety:` block (output not empty and no
`.bukit-output-marker`), with exit 1; fix it and rerun to see later checks. `⚠` lines are advisory and never change the exit
code, so read them even on exit 0. Success ends with `✔ Doctor passed`. A `site.yaml`
that cannot be loaded (missing, YAML error, unknown key) exits 2; a loaded config that
fails validation prints `✖ Config error` and exits 1. With a Notion source, doctor
validates `NOTION_TOKEN` in the config check: without it the first check fails with
`✖ Config error` / `[BKT-0001] NOTION_TOKEN is required for notion provider ...`
(exit 1). With the token set it calls the Notion API (`✖ Notion database check
failed: <status>` or `✖ Notion request failed: ...`, exit 1).
