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
that command's usage. Both `--opt value` and `--opt=value` work.

## Options by command

Project selection, shared by most commands:

- `--config <path>`: config file, default `./site.yaml`. The project root is the
  config file's directory (for `<root>/sites/<name>/site.yaml` the root is `<root>`).
- `--site <name>`: use `./sites/<name>.yaml`; root is the current directory.
  `--config` wins when both are given. Paths escaping `sites/` are rejected.
- `--site-url <url>`: override `site.url` for this run.

| Command | Options |
|---|---|
| `build` | `--config`, `--site`, `--output`, `--base-url`, `--site-url`, `--clean`, `--no-clean`, `--draft`, `--ci`, `--incremental`, `--no-incremental`, `--cache-dir`, `--metrics`, `--jobs`, `--log-format` |
| `doctor` | `--config`, `--site`, `--site-url` |
| `config check` | `--config`, `--site`, `--site-url` |
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

Bare `bukit seo`, `bukit publish`, `bukit geo`, and `bukit config` print usage and exit 2.

### `build` option semantics

| Option | Effect |
|---|---|
| `--output <dir>` | Override `build.output` (default `dist`). |
| `--base-url <path>` | Override `site.baseUrl` (default `/`). |
| `--clean` / `--no-clean` | Override `build.clean` (default `true`). Mutually exclusive. |
| `--draft` | Keep documents with `draft: true`. Without it they are removed. |
| `--ci` | Log level `warn`; forces `build.followSymlinks: false`; turns `build.report.securityFailMode: auto` into strict. |
| `--incremental` / `--no-incremental` | Incremental rendering is on by default. Mutually exclusive. |
| `--cache-dir <dir>` | Replace `<root>/.cache` for the build manifest and media cache. Relative to the current directory; not used by `dev`, not removed by `clean`. |
| `--metrics <file.json>` | Write stage metrics JSON plus a sibling `.html`, relative to the project root. |
| `--jobs <n>` | Render parallelism, positive integer (default CPU count). |
| `--log-format text\|json` | Log and error format. Accepted on every command line; JSON errors go to stderr. |

Details: `bukit-build`. Servers: `bukit-local-server`. Audits and deploy:
`bukit-seo`, `bukit-geo`, `bukit-publish-deploy`, `bukit-seo-insights`.

### Other options at a glance

| Command | Option | Default | Meaning | Details |
|---|---|---|---|---|
| `preview` | `--dir <dir>` | `build.output` with `--config`/`--site`, else `./dist` | Directory to serve. | `bukit-local-server` |
| `preview`, `dev` | `--host <host>` | `localhost` | Bind address; non-loopback needs `--allow-lan`. | `bukit-local-server` |
| `preview` | `--port <n\|auto>` | `4173` | Port; `auto` picks a free one. | `bukit-local-server` |
| `dev` | `--port <n>` | `35729` | Integer only; `0` picks a free port. | `bukit-local-server` |
| `preview` | `--strict-port` | off | Fail instead of trying later ports. | `bukit-local-server` |
| `preview`, `dev` | `--allow-lan` / `--public` | off | Allow binding a non-loopback host (aliases). | `bukit-local-server` |
| `dev` | `--output <dir>` | `build.output` | Output directory for dev builds. | `bukit-local-server` |
| `dev` | `--no-watch` | off | Build once and serve; no watch, no reload. | `bukit-local-server` |
| `clean` | `--dir <dir>` | `./dist` | Target; ignored with `--config`/`--site`. | below |
| `doctor`, `config check` | `--site-url <url>` | `site.url` | Override `site.url` for the checks. | `bukit-config` |
| `config schema` | `--output <file>` | stdout | Write the JSON Schema to a file. | `bukit-config` |
| `seo audit`, `publish audit` | `--dir <dir>` | `dist` | Output directory holding `.bukit/`. | `bukit-seo` |
| `seo audit`, `publish audit` | `--report <file>` | `<dir>/.bukit/<seo-report\|publish-audit-report>.json` | Read this report instead. | `bukit-seo` |
| `seo audit`, `publish audit` | `--strict` | off | Warnings also exit 1. | `bukit-seo` |
| `seo audit`, `publish audit` | `--external` | off | Live HTTP checks of canonicals, images, links. | `bukit-seo` |
| `seo diff`, `publish diff` | `--baseline`, `--current` | required | Reports to compare. | `bukit-seo` |
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
| `deploy` | `--dry-run` | off | Print the plan; needs a completed build. | `bukit-publish-deploy` |
| `deploy` | `--skip-build` | off | Push the existing output. | `bukit-publish-deploy` |
| `deploy` | `--base-url`, `--site-url`, `--output`, `--ci` | config | Passed to the internal build. | `bukit-publish-deploy` |
| `deploy` | `--branch <name>` | `deploy.branch` or `gh-pages` | Target branch. | `bukit-publish-deploy` |
| `deploy` | `--repo <owner/name>` | `deploy.repo`, then `origin` | Target repository. | `bukit-publish-deploy` |
| `deploy` | `--token-env <NAME>` | `GITHUB_TOKEN` | Variable holding the token. | `bukit-publish-deploy` |
| `deploy` | `--message <text>` | `deploy.message` or `bukit deploy` | Commit message. | `bukit-publish-deploy` |
| `deploy` | `--force` | off | Overwrite a diverged branch. | `bukit-publish-deploy` |

