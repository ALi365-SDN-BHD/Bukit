# 05 Markdown Content

The Markdown provider reads files from `content.sources[].markdown.dir`, parses
YAML front matter, renders the body with Markdig, and stores the body lazily
through `MarkdownBodyStore`.

## Source Config

```yaml
content:
  sources:
    - type: markdown
      mode: content
      collection: news
      markdown:
        dir: content/news
        defaultType: article
        includeGlobs:
          - "**/*.md"
```

`markdown.defaultType` fills only a missing document `type`. In this example it
produces `type: article`, while the source independently provides
`collection: news`. Omitting collection from both the source and front matter
causes a content build error; `defaultType: article` cannot satisfy collection
ownership. A source collection overrides an item's front matter collection but
does not change its type. If content type is otherwise absent, it defaults to
`page`.

## Front Matter

Common fields:

| Field | Purpose |
|---|---|
| `title` | Page title. |
| `slug` | URL segment used by route patterns. |
| `type` | Document kind; independent from collection and defaults to `page`. |
| `collection` | Selects `site.collections.<key>`. |
| `publishAt` | Explicit publish timestamp used by routes and feeds. Required for date-based permalinks and chronological feed ordering. |
| `language` | Variant selection for i18n. |
| `summary` | Feed, list, and SEO summary. |
| `draft` | Excluded unless `--draft` is passed. |
| `tags`, `categories` | Taxonomy and related content inputs. |
| `route` object with `url` | Optional route override; output path is derived from URL (see below). |

A route URL whose last segment ends in `.html`, `.htm`, `.xml`, `.json`, or
`.txt` (any case) and has no trailing slash is written as that file:
`/docs/page.html` → `docs/page.html`. Every other URL is a directory with an
`index.html`, even when it contains a dot: `/docs/v1.0` → `/docs/v1.0/` and
`docs/v1.0/index.html`. Add a trailing slash (`/docs/page.html/`) to force a
directory.

A route URL ending in a static file extension (`.css`, `.js`, `.pdf`, images,
fonts, archives, audio/video, Office files) without a trailing slash fails the
build; place such files in the theme's `static/` or `assets/` directory instead.
See [Troubleshooting](14-troubleshooting.md#route-ends-in-a-static-file-extension).

Top-level `outputPath` and the `outputPath` field inside the `route` object are
rejected in Bukit 1.0. Use the `route` object's `url` field instead.

Front matter can provide collection ownership instead of the source:

```markdown
---
title: Product launch
slug: product-launch
type: article
collection: news
---
```

Even a complete `url` override inside the front matter `route` object does not
make collection optional.
`addToCollections` on the source creates explicit cloned documents and routes
for its target collections; it is not implicit multi-membership.

## Markdown Behavior

- Headings are used to build `page.table_of_contents`.
- `bodyFingerprint` is derived from the body and participates in incremental
  rendering.
- When `site.autoSummary` is true, missing summaries can be generated from the
  rendered text.
- Media URLs in body HTML can be localized through `content.media`.

## Source Directory Protection

A Markdown `dir` is protected as a whole source root. Build output, the default
or custom cache, media downloads, external metrics files, and Notion writable
caches must not equal, contain, or sit inside a configured Markdown source
directory (and the reverse is also rejected). The check runs before staging,
marker writes, cache creation, or clean deletion; `includePaths` and
`includeGlobs` choose documents but do not narrow the protected root.

`markdown.dir: "."` therefore conflicts with any in-project output or cache.
Use a dedicated content directory such as `content/`. If a conflict is
reported, change the source directory or the managed target; Bukit refuses the
build instead of deleting or overwriting source files.

The guarantee covers configured Markdown roots and the built-in injected
Markdown/Composite and Notion writable-cache identities. A custom provider or
opaque wrapper that reads hidden disk locations is outside this recognizable
source boundary; it keeps the ordinary output safety rules and is not reported
as source-protected.

## Body Changes During a Build

The Markdown provider captures a body-only SHA-256 fingerprint (front matter is
excluded) with the document metadata. When the body is loaded later, Bukit
verifies that the file still matches this fingerprint before rendering it.

If the file changes between metadata loading and body rendering, the build
fails before commit with a "content changed while the build was running"
diagnostic. The previous output, cache, and manifest stay intact. Stop editing
the file and rerun the build; Bukit then publishes the current content. The
diagnostic never prints file contents.

## Publish Date Migration

Markdown file modification times are not content metadata and no longer supply
a missing `publishAt`. An undated Markdown document keeps `PublishedAt` unset,
emits the stable warning `content.publish_at.missing`, and uses the Unix epoch
only as the deterministic canonical fallback.

Add an explicit `publishAt` before relying on `{year}`, `{month}`, or `{day}` in
a permalink, or before relying on chronological feed and archive ordering.
Changing a Markdown file's modification time alone does not change canonical
content, routes, hashes, or feed output.
