# Fix five verified Bukit Core findings

## Why

Five verified Core findings can destroy original manuscripts, deploy to the wrong
repository, reset Notion rate limits, leak Unicode-escaped internal identifiers
through privacy checks, and force extra incremental renders. The user approved
this Core task and the remediation plan
`2026-11-01-bukit-core-five-findings-remediation-plan.zh-CN.md`; implementation
must follow that plan and only its mapped closure.

## What Changes

- Protect real local content sources from the managed build/clean write set:
  configured Markdown roots, injected built-in Markdown/Composite roots, injected
  built-in Notion writable caches, and the explicit multilingual CLI worktree
  entry, evaluated against the original project's formal output/cache/media/
  metrics targets before locks, staging, caches, or workers.
- Bind GitHub Pages deployment identity and transport to the configured project:
  local/worktree-scoped origin, no inherited repository-location or runtime
  config overrides, private remote created before the preflight, and rejection of
  matching `insteadOf`/`pushInsteadOf` rewrites before any transport.
- Treat every JSON parse or string-decode failure as an incomplete privacy scan on
  both scanner sides; JSON string values, property names, arrays, and top-level
  strings are checked after decoding, and incomplete scans fail strict builds,
  are reported as failures in warn builds, and block deployment.
- Scope Notion rate limiting, body concurrency, relation resolution, cross-source
  lookup, and lazy body rendering to one source load; close every task on every
  exit path, clear failed cached body tasks by identity, dispose composite child
  stores on final construction failure, and reuse successfully prefetched empty
  bodies.
- Save the same stable content hash that incremental comparison uses, after
  adding the effective Notion `RenderContent` marker and verifying the Markdown
  body fingerprint against the body actually rendered.

## Impact

- Affected specs: `in-process-build-transactions`, `i18n-worktree-build`,
  `core-deployment-identity`, `core-report-contracts`,
  `notion-load-resource-lifecycle`.
- Affected code: Engine, Content, Content.Notion, CLI deploy/clean/worktree, and
  Engine.Audit privacy scanning. Public API signatures stay compatible; internal
  overloads and internal provider members carry the new context.
- Compatibility tightening (intentional): whole-source-root protection rejects
  `markdown.dir: "."` combined with in-project output/cache; global origin
  fallback, URL-rewrite-dependent origins, and inherited Git repository/config
  overrides no longer work for deployment; unparseable or undecodable public JSON
  fails the privacy scan; a Markdown body change between metadata and body reads
  fails the build before commit.
- Hidden disk roots of arbitrary custom providers or opaque wrappers are outside
  the recognizable source guarantee; they keep the prior output safety rules and
  are not claimed as protected.
- Not in scope: full/release gates, commits, pushes, deployments, Notion writes,
  new configuration options, schema changes, or changes to protected reference
  areas (`guide-0.1/`, `guide-0.2/`, `scripts-0.1/`, `scripts-0.2/`).
