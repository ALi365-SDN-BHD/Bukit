# vNext Meta Removal Spec

## Goal

Remove `ContentItem.Meta` as a runtime business data surface in Bukit vNext.
Raw Markdown front matter and Notion properties remain valid provider input,
but they must be normalized once into typed content documents before routing,
rendering, SEO, audit, feeds, search, projections, CLI, or plugins consume them.

## Source Plan

This spec implements the design in:

- `docs/plans/2026-06-06-meta-removal-vnext.md`

## Current State

- `ContentItem.Meta` is still part of the shared runtime model.
- P1 introduced `ContentRecord` and `CanonicalContentGraph`, but many consumers
  still depend on `ContentItem` and some direct or fallback `Meta` access.
- Providers currently emit `ContentItem`, causing raw provider properties to
  travel beyond the ingestion boundary.
- Plugin protocol DTOs expose `Meta`.
- Templates can still access `page.meta`.

## Target State

- Providers emit `RawContentDocument`.
- `IContentNormalizer` converts raw provider input into `ContentDocument`.
- `ContentDocument.Record` is the semantic truth.
- `ContentDocument.CustomFields` is the only dynamic user field surface.
- `ContentRoutePolicy`, `ContentPublishPolicy`, and `ContentSourceInfo` replace
  route, publish, data-module, and source-related `Meta` keys.
- Runtime engine modules consume `ContentDocument`, not raw provider maps.
- Plugin protocol v2 removes `Meta`.
- Templates expose typed canonical objects and do not expose `page.meta`.

## Non-Goals

- Do not preserve runtime compatibility fallback for `Meta`.
- Do not keep plugin protocol v1 in vNext.
- Do not silently accept unknown front matter keys.
- Do not add new NuGet dependencies.
- Do not change unrelated P2/P3 output behavior except where required by the
  typed document model.

## Breaking Change Policy

This is a major-version breaking change. Per project rules, no implicit
compatibility branch, `[Obsolete]` forwarding API, silent fallback, or automatic
legacy migration should be introduced.

## Scope

In scope:

- Abstractions model changes.
- Provider raw document output.
- Normalization layer.
- Content pipeline result changes.
- Routing, rendering, SEO, feeds, search, sitemap, audit, projections, CLI, and
  plugin protocol conversion.
- Template model migration.
- Docs and tests.

Out of scope:

- Changing user-facing site config unrelated to content normalization.
- Reworking visual theme design.
- Adding new publishing formats beyond those already present in P2/P3 work.

## Verification Strategy

- TDD for every `.cs` behavior change.
- Inventory test preventing `.Meta`, `MetaHelpers`, and `page.meta` from
  re-entering runtime modules.
- Targeted tests per phase.
- Full project test suites before claiming completion.
- Final quality gate when all phases are complete.
