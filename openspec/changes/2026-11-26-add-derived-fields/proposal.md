# Collection derived fields

## Why

City pages need an SEO title made from the Chinese and English names and a description cut from
the summary. Today every page carries these by hand. Roadmap item F9
(`docs/plans/2026-11-25-generic-data-derivation-roadmap.zh-CN.md`).

## What Changes

- `content.derivedFields.<collection>.<field>: { value, mode }`: `value` is a Scriban template over
  the page's own top-level fields (including `<field>_links`); `mode` is `default` (fill missing or
  empty) or `override`.
- `seo.describe <width>` cuts plain text by display width.
- Templates are deterministic: invariant culture, loop limit, no clock or random functions.
- A template that does not parse fails at config load; a runtime failure is reported as
  `content.derived_field_failed` under the collection's `schemaFailMode`.

## Impact

- Config: `ContentConfig.DerivedFields` (internal), reader, strict keys, schema, validator.
- Engine: `DerivedFieldResolver` runs in `RoutePipeline` after content links; the validate stage
  reports runtime failures. Rendering: `DerivedFieldTemplate`.
- No public API change. Docs: `04-site-yaml-config.md`, the `bukit-content-model` skill, CHANGELOG.
- Out of scope: reading other pages or `site` data, typed (non-text) results.
