# Keep structured fields in the CloudBase source

## Why

`CloudBaseFieldProjector` keeps only strings, numbers, booleans and string arrays. A jsonb object,
and every non-string element of an array, is dropped. Markdown front matter already supports
nested objects and lists of objects, so the two sources disagree. travelWeb, the named consumer
(roadmap `docs/plans/2026-11-25-generic-data-derivation-roadmap.zh-CN.md`, item F1), needs route
itineraries (`object[]` with `title`, `text`, `attractions[]`), recommended months (`[3,4,5]`)
and a contact object straight from CloudBase views. F2 (reference fields with nested paths)
depends on this item.

## What Changes

- A jsonb object, or an array holding anything other than strings and nulls, becomes a
  structured field in the canonical form already used by data index `object` values: ordinal
  sorted keys, JSON types kept (`long`, then `decimal`, then `double`; booleans; null), at most
  8 nesting levels, and at most 256 KiB of raw JSON per field.
- Arrays of strings keep today's behaviour (blank items and nulls dropped; empty means absent).
  An empty object is absent like an empty array.
- Types are kept: `[3,4,5]` stays numbers (whole numbers as 64-bit). Scriban's `array.contains 4`
  does not match them (the literal is 32-bit and the comparison is exact), so the guide shows `==`;
  Markdown front matter list items are strings and need `"4"`.
- Privacy: an allowed top-level column is kept whole. The hard-excluded names (`user_id`,
  `owner`, `created_by`, `updated_by`, `_openid`) are also removed from the keys of every nested
  object. No `allowedPaths`.
- `cloud://` anywhere in a structured value (or a nested key) outside `fileFields` drops the whole
  field and reports `CloudBaseUnsupportedFileReference`, as D17 does for top-level values.
- A field that is too deep, too large or otherwise invalid is dropped with an error-severity
  content diagnostic `cloudbase.structured_field_invalid`, so the collection's `schemaFailMode`
  decides: `off` ignores it, `warn` logs it, `strict` fails the build. The message names key paths,
  never values; the content validation log names CloudBase rows only by a hash of the id (D7),
  which also fixes the same log line for every other validation issue on CloudBase documents.
- The JSON-to-canonical rules move from `Bukit.Engine.DataIndexObjectValue` into the shared
  internal `Bukit.Content.CanonicalJsonValue`; data index behaviour and messages are unchanged.

## Impact

- Affected specs: `cloudbase-content-source` (one added requirement).
- Affected code: `Bukit.Content/CloudBase` (projector, content source), new
  `Bukit.Content/CanonicalJsonValue.cs`, `Bukit.Engine/DataIndexObjectValue.cs`,
  `Bukit.Engine/Stages/ContentGraphValidateStage.cs` (log hashing); tests in `Bukit.Content.Tests`,
  `Bukit.Engine.Tests` and `Bukit.Rendering.Tests`.
- With `fieldPolicy.mode: all`, columns that earlier builds dropped now appear; sites using
  `content.modelSchema.rejectUnknownRawKeys` must declare them.
- No public API change: every changed type is internal, and the diagnostic is a content
  diagnostic code string, not a new `DiagnosticCode` member.
- D11 still only backs up CloudBase identifiers (envId, `cloud://`, gateway host, signed links);
  it does not detect personal data, so nested personal data is kept out only by the whitelist and
  the hard-excluded names.
