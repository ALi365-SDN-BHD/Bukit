# Bukit Core 2.0 Notion compatibility migration

This notice applies to Bukit Core 2.0-only. Bukit Core 1.x remains unchanged.
The changes below remove or narrow legacy CLR identities that
duplicated canonical Notion owners; they do not change the Notion wire API or
site-build behavior.

## Removed public identities

The following public identities are no longer exported in 2.0.

| Original assembly | Removed CLR identity | Source migration target |
|---|---|---|
| `Bukit.Shared.dll` | `Bukit.Shared.Notion.NotionApiUrls` | `Bukit.Notion.NotionApiUrls` in `Bukit.Notion.dll` |
| `Bukit.Shared.dll` | `Bukit.Shared.Notion.HtmlToNotionBlockConverter` | `Bukit.Notion.Conversion.HtmlToNotionBlockConverter` in `Bukit.Notion.dll` |
| `Bukit.Shared.dll` | `Bukit.Shared.Notion.NotionBlock` | `Bukit.Notion.Blocks.NotionBlock` in `Bukit.Notion.dll` |
| `Bukit.Shared.dll` | `Bukit.Shared.Notion.Heading1Block` | `Bukit.Notion.Blocks.Heading1Block` in `Bukit.Notion.dll` |
| `Bukit.Shared.dll` | `Bukit.Shared.Notion.Heading2Block` | `Bukit.Notion.Blocks.Heading2Block` in `Bukit.Notion.dll` |
| `Bukit.Shared.dll` | `Bukit.Shared.Notion.Heading3Block` | `Bukit.Notion.Blocks.Heading3Block` in `Bukit.Notion.dll` |
| `Bukit.Shared.dll` | `Bukit.Shared.Notion.ParagraphBlock` | `Bukit.Notion.Blocks.ParagraphBlock` in `Bukit.Notion.dll` |
| `Bukit.Shared.dll` | `Bukit.Shared.Notion.BulletedListItemBlock` | `Bukit.Notion.Blocks.BulletedListItemBlock` in `Bukit.Notion.dll` |
| `Bukit.Shared.dll` | `Bukit.Shared.Notion.NumberedListItemBlock` | `Bukit.Notion.Blocks.NumberedListItemBlock` in `Bukit.Notion.dll` |
| `Bukit.Shared.dll` | `Bukit.Shared.Notion.QuoteBlock` | `Bukit.Notion.Blocks.QuoteBlock` in `Bukit.Notion.dll` |
| `Bukit.Shared.dll` | `Bukit.Shared.Notion.ImageBlock` | `Bukit.Notion.Blocks.ImageBlock` in `Bukit.Notion.dll` |
| `Bukit.Shared.dll` | `Bukit.Shared.Notion.ToggleBlock` | `Bukit.Notion.Blocks.ToggleBlock` in `Bukit.Notion.dll` |
| `Bukit.Shared.dll` | `Bukit.Shared.Notion.CodeBlock` | `Bukit.Notion.Blocks.CodeBlock` in `Bukit.Notion.dll` |
| `Bukit.Shared.dll` | `Bukit.Shared.Notion.CalloutBlock` | `Bukit.Notion.Blocks.CalloutBlock` in `Bukit.Notion.dll` |
| `Bukit.Shared.dll` | `Bukit.Shared.Notion.RichTextSegment` | `Bukit.Notion.Blocks.RichTextSegment` in `Bukit.Notion.dll` |
| `Bukit.Content.dll` | `Bukit.Content.Notion.NotionApiClient` | `Bukit.Notion.Transport.NotionClient` + `Bukit.Notion.Transport.NotionClientOptions` in `Bukit.Notion.dll`; explicitly adapt request semantics, error translation, and `HttpClient` ownership |
| `Bukit.Content.dll` | `Bukit.Content.Notion.NotionContentProvider` | `Bukit.Content.Notion.NotionContentSource` in `Bukit.Content.Notion.dll`; explicitly adapt the consumer interface and content semantics |
| `Bukit.Content.dll` | `Bukit.Content.Notion.NotionProviderOptions` | `Bukit.Content.Notion.NotionContentSourceOptions` in `Bukit.Content.Notion.dll`; explicitly map options; not a drop-in replacement |

The canonical block model namespace is `Bukit.Notion.Blocks`. The canonical
URL and conversion owners are public APIs of `Bukit.Notion.dll`. The three
Content bridge migrations are deliberately descriptive rather than a
one-line type substitution: their internalization is **not a drop-in**
replacement and is **not a productized public SDK**.

## Required consumer action

Source consumers must update assembly references, change namespaces and type
references, then recompile and rerun their own compatibility tests. Merely
replacing a 1.x DLL with a 2.0 DLL is not a supported binary migration.

Binary-only consumers need source or an owner-supplied rebuild. Consumers
using reflection, serializer type-name bindings, or assembly-qualified names
must update those bindings explicitly. Canonical types with similar names do
not preserve the old assembly identity.

`Bukit.Shared.Notion.NotionBlock` was an extensible abstract record. An
external subclass has **no mechanical migration** to the closed canonical
block family. Its owner must choose a canonical subtype or implement an
explicit application-level mapping and test the resulting serialization and
rendering behavior.

## Retained identity

`Bukit.Content.Notion.NotionPropertyParser` remains public in
`Bukit.Content.dll` with its existing two public methods. There is no new
canonical public parser promised by this migration. Its status is
retain-by-design and is reviewed separately if a real correctness/security
defect, a direct consumer declaration, or an approved CLR SDK productization
decision appears.

## Evidence limits

Repository-local and public-web searches did not find a direct current-Core
CLR consumer in the checked scope. That is not proof of absence. Private,
unindexed, binary-only, reflection-based, serializer-bound, externally
subclassed, and undisclosed consumers remain unknown and must not be described
as safe by inference.

## Non-goals

This compatibility cleanup makes no Notion API, HTTP/TLS, retry, cache,
configuration schema, plugin protocol, assets, SEO, global path, content
projection, or canonical runtime behavior change. It also does not implement
Labs or external-plugin business migrations.

The implementation and verification ledger is
[AD-03C final aggregate closure](../analysis/bukit-core-ad03c-final-aggregate-closure-2026-07-24.zh-CN.md).
