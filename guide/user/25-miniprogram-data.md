# 25 Mini-Program Data

Bukit can publish the data a mini-program uses as static, versioned JSON next to
the site. Bukit does not create the mini-program project: the pages, components
and rendering stay in your own mini-program repository, which reads these files
with the WeChat request API.

The output is opt-in. A site without a `miniprogram` section builds exactly as
before.

## How It Works

1. You declare datasets in `site.yaml`. Only declared datasets and declared
   fields are written.
2. Each build writes one JSON file per dataset, named after the SHA-256 of its
   bytes, plus one `catalog.json` per language.
3. The mini-program requests only `catalog.json` on launch, compares the hashes
   with its local copy, and downloads only the files that changed.

Editors change the data in Notion. A rebuild and deploy publishes the change; the
mini-program sees it on its next launch.

## Configuration

```yaml
site:
  url: https://example.com        # must be https://; `bukit dev` skips mp/ when unset
  menus:
    main:
      - identifier: home
        name: Home
        url: /

content:
  sources:
    - type: notion
      name: notion-blog
      notion:
        databaseId: "<blog-database-id>"
    - type: notion
      name: notion-modules
      mode: data
      notion:
        databaseId: "<modules-database-id>"
    - type: notion
      name: notion-settings
      mode: data
      notion:
        databaseId: "<settings-database-id>"
      dataIndex:
        scopeField: scope
    - type: notion
      name: notion-stores
      mode: data
      notion:
        databaseId: "<stores-database-id>"

miniprogram:
  enabled: true
  outputDir: mp                   # default mp
  maxListItems: 2000              # default 2000
  datasets:
    - name: articles
      kind: collection
      source: notion-blog
      listFields: [title, summary, cover, publishedAt, tags]
      detailFields: [title, body, cover, publishedAt, updatedAt, author, tags]
      types: { cover: image }
      required: [title]
    - name: banners
      kind: module
      source: notion-modules
      module: banner
      fields: [title, image, link, order]
      sort: { field: order }
      types: { image: image, link: url, order: number }
      required: [title, image]
    - name: nav
      kind: module
      source: notion-modules
      module: nav
      fields: [title, link, order, parent]
      sort: { field: order }
    - name: site-nav
      kind: menu
      menu: main
    - name: settings
      kind: dataIndex
      source: notion-settings
      scope: app
    - name: stores
      kind: records
      source: notion-stores
      fields: [name, address, phone, lat, lng, photo, order]
      sort: { field: order }
      types: { lat: number, lng: number, photo: image }
    - name: tags
      kind: taxonomy
      taxonomy: tags
```

`source` names a `content.sources[].name`. Dataset names match
`^[a-z0-9][a-z0-9-]*$`; `site` is reserved. A key that a kind does not use, such as
`listFields` on a `records` dataset, fails validation.

| Kind | Source | Writes |
|---|---|---|
| `collection` | `mode: content` source | A list file with `listFields`, a detail catalog, and one detail file per page with `detailFields`. Sorted by `sort` (default `publishedAt` descending). |
| `module` | `mode: data` source | Data records whose `type` equals `module`, with `fields`. `sort` is required. A `module` value no record of any site language uses, enabled or not, fails the build (a record whose `locale` is not a site language does not count); records that exist but are all disabled give an empty list. A language whose matching records all carry another `locale` gets an empty list and a warning. |
| `records` | `mode: data` source | Every public record of the source, with `fields`. `sort` is required. Replaces `data/*.yaml` files. |
| `dataIndex` | `mode: data` source with `dataIndex` | The key/value pairs of one `scope`, keys in order. A scope no row of any site language uses fails the build; a scope whose rows are all disabled, or that only another language's rows use, gives empty values. |
| `taxonomy` | none | Terms of `tags`, `categories` or another list field of the pages in `collection` datasets, with their page counts. Terms that differ only in case count as one. Needs at least one `collection` dataset. |
| `menu` | none | One `site.menus` menu, ordered by `weight` as on the website, keeping its nesting, with absolute URLs. |

A `site` dataset is always written with the site name, title, description,
absolute URL, language and languages.

### Fields

Every record has an `id`. Content pages use their public id (the canonical key
or route); data records use an opaque hash so source page ids are not
published. Other fields read, in this order, `slug`, `title`, `summary`, `body`,
`url`, `publishedAt`, `updatedAt`, `author`, `tags`, `categories`,
`collection`, `language`, then the record's own properties. Data records have
no page, so their `url` is the record's own `url` property.

