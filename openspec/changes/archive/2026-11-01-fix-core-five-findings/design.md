# Design

## Context

Baseline `de9a5ebf`. The plan records three audit rounds (first 1-6, re-review
1-2, third round 1-11). This change carries the design that closes them; runtime
proof is recorded in `tasks.md` and external evidence while implementation runs.

## Goals / Non-Goals

**Goals:** stop original-manuscript deletion, bind deployment identity and
transport to the configured project, keep one Notion load resource set, make
privacy scanning complete-or-failed, and make incremental save/compare agree
after fingerprint correctness is established.

**Non-Goals:** new public options or interfaces, new report schema fields,
raising the JSON depth limit, staging arbitrary injected provider writes,
rewriting the public API drift baseline, or running full/release gates.

## Decisions

### 1. Source protection uses real, knowable roots

`BuildTransaction` accepts an optional explicit source-root list and extra known
write targets. `null` means "resolve from effective config"; an explicit empty
list means "no recognizable local root" and never falls back to the synthetic
`content` source. Injected `MarkdownFolderProvider` exposes its real
`ContentDir`; `CompositeContentProvider` exposes its children recursively;
`NotionContentSource` exposes its writable cache only when the effective mode is
`readwrite` with a non-empty directory, resolved by provider semantics
(relative cache paths use the process working directory). `NotionContentSource`
also exposes the writable cache as an extra write target.

A shared check in `OutputDirectoryCleaner` compares canonical paths by equality
and both containment directions, using existing normalization/symlink/case
resolution. The CLI clean command checks output and `.cache` against configured
Markdown roots before deleting anything. `WorktreeBuildRun.StartAsync` checks
the original project's configured Markdown roots against the formal output,
cache, media, metrics JSON/HTML, and configured Notion readwrite caches before
the run lock, run directory, staging, or workers.

### 2. Deployment identity and transport share one effective config context

The provider gains an internal overload carrying the project root; the public
`DeployAsync(DeployContext, CancellationToken)` delegates with the current
working directory. Origin resolution reads
`git config --worktree --includes --null --get-all remote.origin.url` and falls
back to `--local` only on exit code 1. The first raw value is authoritative; an
empty, invalid, or non-GitHub first value fails without consulting later values,
other scopes, or global/system origin. Success produces one canonical
`https://github.com/{owner}/{repo}.git`.

Every temporary git process removes inherited repository-location, index and
object overrides plus runtime config injection, while preserving normal
credential, proxy and SSH settings. The temporary checkout is initialized first
and then receives a private remote with the canonical URL. Only after that does
the preflight query effective URL rewrites in the same working directory,
environment and command-line config. Any rewrite value that prefixes the
canonical URL is rejected, as is any private-remote fetch/push URL mismatch.
The same preflight runs before each `ls-remote`, fetch, and push. Branch queries
and fetch/checkout/push use the private remote; no clone or `-c` injection
recreates the config context.

### 3. One Notion load owns one client, one body gate, and one resolver

`LoadRawAsync` creates the client once and reuses it for database query, taxonomy
relation resolution, summary prefetch, cross-source relation fallback, and lazy
body rendering. A per-load `SemaphoreSlim` bounds active body renders using the
effective `RenderConcurrency`. Each page still gets its own renderer. The body
store owns the client, gate, and prefetch seeds; disposal closes admission,
drains accepted operations, cancels/reaps background tasks, then disposes the
client and gate. Successfully prefetched empty or whitespace HTML is seeded
before summary extraction so cache-off reads are not repeated.

The body store exposes its load-bound relation resolver through an internal
contract; `CompositeContentProvider` prefers the resolver carried by a result and
keeps the existing fallback for other providers. `NotionRelationResolver`
observes every started task before propagating a scheduling exit, so resources
are released only after started requests settle. Failed shared body tasks are
removed by key/entry identity even without waiters, so a later same-key read
starts a fresh attempt. The final `CompositeContentBodyStore` construction is
inside the cleanup region and disposes already-completed child stores exactly
once on failure.

### 4. Privacy scanning is complete-or-incomplete, never silently clean

`PublicOutputPrivacyCheck` exposes one internal JSON scanner returning
`Clean`, `Leak`, or `Incomplete`. It decodes once, checks string values, property
names, arrays, and top-level strings against the caller's token set, keeps the
`source`/`sourceKey == notion` marker, and maps `JsonException` plus
`InvalidOperationException` from the narrow string/property-name reads to
`Incomplete`. The original-text scan still runs first; a text hit is a leak.
Incomplete results become `publicOutputPrivacy=failed` with a redacted,
non-leak diagnostic; warn continues, strict blocks, and deployment stops before
Git add/commit/push with `BKT-DEPLOY-PRIVACY-0001`. No exception message or raw
input is emitted.

### 5. Incremental identity is corrected before it is unified

Notion `bodyFingerprint` gains a delimited effective `RenderContent` marker.
Markdown production and body loading share one body-only fingerprint helper;
the body store verifies the captured fingerprint on the same safe-opened string
it renders and fails before returning the body, updating the manifest, or
committing. `PageRenderDispatcher` then always attempts the existing stable
fingerprint selector when saving the manifest and falls back to the loaded HTML
hash only when no stable fingerprint applies, matching the comparison side.

## Risks / Trade-offs

- Whole-root protection is deliberately coarse; `includePaths`/`includeGlobs`
  do not narrow it. Migration is to a dedicated content directory.
- Origin and rewrite rules are intentionally strict; aliases must become direct
  GitHub URLs, and matching rewrites must be removed.
- Undecodable or over-deep public JSON now fails the privacy check in every mode
  that reports failures; warn still completes the build with a failed report.
- Markdown body changes mid-build fail instead of publishing mixed content.

## Verification

Closure, classification, cache records and metrics use
`scripts/checks/codex-workflow.py`. Runtime specialties are the five project test
commands, API-01 (Core CLI Debug build) and API-02 (absolute-root public API
drift check), report-inventory self-test, workflow self-test, and the change's
strict OpenSpec validation. API-02 runs only after API-01 with no source writes
in between.
