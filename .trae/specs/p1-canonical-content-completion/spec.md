# P1 Canonical Content Completion Spec

## Goal

Complete the P1 canonical content migration so content semantics flow through
`ContentRecord` / `CanonicalContentGraph` first, while `ContentItem.Meta` remains
only a raw provider input and compatibility fallback.

## Current State

- Canonical types already exist in `src/Bukit.Engine.Abstractions/CanonicalContent.cs`.
- `ContentPipeline` already returns `ContentGraph`.
- SEO, search, rendering, audit, and projection paths already consume canonical
  records in part.
- Remaining direct `Meta` reads are concentrated in taxonomy, related content,
  feeds, LLMS output, SEO compatibility parsing, and provider raw-input logic.

## Non-Goals

- Do not introduce a second canonical model.
- Do not add CLI commands.
- Do not add NuGet dependencies.
- Do not expand P2/P3 output formats beyond consistency fixes for existing
  publish audit and projection outputs.

## Compatibility Strategy

Use this precedence for content semantics:

1. Structured `ContentField` value.
2. Normalized provider field value.
3. Legacy `Meta` fallback.

Direct `Meta` reads may remain in provider ingestion, legacy configuration, and
explicit fallback paths such as `sourceKey`, pinning fields, `geo`, and
`searchExclude`.

## PR Split

This spec is implemented as small PR-sized slices. Each slice must follow TDD and
keep the diff below the project rule threshold where practical.
