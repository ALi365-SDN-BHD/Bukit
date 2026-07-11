# Task 10 Report

## RED Baseline

A fresh-context read-only reference check could not answer from the original
project skills:

1. whether `markdown.defaultType=article` without collection can build;
2. which keys route, list, and RSS use for `type=article, collection=news`;
3. whether Notion canonical Collection bypasses the ordinary whitelist and
   which scalar shapes it accepts.

## GREEN Changes

- Defined independent type and collection defaults and ownership rules.
- Documented the strict content collection failure boundary and data-mode
  exception.
- Defined route priority, distinct placeholders, and downstream consumer keys.
- Documented Markdown, Notion, source override, and `addToCollections`
  projection behavior.
- Reworked the three project skills as concise reference contracts covering
  the baseline A/B/C scenarios.

## Verification

- `dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj --no-restore
  --filter "FullyQualifiedName~DocsCheck|FullyQualifiedName~Guide"`: 26/26 passed.
- `bash scripts/checks/post-change-targeted.sh -- guide`: passed, including
  docs consistency, skills schema/strict validation, README sync, and Core CLI
  contract checks.
- Required ownership/default, routing/provider/Notion, and `addToCollections`
  scoped searches: passed.
- Forbidden compatibility, migration, warning-only, and type/collection
  derivation wording search: no matches.
- `git diff --check`: passed.

## Fresh-Context GREEN Audit

Two read-only fresh-context checks (the second after the final skill wording
refactor) answered all A/B/C scenarios from the three project skills. Final
result: GREEN, no contradictory or ambiguous contract. The reviewer confirmed
the SEO statement remains limited to Article/BlogPosting decisions and does
not claim that every SEO behavior is keyed by type.
