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

## Future Direction

A dedicated Notion plugin can declare network and token permissions explicitly
and own workflows such as:

- schema validation against a live Notion workspace
- database creation
- upsert/push
- sync reports that contain remote IDs

Alternatively, a future Core Host Action or command-level plugin permission
model can allow Import to request elevated Notion permissions only for a single
push command.

## Non-Goals

This ADR does not define a Notion API client, database schema writer, or push
command implementation.
