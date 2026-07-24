# Bukit Core Product Positioning

> Effective: 2026-07-24
>
> Current mode: enterprise internal-first

## Decision

Bukit Core adopts Route 2 as its product direction and Route 3 as its current
operating mode:

- **Route 2 - deterministic trusted-content publishing compiler:** invest in
  deterministic builds, Markdown and Notion ingestion, canonical publishing
  contracts, routing, rendering, SEO/GEO, audit evidence, and Native AOT.
- **Route 3 - internal stable engine:** prioritize controlled enterprise use,
  existing internal sites, reliability, observability, and bounded maintenance
  over general-purpose SSG expansion.

Route 1, broad general-purpose SSG competition, is not a current objective.

## Internal-First Status

Enterprise-owned publishing projects are the priority consumers. New Core
capability requires a named internal consumer, a concrete business case, an
owner, and verifiable acceptance criteria.

“Stable” means a governed internal Core contract. It does not mean commercial
support or a public compatibility SLA.

## Public Repository And External Use

The repository and its existing open-source license remain public. External
parties may inspect, fork, build, or use Bukit under that license, but use is
self-directed. The project currently provides no public support commitment,
SLA, compatibility guarantee, product-readiness promise, or fixed release
cadence.

## Release Policy

Regular public binary releases are paused. Internal builds, internal
deployments, and internal release-candidate validation may continue.

An exceptional public release requires explicit management approval plus all
applicable technical release evidence. Passing a build, CI, release workflow,
or artifact gate proves technical state only; it does not authorize
publication.

## Scope

This policy applies to Bukit Core. Labs, Import, WeChat, and other
external-plugin business implementations are not promoted to Core and are not
made release-ready by this policy. Their own audits and maturity decisions
remain authoritative.

## Investment Rules

- Fix correctness, safety, contract truth, and observability before adding
  features.
- Prefer controlled simplification over public-surface expansion.
- Do not add general-purpose integrations without proven internal demand.
- Freeze or remove unused capability through versioned governance when its
  internal value is not demonstrated.

## Historical Evidence

Historical audits, plans, release records, version strings, and artifact
evidence retain their original meaning. This document governs current product
operations without rewriting those records.
