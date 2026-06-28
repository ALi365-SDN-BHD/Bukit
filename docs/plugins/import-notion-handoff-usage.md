# Import to Notion Handoff Usage

This guide covers the `1.0.0-rc.1` handoff from `Bukit.Plugin.Import` to the
separate `Bukit.Plugin.Notion` process plugin.

## Boundaries

- Import only reads and writes local project files.
- Import has no network permission and reads no environment variables.
- Notion owns all API requests and is the only plugin allowed to read
  `NOTION_TOKEN`.
- Do not add Notion push options back to `bukit import`.

## Prerequisites

1. Install/enable both process plugins in the project `.bukit/plugins.yaml`.
2. Keep plugin executables under `plugins/import/bin/<rid>` and
   `plugins/notion/bin/<rid>`, never under `.bukit`.
3. Prepare a static HTML demo directory.
4. For live push only, export `NOTION_TOKEN` and share a dedicated sandbox data
   source with the integration.

## 1. Generate the Handoff

`--theme` is required by the Import RC command contract:

```bash
bukit import html-demo ./demo \
  --theme demo \
  --content-source notion \
  --build-source markdown \
  --force
```

The command generates:

```text
sites/demo/notion-seed/
├── pages.json
├── navigation.json
├── sections.json
├── posts.json
├── companies.json
├── services.json
├── faqs.json
├── media.json
├── components.json
└── notion-database-map.yaml
```

The default map includes property mappings but leaves every database/data source
ID empty. Before map validation, set `dataSourceId` or legacy `databaseId` for
each collection you intend to push. Remove map entries you do not intend to
push.

Each target Notion data source must have compatible properties:

```yaml
properties:
  Title:
    source: title
    type: title
  Slug:
    source: slug
    type: rich_text
  Published:
    source: published
    type: checkbox
```

## 2. Validate Local Seed Artifacts

```bash
bukit notion validate-seed ./sites/demo/notion-seed
```

`sections.json`, `faqs.json`, `media.json`, and `components.json` produce the
warning `notion.seedUnsupportedFiles` in this RC. Supported collections are
pages, navigation, posts, companies, and services.

## 3. Validate the Database Map

```bash
bukit notion validate-database-map \
  ./sites/demo/notion-seed/notion-database-map.yaml
```

An empty ID fails with `notion.databaseMapMissingDataSource`. Missing property
mappings fail locally before any token or network access is required.

## 4. Generate a Dry-Run Plan

Dry-run does not require a token and does not call Notion:

```bash
bukit notion push \
  --seed ./sites/demo/notion-seed \
  --database-map ./sites/demo/notion-seed/notion-database-map.yaml \
  --mode create \
  --dry-run
```

Review both reports:

```text
.bukit/reports/plugin-output/notion/notion-push-report.json
.bukit/reports/plugin-output/notion/notion-push-report.md
```

## 5. Run Live Sandbox Acceptance

Use a dedicated disposable data source. Create mode is not idempotent.

```bash
export NOTION_TOKEN='...'

bukit notion push \
  --seed ./sites/demo/notion-seed \
  --database-map ./sites/demo/notion-seed/notion-database-map.yaml \
  --token-env NOTION_TOKEN \
  --mode create
```

Never place the token in YAML, reports, command arguments, or committed files.
The maintained acceptance wrapper is
`scripts/smoke/import-notion-rc-manual.sh`; it additionally requires
`NOTION_DATA_SOURCE_ID` and `BUKIT_NOTION_RC_CONFIRM=YES`. The wrapper preserves
the generated map and writes a pages-only acceptance map to
`.bukit/tmp/notion/rc-manual-database-map.yaml`, so one disposable data source is
enough and unrelated generated collections are not written during RC approval.

## Acceptance Result

The RC is not approved by dry-run alone. Approval requires:

- import handoff artifacts generated successfully;
- local seed and map validation passing;
- dry-run report passing;
- live create report containing remote page IDs;
- no token/raw secret in JSON, Markdown, stdout, or stderr artifacts;
- package smoke and repository release gate passing;
- successful same-commit GitHub CI evidence.
