# Tasks

- [x] 1 Move the JSON canonical rules into `Bukit.Content.CanonicalJsonValue`; `DataIndexObjectValue` uses them.
- [x] 2 Project jsonb objects and non-text arrays as structured fields; keep text arrays unchanged.
- [x] 3 Remove hard-excluded names from nested object keys.
- [x] 4 Enforce depth 8 and 256 KiB; report rejected fields as `cloudbase.structured_field_invalid` error diagnostics.
- [x] 5 Drop fields with nested `cloud://` outside `fileFields` and count them in `CloudBaseUnsupportedFileReference`.
- [x] 6 Tests in `Bukit.Content.Tests` (projector and content source).
- [x] 7 Document the behaviour and the type difference from Markdown; CHANGELOG `[Unreleased]` in three languages.
- [x] 8 Log CloudBase rows only by hash in content validation logs (D7); no diagnostic for structured file fields; objects empty after dropping personal keys are absent.
- [x] 9 Render test for the documented Scriban forms (`Bukit.Rendering.Tests`).
