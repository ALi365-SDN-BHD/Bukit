# mediaCopy Skip-Unchanged Design

## Background

`mediaCopy` currently copies files from `ctx.MediaDownloadDir` into `assets/uploads` with unconditional overwrite. This differs from the existing `assetsSync` path, which already skips unchanged files by comparing file length and `LastWriteTimeUtc`.

The current behavior is correct but wastes I/O on repeated builds, especially when downloaded media files are stable across runs.

## Goal

Optimize `mediaCopy` so unchanged files are skipped instead of being overwritten on every build.

## Non-Goals

- Do not change the output path layout for media files.
- Do not change the current top-level-only copy behavior into recursive directory sync.
- Do not introduce content hashing for this iteration.
- Do not redesign incremental build manifest behavior for media files.

## Constraints

- Preserve existing external behavior except for avoiding redundant copies.
- Keep the stage metric name `mediaCopy` unchanged.
- Reuse existing repository patterns where possible.
- Prefer metadata-based detection using file length and `LastWriteTimeUtc`.

## Options Considered

### Option A: Inline timestamp check inside `SiteEngine`

Add the skip logic directly in the current `mediaCopy` loop.

Pros:

- Smallest code diff.

Cons:

- Duplicates logic that already exists in `DirectoryCopy`.
- Leaves asset sync behavior split across multiple implementations.

### Option B: Reuse recursive `DirectoryCopy.Sync` directly

Replace the current loop with a direct call to `DirectoryCopy.Sync`.

Pros:

- Maximum reuse of existing logic.

Cons:

- Changes behavior from top-level-only to recursive sync.
- Risks copying files from subdirectories that are currently ignored.

### Option C: Add a focused helper for top-level file sync with skip-unchanged behavior

Extend `DirectoryCopy` with a helper dedicated to syncing only files in a single directory while reusing the same skip rule as `Sync`.

Pros:

- Preserves current `mediaCopy` behavior.
- Aligns media copy logic with existing asset sync semantics.
- Keeps future extension points open for hash-based comparison if needed later.

Cons:

- Slightly more code than the inline change.

## Recommended Design

Use Option C.

Add a new helper in `DirectoryCopy` that:

- Enumerates only top-level files from a source directory.
- Optionally ignores dot-prefixed file names.
- Skips copy when destination file exists and both `Length` and `LastWriteTimeUtc` match the source file.
- Copies the file otherwise.
- Sets the destination `LastWriteTimeUtc` to the source value after copying.

Update `SiteEngine` so the `mediaCopy` stage calls this helper instead of manually iterating and unconditionally overwriting files.

## Proposed API Shape

The exact method name can be finalized during implementation, but the helper should express:

- single-directory file sync
- optional dotfile filtering
- skip-unchanged behavior consistent with `DirectoryCopy.Sync`

Example intent:

```csharp
DirectoryCopy.SyncFiles(
    ctx.MediaDownloadDir,
    mediaOutputDir,
    ignoreDotPrefixedFiles: true);
```

The final implementation may choose a different method name if it better matches the existing naming style.

## Behavior Specification

For each file directly under `ctx.MediaDownloadDir`:

- If the file name starts with `.`, ignore it.
- If the destination file does not exist, copy it.
- If the destination file exists and its file length and `LastWriteTimeUtc` match the source, skip it.
- If the destination file exists but either value differs, overwrite it.
- After copying, set the destination file `LastWriteTimeUtc` to the source file timestamp.

For directories under `ctx.MediaDownloadDir`:

- Ignore them for this iteration.

## Error Handling

Do not add new custom recovery behavior in this iteration.

The new helper should preserve the current failure model:

- missing source directory results in no-op
- ordinary file system exceptions continue to surface to the caller

## Testing Strategy

Add focused unit tests around the helper behavior. Cover:

- missing source directory is a no-op
- new file is copied
- unchanged file is skipped
- changed file is overwritten
- copied file gets source `LastWriteTimeUtc`
- dot-prefixed file is ignored when the option is enabled
- subdirectories are not copied by the top-level helper

If the final implementation touches `SiteEngine` in a way that benefits from a targeted integration-style test, keep it minimal and focused on behavior change rather than broad build coverage.

## Observability

Keep the `mediaCopy` stage timing metric unchanged so build performance before and after the change remains comparable in existing observability output.

## Risks

- Metadata-based comparison can theoretically miss a change if content, timestamp, and length all align by coincidence.
- Future callers might assume the new helper is recursive unless its naming is explicit.

These risks are acceptable for this iteration because they match existing repository trade-offs already used in asset sync.

## Acceptance Criteria

- Repeated builds no longer overwrite unchanged files in `assets/uploads`.
- `mediaCopy` still copies only top-level files from `ctx.MediaDownloadDir`.
- Dot-prefixed files remain ignored.
- Existing `assetsSync` behavior remains unchanged.
- Automated tests cover the new helper behavior.

## Follow-Up

If metadata-based skip detection proves insufficient in real-world usage, a future iteration can add an alternative comparison mode based on file hashing without changing the `SiteEngine` call site shape significantly.
