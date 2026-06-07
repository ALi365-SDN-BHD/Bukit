# P3 Publish Projections Completion Spec

## Goal

Complete the P3 publishing layer by turning the existing ad hoc content projection output into a first-class projection contract, registry, and auditable representation inventory.

## Current State

- P1 canonical content graph exists through `ContentRecord`, `EntityRecord`, `ContentRelation`, provenance, trust, and media records.
- P2 machine readability and trust audit exists through `PublishDocument`, `PublishAuditReport`, and `MachineReadabilityTrustAuditBuilder`.
- P3 has partial output support through `ContentProjectionWriter`, which writes `content/*.json`, `content/*.md`, and `agent-manifest.json`.
- `IPublishProjection` is an engine-internal contract. It is not a public external plugin ABI because its context includes internal routing, SEO, rendering, variant, and build-context types. External process plugins that need to emit additional machine-readable files should continue using the protocol after-build output channel until a dedicated projection plugin ABI is designed.

## Required Behavior

- The engine exposes a projection contract with explicit document and aggregate projection metadata.
- Built-in JSON, Markdown, and agent manifest projections execute through `IPublishProjection.Project(PublishProjectionContext)` and return generated output inventory.
- The projection contract remains internal and must not be documented as the external plugin extension point.
- The registry is the single source for document representation kinds used by templates, publish documents, content projections, and agent manifests.
- `agent-manifest.json` is written by the projection pipeline, not by the audit writer.
- Publish audit detects missing required per-document projection files.
- Publish audit records a structured representation inventory for every document.
- Publish audit validates that JSON, Markdown, and agent manifest metadata stay consistent with the canonical publish document.
- Expired, unpublished, and noindex content must not be included in indexable aggregate outputs such as sitemap, search, feeds, and agent manifest.
- Aggregate outputs remain on their current paths and are represented in the registry without rewriting their internal generators, including RSS, Atom, JSON Feed, sitemap, search, llms, robots, and agent manifest.

## Non-Goals

- Do not redesign P1 canonical content models.
- Do not redesign P2 audit report semantics.
- Do not rewrite feed, sitemap, search, robots, or llms generation algorithms.
- Do not add NuGet dependencies.
- Do not make `IPublishProjection` public until Bukit has a stable projection plugin ABI that does not expose internal engine implementation types.
