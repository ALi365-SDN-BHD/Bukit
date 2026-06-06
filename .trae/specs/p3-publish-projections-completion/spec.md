# P3 Publish Projections Completion Spec

## Goal

Complete the P3 publishing layer by turning the existing ad hoc content projection output into a first-class projection contract, registry, and auditable representation inventory.

## Current State

- P1 canonical content graph exists through `ContentRecord`, `EntityRecord`, `ContentRelation`, provenance, trust, and media records.
- P2 machine readability and trust audit exists through `PublishDocument`, `PublishAuditReport`, and `MachineReadabilityTrustAuditBuilder`.
- P3 has partial output support through `ContentProjectionWriter`, which writes `content/*.json`, `content/*.md`, and `agent-manifest.json`.

## Required Behavior

- The engine exposes a projection contract with explicit document and aggregate projection metadata.
- Built-in JSON, Markdown, and agent manifest projections execute through `IPublishProjection.Project(PublishProjectionContext)` and return generated output inventory.
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
