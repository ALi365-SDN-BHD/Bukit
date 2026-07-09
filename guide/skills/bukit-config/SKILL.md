---
name: bukit-config
description: Use for Bukit site.yaml fields, defaults, validation errors, and config examples.
---

# Bukit Config

`site.yaml` maps to `AppConfig`. Required top-level sections are `site` and
`content`; `content.sources[]` is required in Bukit 1.0.

Key validation files: `ConfigStrictFieldValidator`, `ConfigValidator`,
`ProviderValidators`, `CollectionsValidator`, and `ConfigJsonSchemaGenerator`.

Use `bukit config check` for validation and `bukit config schema` to emit the
current schema.
