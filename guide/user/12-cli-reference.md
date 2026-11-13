# 12 CLI Reference

This table is derived from `BukitCliSpecs.cs`.

## Commands

| Command | Purpose | Parameters |
|---|---|---|
| `build` | Build the site. | `--config`, `--site`, `--output`, `--base-url`, `--site-url`, `--clean`, `--no-clean`, `--draft`, `--ci`, `--incremental`, `--no-incremental`, `--cache-dir`, `--metrics`, `--jobs`, `--log-format`, `--notion-snapshot`, `--notion-offline`, `--secrets-stdin` |
| `doctor` | Diagnose config, content, theme, templates, and providers. | `--config`, `--site`, `--site-url`, `--notion-snapshot`, `--notion-offline`, `--secrets-stdin` |
| `config` | Parent command for config checks. | `--config`, `--site`, `--site-url`, `--output` |
| `config check` | Validate config without building pages. | `--config`, `--site`, `--site-url`, `--notion-snapshot`, `--notion-offline`, `--secrets-stdin` |
| `config schema` | Emit JSON Schema for `site.yaml`. | `--output` |
| `preview` | Serve an existing output directory. | `--dir`, `--host`, `--port`, `--strict-port`, `--config`, `--site`, `--allow-lan`, `--public` |
| `dev` | Build, watch files, serve output, and trigger LiveReload. | `--config`, `--site`, `--host`, `--port`, `--output`, `--no-watch`, `--allow-lan`, `--public`, `--secrets-stdin` |
| `clean` | Remove output and cache directories. | `--dir`, `--config`, `--site` |
| `version` | Print version. | none |
| `completion` | Print shell completion. | `<shell>` |
| `seo` | Parent command for SEO reports. | `--dir`, `--report`, `--strict`, `--external` |
| `seo audit` | Validate `.bukit/seo-report.json`. | `--dir`, `--report`, `--strict`, `--external` |
| `seo diff` | Compare SEO reports. | `--baseline`, `--current`, `--max-new-errors`, `--max-new-warnings`, `--max-new-issues`, `--fail-on-new-code`, `--fail-on-route-removed`, `--fail-on-indexable-drop` |
| `seo insights` | Join local SEO observations with the route map and write an offline insights report. | `--dir`, `--routes`, `--observations`, `--rules`, `--out`, `--strict-join` |
| `seo question-insights` | Join a local question target map with search question observations and write an offline coverage report. | `--dir`, `--routes` (`--route-map` alias), `--targets`, `--observations`, `--rules`, `--out`, `--strict-join` |
| `seo generative-insights` | Join local generative answer observations with the route map and write an offline citation report. | `--dir`, `--routes` (`--route-map` alias), `--observations`, `--rules`, `--out`, `--strict-join` |
| `seo authority-insights` | Join local external authority observations with the route map and write an offline citation evidence report. | `--dir`, `--routes` (`--route-map` alias), `--observations`, `--rules`, `--out`, `--strict-join` |
| `geo` | Parent command for GEO reports. | `--dir` |
| `geo audit` | Validate `.bukit/geo-report.json`. | `--dir` |
| `publish` | Parent command for publish audit reports. | `--dir`, `--report`, `--strict`, `--external` |
| `publish audit` | Validate `.bukit/publish-audit-report.json`. | `--dir`, `--report`, `--strict`, `--external` |
| `publish diff` | Compare publish audit reports. | `--baseline`, `--current`, `--max-new-errors`, `--max-new-warnings`, `--max-new-issues`, `--fail-on-new-code`, `--fail-on-route-removed`, `--fail-on-indexable-drop` |
| `deploy` | Build if needed and deploy to GitHub Pages. | `--config`, `--site`, `--dry-run`, `--skip-build`, `--base-url`, `--site-url`, `--output`, `--branch`, `--repo`, `--token-env`, `--secrets-stdin`, `--message`, `--ci`, `--force` |

`bukit deploy --dry-run` validates an existing build without rebuilding or
requiring provider secrets. If the output directory is missing, it exits 1 and
asks you to run `bukit build` first.

`bukit clean --dir <path>` only removes an empty directory or a Bukit output
directory containing `.bukit-output-marker`. It refuses the project root,
user home, filesystem root, `.git` or a `.git` descendant, paths outside the
current project, targets reached through symlink/reparse-point segments, and
non-empty unmarked directories. A refusal returns exit code 2 and preserves the
target. Config-based clean and build recovery use the same cleaner.

All `bukit clean` forms (default, `--dir`, `--config`, and `--site`) preserve the
project-root `.bukit` directory, including plugin configuration, lock files,
unknown user files, and generated metadata. Eligible output and the project
`.cache` are still removed. Selecting project-root `.bukit` or one of its
descendants as clean output is refused with exit code 2 before deleting anything,
even with an output marker; case variants are also protected. Similarly named
siblings such as `.bukit-output` remain eligible under the ordinary safety checks.
This metadata-retention rule applies to CLI clean, not build recovery.

