# Design: direct typed fallback error classification

## Root cause and repair

`NotionContentClient.GetAsync` preserves canonical `NotionApiException` as the direct inner exception of `ContentException`. `SourceBoundedRelationFallbackResolver.ResolveAsync` currently recognizes only the unwrapped type. Extend only its existing permission-status catch filter with a typed OR pattern: direct canonical 403/404, or `ContentException` whose direct inner canonical exception has status 403/404.

Keep caller cancellation catch first and unchanged. Keep the generic warning and `notion.relation.unresolved` branch unchanged for 401, 500 and other failures. Do not unwrap recursively, parse messages, change client wrapping or introduce a production helper/seam.

## Projection and security boundaries

The real Source-to-Composite regression retains ordered duplicate relation links, loaded target fields/reference aliases, unresolved null fields, exact deduplicated source-bound requests, canonical host and source authorization checks, and body-store disposal. Only missing-target status/error JSON varies across 403, 404, 401 and 500. Diagnostics must not expose body/token sentinels. One real source fallback cancellation regression cancels an in-memory blocked HTTP request after it starts and checks the original caller token.

## Verification and evidence

Before production edits, run the complete frozen Content.Notion selection and require assertion RED only for 403/404. Before adding the one exact first policy rule, run the complete workflow owner with the four new mapping assertions and require unmapped-path assertion RED. Preserve every existing policy rule/value and mapping assertion.

Freeze the eight-file delta against this task's immutable `before/`, not the whole HEAD migration diff. Rebuild the CLI and two affected test projects serially, then run the frozen workflow owner, Content.Notion, Content and direct API specialties serially. Each GREEN observes its own actual consumed inputs, membership, copied binaries, environment states and toolchain and uses the unchanged official cache CLI. CLI build inputs/outputs belong to the separate complete task closure, not unrelated test caches. The four source-only contracts remain explicit final-review inputs.

Public baseline bytes and protected predecessor/main/ITEM3/legacy states must remain unchanged. Parent acceptance includes one post-GREEN specialty review and one generated delta-only final review; no duplicate historical audit or unscheduled gates. Future observed verification outcomes remain in external evidence rather than mutating frozen task files.