`types` converts and checks a field: `string`, `number`, `boolean`, `date`,
`image`, `url`, `string[]`, `object` or `object[]` (see
[Object Fields](#object-fields)). `image` and `url` values become absolute http(s)
URLs, and one that still holds a signed URL fails the build; relative values of data records resolve against the site root, base path
included. On a multilingual site with `site.sharedAssets`, links to the shared
files (such as theme images under `assets/`) keep the root path in every
language, as the website's do, whether written as a site path, a relative
path or a full URL of the site; localized media keeps the language path. A site
path that already carries the root `baseUrl` (`/repo/about/` on a `/repo/` site)
is kept as written in every language.
`number` keeps integers and decimals exactly (text in JSON number form is
written as is), and `NaN` or infinity fails the build. `date` text must be ISO
8601: a date alone (`2026-01-01`) stays a calendar date, and a date with a time
(`2026-01-01T10:00:00+08:00`) is written as that instant. A date the source
already parsed (Markdown front matter, a Notion date property) is also written
as a calendar date when its time is exactly midnight, because those sources turn
a date-only value into midnight before the mini-program output reads it; a time
of exactly midnight from them is therefore not kept. ISO text keeps the time it
states.

Markdown front matter is typed when the site loads it, for the website too:
numeric text becomes a number (so a decimal keeps double precision, about 15
significant digits) and text that reads as a date becomes a date (including
non-ISO text such as `May`); quoting the value in YAML does not change this.
To keep such a value as text, write it in a form that is neither a number nor
a date. A `sort` field
compares dates stored as dates or as ISO text by time, and large integers
exactly; records whose sort value is text sort after numbers and dates in
either direction, and records without one (or with `NaN`) come last. `required` fields must be present and non-empty. Except in a `dataIndex`
dataset, every `required` and `types` entry must name a declared field. `body`
cannot be given a type: it is always sanitized HTML, including a `body` key in
a `dataIndex` scope, which must be text.

A `body` field on a `module` or `records` dataset from a Notion source needs
`notion.renderContent: true` on that source; Notion data sources skip page
rendering otherwise, and validation reports it. A record that breaks
either rule fails the build with `SchemaValidationFailed`, naming the dataset,
record id and field, so bad data never reaches the mini-program.

`body` is reduced to an HTML subset that WeChat `rich-text` renders: headings,
paragraphs, lists, quotes, code, emphasis, links, images, tables, `hr`, `br` and
`span`. `class` and `style` are removed, unknown containers are unwrapped
(text and images directly inside `div`, `figure`, `figcaption`, `section` and
similar blocks become a paragraph, or is separated by line breaks inside a table cell
or list item, so blocks do not run together), a table `caption` becomes a
paragraph before the table,
a link that wraps a block keeps the block as its own paragraph, scripts and
forms are dropped, `iframe`, `video` and `audio` become links (inline when
they sit inside a paragraph), block markup inside a paragraph, heading or
`summary` is reduced to its text separated by line breaks, and `<details>`
is expanded. An image, link or embed in `body` that still holds a signed URL
fails the build. Links to pages of any `collection` dataset carry
`data-bukit-id` with the target id, so the mini-program can open its own page.

### Object Fields

A field whose value is a mapping, or a list of mappings, is written only when
`types` declares it `object` or `object[]`; without a type such a value fails
the build, as before. A type is either a name or a mapping with `type` and an
optional `fields` mapping that declares nested types the same way:

```yaml
    - name: routes
      kind: collection
      source: routes
      listFields: [title, cover]
      detailFields: [title, cover, days]
      types:
        cover: image
        days:
          type: object[]
          fields:
            title: string
            text: string
            sights: string[]
            photo: image
            hotel: { type: object, fields: { name: string, stars: number } }
```

With `fields`, each object holds exactly the declared keys (a missing one is
written as `null`) and other keys are dropped. Without `fields`
(`days: object[]`), every key is kept and each value is inferred like an
untyped field: text, numbers, booleans, dates and lists of text keep their
form, and a nested mapping or list of mappings becomes an object or a list of
objects. Inferred values are not converted, so an image path stays as written;
declare `image` or `url` to make it an absolute http(s) URL. Declared nested
`image` and `url` values follow the same rules as top-level ones, including the
rejection of signed URLs. Nested strings are plain text: only the top-level
`body` field is HTML and sanitized, so render nested text with a `text`
component, not `rich-text`.

Object keys are written in ordinal order, so reordering keys in the source does
not change a file's hash. `NaN` and infinity fail the build at any depth, and
objects may nest at most 8 levels (a top-level object is level 1); deeper data
fails with `SchemaValidationFailed`, naming the dataset, record and the path of
the field, such as `days[1].hotel`. A single mapping given to `object[]` is
written as a list of one. As at the top level, an empty value inside an object
(an empty list `[]`, an empty mapping, blank text or a YAML `null`) is written
as `null`; quote `"null"` to keep it as text. A `string[]` value drops YAML
null items (an empty `- ` or `~`), at the top level and inside objects. A `dataIndex` dataset accepts the same types for its
keys, although data index rows hold text values today.

Object lists can make list files large. Put them in `detailFields`, so each one
is downloaded only with its own record, and keep `listFields` to what a card
shows. Bukit does not warn about file size; check the `size` of each dataset in
the catalog. A catalog with an object field anywhere has `schemaVersion` and
`minClientSchema` `1.1`, so clients built for `1.0` keep their local data and
ask for an update instead of reading fields they do not expect; sites without
object fields keep writing `1.0` and byte-identical files.

### Visibility

Disabled (`enabled: false`), draft and non-indexable records are not written.
Unchecking `enabled` in Notion removes a record on the next build.

## Output

```text
dist/mp/                         # dist/<lang>/mp/ for subdirectory languages
  catalog.json                   # the only file with a fixed name
  site.<hash16>.json
  banners.<hash16>.json
  articles.list.<hash16>.json
  articles.details.<hash16>.json
  articles/<hash16>.json         # one per page
```

`catalog.json`:

```json
{
  "schema": "https://bukit.dev/schemas/miniprogram-catalog.v1.json",
  "schemaVersion": "1.0",
  "minClientSchema": "1.0",
  "language": "zh-CN",
  "generatedAt": "2026-11-18T08:00:00+08:00",
  "catalogHash": "sha256:…",
  "datasets": {
    "articles": {
      "kind": "collection",
      "hash": "sha256:…",
      "url": "https://example.com/mp/articles.list.9c1d0a2b3c4d5e6f.json",
      "size": 18342,
      "detailCatalog": { "hash": "sha256:…", "url": "https://example.com/mp/articles.details.77ab….json" }
    }
  }
}
```

The files are deterministic: the same content produces the same bytes on every
operating system (UTF-8 without BOM, one trailing `\n`, fixed field order and
record order, no build time). `generatedAt` is the latest record update time.
Unchanged data therefore keeps its hash across rebuilds. `catalogHash` covers
every catalog field except `generatedAt`, so a changed file URL or
`minClientSchema` changes it too. The catalog format is published as a JSON Schema in
`docs/schemas/miniprogram-catalog.v1.schema.json`, whose `$id` is the
catalog's `schema` value.

A set `site.url` that is not `https://`, or has user info, a query or a fragment, fails with
`ConfigInvalidValue`; a
missing one fails a production build with `ConfigRequiredFieldMissing`.

The build manifest tracks every file under `mp/` as it is written, even when a
later dataset fails the build; files no longer referenced
are removed on the next build, and turning the output off removes the
directory.

## Mini-Program Sync Protocol

This is the expected client behavior; Bukit does not ship client code.
[26 Mini-Program Client Integration](26-miniprogram-client.md) has the full
data contract and a reference client implementation.

1. On launch, request `…/mp/catalog.json?t=<timestamp>` to bypass caches.
2. If `catalogHash` equals the stored one, stop.
3. If `minClientSchema` is newer than the client supports, keep the local data
   and ask the user to update the mini-program.
4. Compare each dataset `hash`:
   - small datasets and collection lists: download the changed file now;
   - a changed `detailCatalog`: download it, mark detail files with a changed
     hash as stale, and fetch each one when the user opens it (show the stale
     copy meanwhile).
5. Delete local datasets and details that the catalog no longer lists.
6. Store the new catalog only after every download succeeded.
7. If a data file returns 404 (a deploy replaced it), fetch the catalog again
   and retry once.
8. Keep at most about five concurrent requests and stay under the 10 MB local
   storage limit, evicting least recently used details first.

```js
const catalog = await get(`${base}/mp/catalog.json?t=${Date.now()}`)
if (catalog.catalogHash === local.catalogHash) return
for (const [name, entry] of Object.entries(catalog.datasets)) {
  if (local.datasets[name]?.hash !== entry.hash) store(name, await get(entry.url))
  if (entry.detailCatalog && local.datasets[name]?.detailCatalog?.hash !== entry.detailCatalog.hash) {
    markStaleDetails(name, await get(entry.detailCatalog.url))
  }
}
removeMissing(local, catalog)
saveCatalog(catalog)
```

## Modeling Data In Notion

| Data | Notion database | Dataset |
|---|---|---|
| Articles, pages | One database per collection, `mode: content` | `collection` |
| Banners, notices, FAQ, navigation | One database with a `type` select (`banner`, `nav`, …), `mode: data` | `module` |
| Settings, labels, switches | A database with `scope`, `key`, `value`, `valueType`, plus `dataIndex` | `dataIndex` |
| Stores, prices, other tables (was `data/*.yaml`) | One database per table, `mode: data` | `records` |
| Tags, categories | The pages' `tags` / `categories` properties | `taxonomy` |

Give every `module` and `records` database a number property such as `order`
and use it as `sort`; Notion does not return rows in a stable order. Use Notion
`files` properties for images: image localization downloads them, because
Notion file URLs expire. An `image`- or `url`-typed field that still holds a
signed URL fails the build; other fields are not checked, so give file links a
`url` type.
Navigation can also stay in `site.yaml` `menus` through a `menu` dataset.

## Deployment Notes

- WeChat only accepts request and download domains that are HTTPS and
  ICP-registered; a default `*.github.io` domain usually cannot be added.
- Serve `catalog.json` without caching; the hashed files can be cached forever.
- A Notion edit reaches the mini-program after the next build and deploy.
  Schedule builds in CI (for example every 15 minutes), trigger them manually,
  or, where your Notion plan supports database automations, call a webhook that
  starts the workflow. The mini-program never calls Notion.
- Only WeChat mini-programs are supported targets. The JSON itself is
  platform-neutral.
