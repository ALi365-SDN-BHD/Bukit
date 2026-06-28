# Import Notion Push Design ADR

## Status

Accepted for Import plugin v1.

## Decision

The Import process plugin does not implement direct Notion push.

Instead, Import generates local handoff artifacts:

- `sites/<site-name>/notion-seed/*.json`
- `sites/<site-name>/notion-seed/notion-database-map.yaml`
- import report entries and plugin artifacts that point at those files

Actual Notion write behavior belongs in a separate `Bukit.Plugin.Notion` plugin
or a later protocol revision with command-level permissions.

## Rationale

The current plugin permission model is plugin-level. If Import declared network
access and token environment access for Notion push, every user enabling the
Import plugin would need to grant those permissions even when running local
HTML imports that only write files.

That conflicts with least-privilege behavior.

Keeping Import local-only preserves:

- no network permission
- no token environment reads
- predictable stdout JSON and stderr logs
- static manifest policy where runtime capabilities do not exceed `plugin.yaml`

## Current Implementation

`Bukit.Plugin.Notion` now declares network and token permissions explicitly and
owns:

- local seed and database-map validation
- create/upsert/replace push modes
- dry-run and execution reports containing remote IDs

Database creation remains outside the `1.0.0-rc.1` scope. Import remains
local-only even if a future command-level permission model is introduced.

## Non-Goals

This ADR does not move the Notion API client or push implementation into Import.
