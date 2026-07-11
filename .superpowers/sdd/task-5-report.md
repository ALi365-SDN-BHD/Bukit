# Task 5 Report: Make provider collection projection explicit

## Status

Implemented explicit, independent type and collection projection for Markdown,
Notion, and composite content-provider flows within the Task 5 scope.

## RED evidence

- The first five-behavior run produced 4 expected failures and 1 pass:
  - Markdown did not inject `defaultType` when front matter already declared a
    collection.
  - `NotionPropertyParser` had no canonical Collection extractor.
  - restrictive Notion whitelist projection dropped canonical Collection.
  - multi-select Collection had no clear rejection path.
  - the existing Composite clone behavior already preserved type and explicit
    target collection, so its characterization test passed.
- Read-only diff review then found that CLR string-shape detection could accept
  a single file URL as Collection and silently ignore an empty multi-select.
  Two added edge tests both failed because no `ContentException` was thrown.

## GREEN evidence

- New behavior tests: 5 passed, 0 failed before reviewer follow-up.
- `NotionPropertyMapTests` after reviewer follow-up: 14 passed, 0 failed.
- Final focused provider/parser run:
  `MarkdownFolderProviderTests`, `NotionPropertyMapTests`,
  `NotionContentProviderEndToEndTests`, and
  `CompositeContentProviderTests`: 92 passed, 0 failed.
- Final full `Bukit.Content.Tests`: 662 passed, 0 failed.

## Gate evidence

- Task 5 path-scoped `git diff --check`: passed.
- `bash scripts/checks/post-change-targeted.sh -- <Task 5 source/test paths>`:
  passed after the final review fix.
- The targeted gate ran fast contracts, documentation consistency checks,
  post-change self-tests, and `Bukit.Content.Tests` in Release; the Release test
  result was 662 passed, 0 failed.
- No full, release, whole-solution, or backup/reference gate was run.

## Changed files

- `src/Bukit-Core/Bukit.Content/Markdown/MarkdownFolderProvider.cs`
- `src/Bukit-Core/Bukit.Content/Notion/NotionContentProvider.cs`
- `src/Bukit-Core/Bukit.Content/Notion/NotionPropertyParser.cs`
- `tests/Bukit.Content.Tests/MarkdownFolderProviderTests.cs`
- `tests/Bukit.Content.Tests/NotionPropertyMapTests.cs`
- `tests/Bukit.Content.Tests/NotionContentProviderEndToEndTests.cs`
- `tests/Bukit.Content.Tests/CompositeContentProviderTests.cs`
- `.superpowers/sdd/task-5-report.md`

## Commit

`fix(content): make collection projection explicit` (this Task 5 commit)

## Self-review

- Markdown `defaultType` now defaults only missing `type`; it never creates or
  replaces collection.
- Explicit Markdown type and collection remain independent.
- Notion Type and Collection are extracted independently from their mapped raw
  properties before ordinary whitelist filtering.
- Collection accepts only single-value `rich_text`, `select`, or `status`
  Notion properties. `title`, `url`, `email`, `phone_number`, `formula`, and
  multi-valued property types throw a clear `ContentException` identifying the
  property type and allowed types.
- Composite `source.collection` remains the final collection override and never
  changes type. `addToCollections` clone logic was not modified; tests prove
  clones preserve type and carry their explicit target collection.
- The bounded read-only rereview returned PASS with no remaining P0/P1/P2.
- Concurrent Config, dataIndex, docs, Engine, Rendering, and backup/reference
  changes were not modified or included in this commit.

## Concerns

None within Task 5 scope. The implementation depends on the concurrent Config
surface that provides `NotionPropertyMapConfig.Collection`; that surface built
successfully in both focused and targeted-gate runs and is intentionally not
part of this commit.
