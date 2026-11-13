# 06 Notion Content

The Notion provider loads database pages through the Notion API, maps selected
properties to Core fields, optionally renders page blocks, and can cache block
HTML.

## Source Config

```yaml
content:
  sources:
    - type: notion
      mode: content
      notion:
        databaseId: "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
        filterProperty: Published
        filterType: checkbox_true
        sortProperty: PublishAt
        sortDirection: descending
        propertyMap:
          Title: Name
          Slug: Slug
          Type: Kind
          Collection: Section
          PublishAt: PublishAt
          Language: Language
```

`NOTION_TOKEN` must be provided by the environment when provider secret
validation is enabled.

## Important Options

| Field | Purpose |
|---|---|
| `pageSize` | Notion query page size, 1 to 100. |
| `maxItems` | Optional cap for loaded pages. |
| `renderContent` | Enables or disables block body rendering. |
| `renderConcurrency` | Parallel block rendering limit. |
| `maxRps`, `maxRetries` | API throttling and retry controls. |
| `filterType` | `checkbox_true`, `checkbox_false`, `select_equals`, `status_equals`, `rich_text_equals`, or `none`. |
| `filterValue` | Required for select, status, and rich text filters. |
| `cacheMode` | `off`, `readwrite`, or `readonly`. |
| `content.sources[].notion.fieldPolicy.mode` | `whitelist` or `all`. |
| `propertyMap` | Uses Core field keys `Title`, `Slug`, `Type`, `PublishAt`, `Language`, `I18nKey`, `Summary`, `Collection`, `SeoTitle`, `SeoDescription`, `SeoImage`, `Canonical`, `OriginalUrl`, `References`, `EntitiesJson`, `Cover`, `CoverAlt`, and `CoverCaption`. |

The canonical publish date is resolved deterministically: the mapped
`PublishAt` value wins, otherwise Bukit uses the Notion Page object's top-level
`created_time`. If neither value is a valid ISO 8601 timestamp, loading fails
with the page id and both field names. Top-level `last_edited_time` remains an
updated-at/cache value and is never used as the publish date.

`content.sources[].notion.propertyMap.Type` and
`content.sources[].notion.propertyMap.Collection` project different canonical
fields. With Notion values `Kind = article` and `Section = news`, the document
has `type: article` and `collection: news`; neither value is inferred from the
other. Missing content type defaults to `page`, but missing collection after
projection causes the build to fail.

Canonical `Collection` projection happens before the ordinary
`fieldPolicy.mode: whitelist` filtering, so the mapped Collection property does
not need to be repeated in the normal field allowlist. It must resolve to one
scalar string from `rich_text`, `select`, or `status`. `title`, `url`, `email`,
`phone_number`, `formula`, and multi-value properties are rejected with a
`ContentException` that identifies the property type and lists the allowed
types; values are never converted with `ToString()`.

A source-level `collection` can provide ownership instead and overrides the
mapped item collection without changing its mapped type.

## Canonical Source, Entity And Cover Mapping

Each mapping value names a Notion property. These mappings read raw properties
directly and project canonical inputs independently of ordinary field whitelist
filtering; the mapped properties do not need to be repeated in the allowlist:

| Mapping key | Accepted Notion property types | Projected value |
|---|---|---|
| `OriginalUrl` | `url` | `original_url` source URL. |
| `References` | `multi_select`, `rich_text` | `references` list; one rich-text value becomes one list item. |
| `EntitiesJson` | `rich_text` | `entities` parsed from a JSON array of objects. |
| `Cover` | `rich_text`, `url`, `files` | `cover`; a file list uses its first value. |
| `CoverAlt` | `rich_text` | `cover_alt` text. |
| `CoverCaption` | `rich_text` | `cover_caption` text. |

Every `EntitiesJson` object requires non-empty string fields `type`, `name`,
and `description`, for example:

```json
[{"type":"Organization","name":"Example News","description":"Editorial publisher"}]
```

Malformed JSON, a non-array value, a non-object item, or a missing/empty required
string fails content loading. A mapped property with an unsupported Notion
type also fails with a diagnostic identifying the page and property.

## Rendering

Notion blocks are rendered by `NotionBlockRendererRegistry`. Built-in renderers
cover headings, paragraphs, lists, tables, toggles, callouts, media, code,
embeds, equations, synced blocks, and unsupported no-op block types.

## Rate Limiting, Concurrency And Lifecycle

One source load owns one Notion client and one body concurrency gate. The
database query, relation resolution, summary prefetch, cross-source relation
lookup, and lazy body reads for that load share the same rate-limit state, so
`maxRps` and retry behaviour cover the whole load instead of resetting per page.
`renderConcurrency` bounds active block-body renders for the same load.

Each load result has independent resources. Multiple sources, even with the same
token, each have their own budget; allocate `maxRps` per source when several
sources share a token. Disposing a body store closes new reads, waits for
already accepted renders, cancels and observes background work, then releases
the client and gate.

