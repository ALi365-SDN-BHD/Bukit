# Bukit Import + Notion Plugins 1.0.0-rc.1 Release Notes

Date: 2026-06-28

## Release Status

`Bukit.Plugin.Import` and `Bukit.Plugin.Notion` are marked `1.0.0-rc.1` in their
runtime handshake identities and static package manifests.

This document records the RC scope. Publishing or tagging remains blocked until
the exact release commit has both:

- a completed successful `ci.yml` run on `main` or `master`;
- a successful manual create-mode acceptance against a dedicated Notion sandbox.

## Import Plugin RC Scope

- External process plugin with `import seed` and `import html-demo` commands.
- Generates local Markdown/JSON/YAML/Notion seed artifacts.
- Generates `notion-seed/notion-database-map.yaml` with default `Title`, `Slug`,
  and `Published` property mappings.
- Keeps `network: false` and `environment.read: []`.
- Does not read `NOTION_TOKEN` and does not call the Notion API.

## Notion Plugin RC Scope

- External process plugin with `notion validate-seed`,
  `notion validate-database-map`, and `notion push`.
- Supports create, upsert, replace, and dry-run planning.
- Reads the Notion token only from the allowlisted `NOTION_TOKEN` environment
  variable.
- Writes JSON/Markdown reports under `.bukit/reports/plugin-output/notion` or
  `.bukit/tmp/notion`.
- Preserves completed, failed, and skipped record states after partial failure.
- Splits title/rich-text values to Notion request limits and rejects oversized
  property arrays locally.

## Handoff Contract

Import owns local generation. Notion owns validation and remote writes. The
supported handoff sequence is documented in
[Import to Notion Handoff Usage](../plugins/import-notion-handoff-usage.md).

Supported push collections:

- `pages`
- `navigation`
- `posts`
- `companies`
- `services`

Import also emits `sections`, `faqs`, `media`, and `components`; the Notion RC
reports these as unsupported rather than silently pushing them.

## Packaging and Release Gate

Both plugins are self-contained packages for `win-x64`, `linux-x64`, and
`osx-arm64`. The release gate must build and smoke both packages. Executables
remain under `plugins/<id>/bin/<rid>` and are never stored in `.bukit`.

## Known RC Constraints

- `databaseId`/`dataSourceId` values are intentionally user-supplied.
- Create mode is not idempotent and can create duplicates when repeated.
- Replace mode is not atomic; properties can be updated before block replacement
  fails.
- Live acceptance must use a dedicated disposable Notion sandbox data source.
- stdout is protocol JSON only; operational logs use stderr.

## Deferred to v1.1

- `import validate-handoff`
- `import-handoff-report.json`
- `--template full|bare|none`
- `--base-url`
- `--no-preserve-html`
- stronger route-map schema validation

