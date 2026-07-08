---
name: bukit-cli-reference
description: Use when an agent needs to execute Bukit commands, explain command options, detect the Bukit CLI, interpret exit codes, or keep command guidance aligned with Bukit Core 1.0.
status: stable
since: "v4.0.0-core1"
verified_by:
  - "guide/skills/scripts/check-cli-commands.py"
  - "tests/Bukit.Architecture.Tests/CoreBoundaryTests.cs"
source_anchors:
  - "src/Bukit-Core/Bukit.Cli/Cli/BukitCliSpecs.cs"
  - "src/Bukit-Core/Bukit.Cli/Cli/BukitCliDescriptors.cs"
guide_chapters:
  - "guide/skills/README.md"
---

# Bukit CLI Reference

This skill is the command source of truth for Bukit Core 1.0. Do not suggest commands outside the registry in `BukitCliSpecs.cs`.

## Detection

```bash
bukit version
```

Use local binaries such as `./bukit` or `.\bukit.exe` when the executable is not on `PATH`.

## Command Quick Reference

| Command | Purpose | Key Parameters |
|---|---|---|
| `build` | Build static output | `--config` `--site` `--output` `--base-url` `--site-url` `--clean` `--no-clean` `--draft` `--ci` `--incremental` `--no-incremental` `--cache-dir` `--metrics` `--jobs` `--log-format` |
| `doctor` | Diagnose config, templates, routes, output reports, and providers | `--config` `--site` `--site-url` |
| `config` | Parent for config diagnostics | `--config` `--site` `--site-url` `--output` |
| `config check` | Validate configuration without building | `--config` `--site` `--site-url` |
| `config schema` | Generate `site.yaml` JSON Schema | `--output` |
| `preview` | Serve generated output locally | `--dir` `--host` `--port` `--strict-port` `--config` `--site` |
| `dev` | Development preview with file watching and browser reload | `--config` `--site` `--host` `--port` `--output` `--no-watch` `--allow-lan` `--public` |
| `clean` | Clean output and cache directories | `--dir` `--config` `--site` |
| `version` | Print version information | none |
| `completion` | Generate shell completion script | `<shell>` |
| `seo` | Parent for SEO quality gates | `--dir` `--report` `--strict` `--external` |
| `seo audit` | Validate `.bukit/seo-report.json` | `--dir` `--report` `--strict` `--external` |
| `seo diff` | Compare SEO reports | `--baseline` `--current` `--max-new-errors` `--max-new-warnings` `--max-new-issues` `--fail-on-new-code` `--fail-on-route-removed` `--fail-on-indexable-drop` |
| `geo` | Parent for GEO quality gates | `--dir` |
| `geo audit` | Validate `.bukit/geo-report.json` and generated GEO files | `--dir` |
| `publish` | Parent for publish-readiness quality gates | `--dir` `--report` `--strict` `--external` |
| `publish audit` | Validate `.bukit/publish-audit-report.json` | `--dir` `--report` `--strict` `--external` |
| `publish diff` | Compare publish audit reports | `--baseline` `--current` `--max-new-errors` `--max-new-warnings` `--max-new-issues` `--fail-on-new-code` `--fail-on-route-removed` `--fail-on-indexable-drop` |
| `deploy` | Build and deploy to GitHub Pages | `--config` `--site` `--dry-run` `--skip-build` `--base-url` `--site-url` `--output` `--branch` `--message` `--ci` `--force` |

## Common Command Paths

```bash
bukit config check
bukit doctor
bukit build
bukit dev
bukit preview --dir dist
bukit seo audit --dir dist
bukit geo audit --dir dist
bukit publish audit --dir dist
bukit deploy --dry-run
```

## Exit Code Guidance

| Exit code | Meaning |
|---|---|
| 0 | Success |
| 1 | Runtime, build, validation, or quality-gate failure |
| 2 | Command usage, argument, missing directory, or invalid option failure |

## Operational Notes

- Run commands from the site root unless `--config` or `--site` resolves another config.
- `preview` serves existing output and does not rebuild.
- `dev` builds first, watches content/theme/template/static inputs, and sends browser reload messages after rebuilds.
- `deploy` runs a build unless `--skip-build` is supplied.
- `config check` validates provider secrets; a Notion source requires `NOTION_TOKEN`.
- Labs commands are absent from this Core reference.
