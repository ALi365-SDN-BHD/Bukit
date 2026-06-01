# Checklist

- [x] GetBlockChildrenIdsAsync returns `Task<(bool, List<string>)>` with success flag
- [x] GetBlockChildrenIdsAsync returns `(false, ids)` when mid-pagination HTTP fails
- [x] DeleteBlockAsync returns `Task<bool>` (not void)
- [x] DeleteBlockAsync reads response status and returns true/false
- [x] PushAsync replace: read-failed → `"replace-failed"` item added, `continue` executed
- [x] PushAsync replace: delete-failed → `"replace-failed"` item added, `continue` executed
- [x] PushAsync replace: all succeed → append runs, `"updated"` item added
- [x] Test: `NotionPush_AppendFailed_MarksAppendFailed` exists and passes
- [x] Test: `NotionPush_ReplaceFailed_MarksReplaceFailed` exists and passes
- [x] `dotnet test` — all tests pass with 0 failures (3,306 passed)
