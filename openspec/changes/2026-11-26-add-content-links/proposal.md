# Resolve content links

## Why

Pages often store keys of other documents: a route stores the slugs of its cities, and each
itinerary day stores the codes of its attractions. Templates can only show those keys, and SEO,
Open Graph and search cannot use the targets' titles, URLs or images. The existing
`customFields[].reference` rule only validates reference maps a source already resolved, and Notion
relation links serve only Notion relations. travelWeb, the named consumer (roadmap
`docs/plans/2026-11-25-generic-data-derivation-roadmap.zh-CN.md`, item F2), needs generic links,
including nested paths through the structured fields that F1 added.

## What Changes

- A new `content.links` list. Each entry names the page collection, the key field (a field name or
  a nested path such as `days[].attractions`), the target collection, the target key field
  (`slug` by default) and optional `select` fields to copy.
- After routing, each page in the collection gets `<field>_links`, written next to the key field and
  never over it. A nested path also gets one top-level list without repeats
  (`days_attractions_links`).
- Unknown keys, keys repeated in one language, non-text keys and existing output fields become
  content validation issues under the page collection's `schemaFailMode`.
- The links are page fields, so incremental builds rebuild a linking page when a target's title,
  URL or selected fields change.

## Impact

- Public API: none. `ContentConfig.Links` and its types are internal, set only through `site.yaml`, because the public API baseline is frozen.
- Config: the `content.links` reader, the strict field validator, the JSON schema, and semantic validation.
- Engine: `RoutePipeline` applies the links, and `ContentGraphValidateStage` reports their problems.
- Docs: `04-site-yaml-config.md`, the `bukit-config` and `bukit-content-model` skills, and CHANGELOG in three languages.
- Out of scope: media targets (`target: { source: media }`), data documents as link sources, and following links recursively.