### `version` and `completion`

`bukit version` prints two lines, `bukit <version>` and `runtime: native-aot`; parse the
first line in scripts. `bukit completion` without an argument prints the bash script.
Unknown shells exit 2 with `Usage: bukit completion [bash|zsh|fish]`.

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
| 2 | Usage or input error: unknown command/option, missing or invalid option value, conflicting options, config or content errors during `build`, `doctor` when `site.yaml` cannot be loaded (missing file, YAML error, unknown key), an explicit `--report` file that is missing or an invalid SEO/publish report, unsafe `clean` target, refused non-loopback host, unknown completion shell, invalid insight inputs. |
| 3 | Template render failure (`RenderException`). |

`geo audit` exits 0 whatever the score; it fails only for a missing or invalid report (1)
or bad usage/missing directory (2).

## Environment variables

| Variable | Effect |
|---|---|
| `BUKIT_<SECTION>__<KEY>...` | Override any `site.yaml` scalar before validation. Segments are snake case mapped to camelCase: `BUKIT_SITE__BASE_URL=/docs/` sets `site.baseUrl`. Applies to every command that loads config. An empty value can fail validation for every command. |
| `NOTION_TOKEN` | Required for Notion sources when secrets are validated (`build`, `config check`, `doctor`). |
| `GITHUB_TOKEN` | Default deploy token; choose another variable with `--token-env NAME` (pass the name, never the value). |
| `BUKIT_DEPLOY_GIT_TIMEOUT_SECONDS` | Per-git-command deploy timeout, default 300, `0` = no limit. |
| `BUKIT_BUILD_MODE=release\|profile`, `BUKIT_PROFILE`, `BUKIT_RELEASE` | Make `securityFailMode: auto` strict, like `--ci`. `BUKIT_PROFILE` counts with any non-empty value; `BUKIT_RELEASE` with any non-empty value except `0` or `false`. |
| `BUKIT_TRACE_ID`, `BUKIT_SPAN_ID` | Added to JSON log lines. |

Logs go to stderr: `[info] ...` in text mode, one JSON object per line with
`--log-format json`. Level comes from `logging.level` (`debug|info|warn|error`);
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

The refusals below protect the output directory only. `<root>/.cache` (the current
directory without `--config`/`--site`) is always deleted, even when no output directory
exists, so run `bukit clean` only for a Bukit project root and never where `.cache` is
shared with other tools.

It refuses, with exit 2 and the target left intact: the project root, the home
directory, paths outside the project, any path with a `.git` segment, symlinked
segments, the project `.bukit/` directory, and any non-empty directory without
`.bukit-output-marker`. Never create a marker by hand to force a clean.
