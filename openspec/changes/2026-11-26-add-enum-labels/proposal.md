# Enum labels from the content model

## Why

Display labels for enum values live in static `data/*.json` next to the field definitions. With
labels in the model, the CMS field definition can be the single source. Roadmap item F8
(`docs/plans/2026-11-25-generic-data-derivation-roadmap.zh-CN.md`), marked optional.

## What Changes

- `content.modelSchema` `enum` items accept `{ value, label }` next to plain values.
- Templates read the options in definition order as `site.field_enums.<field>` and
  `site.field_enums.scopes.<collection>.<field>`, each `{ value, label }`.
- Duplicate values in one `enum` fail; a custom enum field named `scopes` fails.

## Impact

- Config: internal `CustomFieldDefinitionConfig.EnumLabels`; the public `Enum` stays the value
  list, so validation and the public API are unchanged.
- Engine: `FieldEnumProjection`, published through the internal `__field_enums` site data key.
- Docs: `04-site-yaml-config.md`, content-model and templating skills, CHANGELOG.
- Out of scope: colour themes such as `themeStyle` (not enum labels).
