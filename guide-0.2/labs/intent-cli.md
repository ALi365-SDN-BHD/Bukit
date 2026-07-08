# Labs: Intent CLI

Status: not Core 1.0.

The historical intent workflow converted an `intent.yaml` file into
`site.yaml`. It is not part of the Core command registry.

## Core Boundary

Core does not include an intent command. Core docs should describe direct
`site.yaml` authoring and validation through:

```bash
bukit config check
bukit doctor
```

## Historical Shape

Older drafts described:

```bash
bukit intent init --out intent.yaml
bukit intent validate intent.yaml
bukit intent apply intent.yaml --out site.yaml
```

Do not route default agents or users to this workflow as Core behavior.

## Labs Re-Entry Requirements

Intent work needs:

- a Labs-owned command surface or a deliberate Core registry change;
- strict mapping to current `AppConfig`;
- schema validation against `ConfigJsonSchemaGenerator`;
- failure behavior for unknown intent fields;
- tests proving generated `site.yaml` passes Core validation.

