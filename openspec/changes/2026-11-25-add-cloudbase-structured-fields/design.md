# Design

## Decisions (confirmed by the user, 2026-11-25)

1. **Types**: CloudBase keeps JSON types; it does not turn numbers into strings. The difference
   from Markdown front matter (string list items) is documented.
2. **Whitelist**: the whitelist names top-level columns; an allowed column is kept whole. The
   hard-excluded names apply recursively to nested object keys. No `allowedPaths`.
3. **`cloud://`**: any `cloud://` in a structured value outside `fileFields` drops the whole field
   with a diagnostic (D17 granularity).

## Shape

- A structured field is `ContentField("list", value)`, the type Markdown front matter gives nested
  maps and lists. `value` is `SortedDictionary<string, object?>` (ordinal) or `List<object?>`.
- Top-level scalars keep their current types (`long` or `double`, `check`, `text`) so existing
  schemas and templates are unaffected; nested numbers use the canonical `long`/`decimal`/`double`
  order so `12.50` keeps its digits.
- The canonical conversion is `CanonicalJsonValue.FromJson`, shared with data index `object`
  values. It lives in `Bukit.Content` because `Bukit.Engine` references `Bukit.Content`, not the
  other way; `Bukit.Engine` is already a friend assembly, so the friend set is unchanged.

## Limits and diagnostics

- Depth: every object and list is one level, maximum 8 (same constant as data index values).
- Size: raw UTF-8 JSON of one field, maximum 256 KiB, measured with `JsonMarshal.GetRawUtf8Value`
  (no copy). A page response is already capped at 8 MiB by the transport.
- A rejected field is dropped and recorded as a `RawContentDocument` diagnostic with severity
  `error`; `ContentGraphValidateStage` applies `schemaFailMode` per collection, the same path as
  other schema errors.

## Consumers checked

Scriban (`ScribanDynamicValueMapper`, `TemplateJsonWriter`), search index, pages index,
mini-program data (`MiniProgramDatasetBuilder`), incremental hashing (`RenderDependencyHashWriter`,
`IncrementalBuildEngine`, `SignedLinkDiagnostics`; `RenderReadRecorder` only records reads) and
media output handle sorted maps and typed leaves. Scriban's `array.contains` compares with
`Equals`, so a 64-bit element never matches the 32-bit literal `4`; templates compare with `==`
(covered by `StructuredFieldTemplateTests`). Readers that expect text
(`ContentFieldReader.GetText`/`ToTextList`, used by SEO, RSS and llms.txt) format a map as its type
name when a scalar field such as `seo_title` or `image` holds an object; this already happens for
Markdown maps and is recorded as a follow-up, not changed here.
