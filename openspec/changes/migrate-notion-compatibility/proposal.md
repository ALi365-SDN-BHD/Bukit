# Migrate Notion compatibility ownership

## Why

The separately approved 2026-10-25 ITEM1 removes the remaining compatibility
shells and consolidates the current canonical Notion content implementation in
Bukit.Content. The owner explicitly accepted parser deletion, assembly moves,
removal of the old DLL/project, and the binary break without forwarding.
This task is independent of the built-in-plugin split.

## What changes

- Move the 21 current canonical C# files to `Bukit.Content/Notion`, retaining
  their namespaces and behavior; remove `Bukit.Content.Notion.csproj`.
- Remove `NotionApiClient`, `NotionCompatibilityQueries` (including its page
  carrier), `NotionContentProvider`, `NotionPropertyParser`, and
  `NotionProviderOptions`; add no shell, forwarder, or public parser replacement.
- Move public `NotionContentSource` and `NotionContentSourceOptions` from
  Bukit.Content.Notion to Bukit.Content. The source implements IContentProvider
  and explicitly implements the existing internal fallback-provider contract.
- Retarget direct Engine/test consumers, project/friend edges, owner mappings,
  active migration guidance, and the governed API inventory (426→425 / 19→18).

## Boundary

This is 2.0-only; 1.x remains unchanged. Unknown private, unindexed, binary-only,
reflection and serializer-bound consumers must migrate/recompile; search silence
does not prove safety. No general-purpose CLR SDK is promised. The other 423
governed type entries and options' 31 public members remain unchanged.
No logger rollback, Theme/Labs change, new dependency, runtime algorithm change,
live Notion access, publication, or full/release/solution gate is included.