## Common Examples

```bash
bukit config check --config site.yaml
bukit doctor --config site.yaml
bukit build --config site.yaml --clean --metrics .bukit/metrics.json
bukit dev --config site.yaml --port 35729
bukit preview --dir dist --port 4173
bukit seo audit --dir dist --strict
bukit seo insights --dir dist --observations incoming/gsc.json,incoming/ga4.json --rules seo-insights-rules.json
bukit seo question-insights --dir dist --route-map dist/.bukit/seo-route-map.json --targets observations/question-targets.json --observations observations/gsc-questions.json --rules seo-insights-rules.json
bukit seo generative-insights --dir dist --route-map dist/.bukit/seo-route-map.json --observations observations/generative-runs.json --rules seo-insights-rules.json
bukit seo authority-insights --dir dist --route-map dist/.bukit/seo-route-map.json --observations observations/external-authority.json --rules seo-insights-rules.json
bukit publish audit --dir dist --strict
bukit deploy --dry-run
```

## Passing Secrets on Standard Input

`build`, `dev`, `doctor`, `config check`, and `deploy` accept `--secrets-stdin`.
With it, Bukit reads tokens from standard input instead of the process
environment. On macOS and Linux other programs of the same user can read a
running process's environment; a token read from stdin stays in Bukit's memory.

Bukit reads stdin to end of file before any other work. The input is one JSON
object, at most 64 KiB, mapping a name to a non-empty string:

```json
{"NOTION_TOKEN":"<notion token>","GITHUB_TOKEN":"<github token>"}
```

- Names look like environment variable names: a letter or `_`, then letters,
  digits, and `_`, up to 128 characters. Names Bukit does not use are ignored,
  and `{}` is valid.
- Each secret Bukit needs is looked up in the stdin object first, then in the
  environment: `NOTION_TOKEN` for Notion sources, and for `deploy` the name
  given by `--token-env` (default `GITHUB_TOKEN`). Without `--secrets-stdin`
  nothing changes: tokens come from environment variables as before.
- Bukit never copies stdin secrets into an environment, so `git`, Sass, image
  tools, and plugin processes it starts do not inherit them. Multilingual build
  workers receive them on their own stdin. Log lines and error messages replace
  any stdin secret value with `***`.
- Unusable input fails with exit code 2 and error code `secrets-stdin-invalid`
  before anything else runs: empty input, invalid JSON or UTF-8, a value that is
  not a non-empty string, an invalid or duplicate name, input over 64 KiB, stdin
  attached to a terminal, or a command that does not take the flag. The message
  never repeats the input.
- Plugin commands do not take `--secrets-stdin`; plugins keep reading the
  environment variables their manifest allows.
- Known limit: during `deploy`, Bukit still hands the GitHub token to each
  authenticated `git` process in that process's environment (read by a
  temporary `GIT_ASKPASS` script). While `git` runs, other programs of the same
  user can read it there.

A program that starts Bukit writes the JSON to the child's stdin and closes it:

```bash
my-secret-tool print bukit-secrets | bukit build --config site.yaml --secrets-stdin
```

## Exit Codes

| Code | Meaning |
|---:|---|
| 0 | Success. |
| 1 | General runtime failure. |
| 2 | Command argument, config, or content setup error, including invalid `--secrets-stdin` input. |
| 3 | Render error. |

`seo insights` refines the generic table: it returns `0` after writing the
report when joins are complete or gaps are allowed, `1` only when
`--strict-join` finds unmatched/ambiguous rows (after writing the report), and
`2` for input, local-path, schema, or read/write failures. See
[21 SEO Insights](21-seo-insights.md) for required options, defaults, and the
offline collector boundary.

`seo question-insights` uses the same exit-code refinement for its two-stage
join of question targets and search question observations. See
[22 SEO Question Insights](22-seo-question-insights.md) for required options,
defaults, and the privacy boundary.

For `seo question-insights`, `seo generative-insights`, and
`seo authority-insights`, the implementation-plan spelling `--route-map` is a
compatibility alias for the canonical `--routes` option. Both names bind the
same local route-map input and share the same default.

`seo generative-insights` uses the same exit-code refinement for its join of
generative cited URLs with the route map. See
[23 Generative Citation Insights](23-generative-citation-insights.md) for
required options, defaults, and the privacy boundary.

`seo authority-insights` uses the same exit-code refinement for its join of
external cited URLs with the route map. See
[24 External Authority Insights](24-external-authority-insights.md) for
required options, defaults, and the privacy boundary.

## Dynamic Plugin Commands

If a project config exposes plugin commands through plugin manifests, `Program`
can compose those commands after Core command resolution. They are project
capabilities, not static Core commands.