A cancelled waiter does not cancel a shared body render for other waiters. If a
shared render fails or is cancelled after its last waiter left, the failure is
removed so the next read starts a new attempt instead of replaying the old
failure. Successfully prefetched empty or whitespace-only bodies are reused with
`cacheMode: off`, so they are not requested twice.

## Caching And Incremental Rendering

`readwrite` creates the cache directory; `readonly` reads an existing cache and
fails on a miss; `off` does not read or create the cache. The effective
`renderContent` mode is part of the stable incremental fingerprint. Switching it
in either direction invalidates the affected pages once; after the switch is
stable, the next build reuses them again.

A cached page body is tied to the Notion renderer version and to the time it was
fetched, read from Notion's `Date` response header (the local clock is used only
when the header is missing). In `readwrite` mode a cached body is reused only when
the renderer version and `last_edited_time` match, its signed links have not
expired, and it was fetched at least 5 seconds after the minute of
`last_edited_time` ended. Notion records edit times to the minute, so a body
fetched in the same minute as an edit is fetched again on the next build, and an
edit made later in that minute is not lost. Pages without `last_edited_time` are
never reused from the cache in `readwrite` mode.

The incremental body fingerprint is
`{last_edited_time}:renderer={version}[:unsettled]:content={on|off}`.
`:unsettled` marks a page whose database query was answered before its edit minute
settled; the page renders once more after it settles and is then reused. Pages
without `last_edited_time` have no stable fingerprint, so their content hash is
computed from the body.

After upgrading to a Bukit release with a new Notion renderer version, the first
`readwrite` build fetches every Notion page body again. On a large database this
can take a long time: each page needs at least one block request, and Notion
allows about 3 requests per second per integration.

`readonly` keeps its offline behavior: it reuses any cached body whose
`last_edited_time` matches, even one rendered by another renderer version or
with expired links, and reports such entries in one
`event=notion.cache.renderer_mismatch` warning. When you switch a site from
`readonly` back to `readwrite`, build once with `--clean`: pages built from those
older bodies already carry the current fingerprint and would otherwise be kept.

A `readwrite` cache directory must not equal, contain, or sit inside a configured
Markdown content source (or the reverse). Create writable Notion caches in a
dedicated directory such as `.cache/notion`.

## Offline Snapshot

A Notion offline snapshot lets `build`, `config check` and `doctor` run with no
`NOTION_TOKEN` and no network, for example to preview changes made by a tool
that must not hold the token.

```bash
# Formal build with the token: records the snapshot.
NOTION_TOKEN=... bukit build --notion-snapshot /path/outside/the/site/notion-snapshot

# Later, with no token: reads only the snapshot.
bukit config check --notion-snapshot /path/outside/the/site/notion-snapshot --notion-offline
bukit doctor       --notion-snapshot /path/outside/the/site/notion-snapshot --notion-offline
bukit build        --notion-snapshot /path/outside/the/site/notion-snapshot --notion-offline
```

`--notion-snapshot <dir>` overrides `cacheMode` and `cacheDir` of every Notion
source, so no Notion cache is written into the site directory. `--notion-offline`
requires `--notion-snapshot`; `config check` and `doctor` only read a snapshot,
so there `--notion-snapshot` also requires `--notion-offline`.

**What it stores.** The raw Notion API answers of the formal build: the database
schema, every page of the database query with all properties (drafts and fields
the site does not show included) and Notion's server time, relation targets, the
cross-source relation fallback, taxonomy select options and `pages_index` pages,
plus the rendered page bodies. Recorded 403/404 answers are replayed as the same
errors. Because the raw data is stored, a changed `propertyMap` or template works
offline.

**Keep it private.** The snapshot holds more than the site publishes. Keep it
outside the site and outside any checkout that tools or other people can read.
On Unix the directories are `0700` and the files `0600`. The token is never
written to it.

**When it is replaced.** A recording build writes to a staging directory next to
`<dir>` and replaces `<dir>` only after the whole build succeeded; a failed or
cancelled build keeps the previous snapshot. Bodies of pages the build no longer
uses are dropped; unchanged bodies are reused from the previous snapshot. The
snapshot records its format version and the Bukit version; a different version is
an error, so upgrade Bukit and record again.

**Offline rules.**

- No Notion client is created and no request is made. Anything the snapshot does
  not hold is an error, never a silent skip: a `databaseId` it lacks (a changed ID
  or a new Notion source), a page body edited after the formal build, a missing
  relation target, taxonomy options or `pages_index` page.
- A changed filter or sort still builds from the pages the formal build queried
  and logs `event=notion.snapshot.query_changed`.
- Signed file links are not checked for expiry, and Notion's server time comes
  from the snapshot, so bodies and fingerprints match the formal build.
- Images are not downloaded. Images already in the media cache (the build cache,
  or `--cache-dir`) are reused, including Notion files whose signed link has since
  expired; others keep their original URL. Videos, PDFs and other attachments keep
  their signed links, which expire about an hour after the formal build.

Every build logs one `event=content.source.loaded source=<name> count=<n>` line
per content source.
