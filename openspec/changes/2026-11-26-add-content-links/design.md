# Design

## Decisions (confirmed by the user, 2026-11-26)

1. **Configuration**: a new `content.links` list, instead of turning the validation-only
   `customFields[].reference` rule into a resolver. The C# types are internal, so the frozen
   public API baseline (426 types, guarded by architecture tests) does not grow.
2. **Output**: a sibling field `<field>_links` holds a list of sorted maps:
   - `key`, `title`, `slug`, `url` and `collection`, plus the `select`ed top-level target fields.
   - `url` is null for a data document.
   - Links are not followed recursively.
   - A nested path writes `<last>_links` inside each object that holds the key field. It also
     writes a top-level `<names joined by _>_links` that keeps the first occurrence of each target.
3. **Conflicts**: an existing output field is never overwritten. It is reported as
   `content.link_output_conflict`.
4. **Timing**: Bukit resolves links in two steps.
   - Before routing, `ContentGraphValidateStage` checks keys once per site language and reports issues on the linking page.
   - After routing, `RoutePipeline` writes the links into the routed pages of one language, before
     the list route graph and plugins read the pages. Unresolved keys are left out.
   - Because the links live in page fields, the page metadata hash covers target changes, and no
     render dependency needs registering.
5. **Missing targets**: `content.link_target_unresolved` is reported under the page collection's
   `schemaFailMode`. It does not reuse `requireRelationTargets`, which belongs to the canonical graph.
   - Keys must be text, or `content.link_value_invalid` is reported.
   - Keys match case-insensitively.
   - A key repeated in one language is reported as `content.link_target_duplicate`, and the first
     document wins.
6. **Scope**: only collection targets. A data target is allowed and has a null `url`.

## Languages

Routing keeps only the pages built for one language, and data documents whose `locale` is blank or
matches. Links follow the same filter, so a page never links to another language's document.
Validation applies the same filter (`I18nOutputMerger.FilterDocumentsByLanguage`) for each site
language. A key that exists only in another language is therefore reported as unknown. A repeated
key is reported only when two documents of the same language view hold it: a blank-locale data
document belongs to every language and an undeclared page to the default language. The problem is
reported on the page that names the key, so the page collection's `schemaFailMode` applies, and a
duplicate that no page names is not reported.

## Keys

`slug`, text, or a whole number: `long`, an integral `decimal`, or an integral `double` up to 2^53
(Notion numbers). These are written as invariant digits. A fractional number is not a key.

## Copying

Structured containers on the path are copied, keeping their map type and key comparer. The source
document's maps are never changed. Containers off the path are shared.
