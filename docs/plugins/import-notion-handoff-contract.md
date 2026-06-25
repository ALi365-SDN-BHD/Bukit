# Bukit Import Notion Handoff Contract

This document defines the `Notion handoff design` module for the external
Import process plugin.

## Scope

`bukit import html-demo <demo-dir> --theme <name>` now exposes local handoff
controls:

```text
--content-source <markdown|json|yaml|notion>
--build-source <markdown|notion>
--no-seed
```

For this module, the main handoff path is:

```text
--content-source notion --build-source markdown
```

That path generates local Markdown content for build/review and also generates
Notion seed files under:

```text
sites/<site-name>/notion-seed/
```

## Handoff Artifacts

When Notion seed generation is enabled, plugin invoke responses include:

```text
type: notion-seed
path: sites/<site-name>/notion-seed

type: notion-database-map
path: sites/<site-name>/notion-seed/notion-database-map.yaml
```

The domain import also emits:

```text
diagnostic: import.notionHandoffReady
```

## `--no-seed`

`--no-seed` disables handoff seed generation and omits Notion seed artifacts.
Theme, site, content, and report generation continue according to the other
options.

## Permission Boundary

The Import plugin does not request network permission and does not request
environment variable access. It does not read `NOTION_TOKEN`, call Notion APIs,
or push content to Notion.

## Deferred Scope

Direct Notion push, token handling, schema validation against a live workspace,
database creation, and Notion database ID options are deferred to a separate
Notion plugin or a future command-level permission model.
