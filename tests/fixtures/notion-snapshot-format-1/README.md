# Notion snapshot format 1 compatibility fixture

This fixture proves that a committed `format=1` Notion offline snapshot stays readable as Bukit
changes. It is used by `tests/Bukit.Engine.Tests/NotionSnapshotCompatibilityTests.cs` and by the
CLI smoke test in `tests/Bukit.Cli.Tests/NotionOfflineSnapshotCommandTests.cs`.

## Layout

- `api/` — the hand-written Notion API responses, which are the source of truth for this fixture:
  - `databases/<id>/schema.json`
  - `databases/<id>/queries/first.json` and `databases/<id>/queries/<cursor>.json`
  - `pages/<id>.json` and `pages/<id>.404.json`
  - `blocks/<id>.json`
- `site/` — one minimal site using that API data: two Notion sources (articles and people), a
  cross-source relation, a taxonomy, `pages_index`, and a small local theme. Media is not
  downloaded (`media.downloadToLocal: false`); the media cache is not part of a Notion snapshot.
- `snapshot/` — the committed `format=1` snapshot recorded from `site/` and `api/`.

The fixture snapshot records both databases, the query results, the cross-source target, the
recorded 404 of the missing relation target, the successfully fetched `pages_index` menu page, the
taxonomy options, and the rendered article body. `api/pages/article-2.json` is a draft kept in the
raw data; the filtered query's second page returns no results, so the draft is neither snapshotted
nor published. The compatibility test asserts that no draft page appears in the output and that
the snapshot query request carries the `Published` filter. The manifest's `bukitVersion` is a synthetic `2.0.0`, chosen only to differ from any current Bukit
build (including builds without a git source revision, whose version is `2.1.0`); format 1 itself
was introduced in 2.1.0, so 2.0.0 did not really write this snapshot. The body entries have
`version = NotionCacheManager.CacheFormat (2)` and
`renderer = NotionBlocksRenderer.ContractVersion (1)` at the time of recording.

## Regenerating

The generation test returns early unless `BUKIT_GENERATE_SNAPSHOT_FIXTURE=1` is set (xUnit 2
reports that as passed, not skipped). Run it only when intentionally regenerating the fixture:

```sh
BUKIT_GENERATE_SNAPSHOT_FIXTURE=1 dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj \
  --filter "FullyQualifiedName~NotionSnapshotCompatibilityTests.GeneratesTheFixtureSnapshot"
```

It builds `site/` with `api/`, writes the snapshot next to `snapshot/`, rewrites the manifest's
`bukitVersion` to `2.0.0` and `createdAt` to a fixed time, then swaps it in while keeping the old
directory until the move succeeds. Do not edit the generated snapshot by hand.

## When the fixture may change

- **Fixture data written wrong** (a typo in `api/`, a missing page): fix `api/`, regenerate
  `snapshot/`, and record what and why in this README. This is not a snapshot format change.
- **The meaning of the snapshot data changes** (an entry's meaning, the Notion API version used by
  `NotionApiUrls.NotionVersion`, or the snapshot directory layout): do not keep format 1. Bump
  `NotionSnapshotSession.FormatVersion`, create `tests/fixtures/notion-snapshot-format-2/`, delete
  format 1, and add a test that format 1 is rejected as `BKT-0B10`.
- To decide which case applies, regenerate with the Bukit before the change and with the changed
  Bukit. Identical snapshots mean a fixture-data bug; different snapshots mean a format change.
  A new renderer contract changes only the rendered bodies; the compatibility test compares them
  separately and does not require a format bump.

Never regenerate `snapshot/` merely to make a failing compatibility test pass. The offline and
online builds use the same site and the same raw `api/` data; parser or template changes affect both
sides and do not need a fixture update.
